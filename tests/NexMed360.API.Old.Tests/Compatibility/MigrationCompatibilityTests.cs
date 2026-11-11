using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NexMed360.Domain.Rules;
using NexMed360Api.Controllers.v1;
using NexMed360Api.Data;
using NexMed360Api.Dtos.User;
using NexMed360Api.Services;
using NexMed360.Infrastructure.Migrations;

namespace NexMed360.API.Old.Tests.Compatibility;

public class MigrationCompatibilityTests
{
    [Fact]
    public void RenamedEndpointsHaveExpectedRoutesVerbsAndAuthorization()
    {
        var expected = JsonSerializer.Deserialize<Endpoint[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Compatibility", "endpoints.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var actual = typeof(UsersController).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(ControllerBase)))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.IsDefined(typeof(HttpMethodAttribute)))
                .Select(method =>
                {
                    Assert.Equal("api/v{version:apiVersion}/[controller]", type.GetCustomAttribute<RouteAttribute>()!.Template);
                    Assert.Equal("1.0", type.GetCustomAttribute<Asp.Versioning.ApiVersionAttribute>()!.Versions.Single().ToString());
                    var auth = type.GetCustomAttributes<AuthorizeAttribute>().Concat(method.GetCustomAttributes<AuthorizeAttribute>()).ToArray();
                    return new Endpoint(type.Name, method.Name,
                        method.GetCustomAttribute<HttpMethodAttribute>()!.HttpMethods.Single(),
                        method.GetCustomAttribute<RouteAttribute>()?.Template ?? method.GetCustomAttribute<HttpMethodAttribute>()!.Template ?? "",
                        auth.SelectMany(a => (a.Roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            .OrderBy(role => role, StringComparer.Ordinal).ToArray(),
                        auth.Length > 0, method.IsDefined(typeof(AllowAnonymousAttribute)) || type.IsDefined(typeof(AllowAnonymousAttribute)));
                })).OrderBy(endpoint => endpoint.Controller, StringComparer.Ordinal)
                .ThenBy(endpoint => endpoint.Action, StringComparer.Ordinal).ToArray();
        Assert.Equal(57, actual.Length);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Fact]
    public void ApprovedDtoSourcesMatchTheCurrentContracts()
    {
        var root = RepositoryRoot();
        var expected = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Compatibility", "dto-source-sha256.json")))!;
        var folder = Path.Combine(root, "src", "NexMed360.Application");
        var files = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin")).ToArray();
        Assert.Equal(expected.Count, files.Count(path => expected.Keys.Any(key => Path.GetFileName(key) == Path.GetFileName(path))));
        foreach (var (relative, hash) in expected)
        {
            // Ignore checkout line-ending normalization; preserve the actual source contract.
            var bytes = Encoding.UTF8.GetBytes(File.ReadAllText(Assert.Single(files.Where(path => Path.GetFileName(path) == Path.GetFileName(relative)))).Replace("\r\n", "\n"));
            Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }
    }

    [Fact]
    public void DomainAndApplicationKeepInwardFrameworkFreeDependencies()
    {
        var domain = typeof(AccountRules).Assembly;
        var application = typeof(UserService).Assembly;
        Assert.Equal("NexMed360.Domain", domain.GetName().Name);
        Assert.Equal("NexMed360.Application", application.GetName().Name);
        Assert.DoesNotContain(domain.GetReferencedAssemblies(), a => a.Name!.StartsWith("NexMed360"));
        foreach (var assembly in new[] { domain, application })
        {
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a =>
                a.Name!.StartsWith("Microsoft.EntityFrameworkCore") || a.Name.StartsWith("Microsoft.AspNetCore")
                || a.Name.StartsWith("Npgsql") || a.Name.StartsWith("Hl7") || a.Name.StartsWith("System.Security.Claims")
                || a.Name == "NexMed360.Infrastructure" || a.Name == "NexMed360.Api");
        }
        Assert.Equal("NexMed360.Application", typeof(ViewUserDto).Assembly.GetName().Name);
        Assert.Equal("NexMed360.Infrastructure", typeof(ApplicationDbContext).Assembly.GetName().Name);
        Assert.Equal("NexMed360.Api", typeof(UsersController).Assembly.GetName().Name);
    }

    [Fact]
    public void NewRelationalModelMatchesGeneratedMigrationWithoutConnectingToPostgres()
    {
        using var context = DesignContext();
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Single(context.Database.GetMigrations());
    }

    [Fact]
    public void InitialMigrationHasGenericModelsOwnershipAndBookingConstraint()
    {
        var migration = new InitialCreate();
        var tables = migration.UpOperations.OfType<CreateTableOperation>().ToDictionary(table => table.Name);
        var requester = tables["CancellationRequests"].Columns.Single(column => column.Name == "RequestedById");
        Assert.Equal("text", requester.ColumnType);
        Assert.False(requester.IsNullable);
        var slot = Assert.Single(migration.UpOperations.OfType<CreateIndexOperation>()
            .Where(index => index.Name == ApplicationDbContext.NonCancelledSlotIndexName));
        Assert.True(slot.IsUnique);
        Assert.Equal("\"Status\" <> 5", slot.Filter);
        Assert.Equal(new[] { "SessionSlotId" }, slot.Columns);
        foreach (var table in new[] { "PractitionerRoles", "OrganizationMemberships", "OrganizationPatients", "Locations",
            "AccessRoles", "MembershipAccessRoles", "Encounters", "EncounterParticipants", "ClinicalRecords", "Prescriptions" })
            Assert.False(tables[table].Columns.Single(column => column.Name == "OrganizationId").IsNullable);
        Assert.Contains("People", tables.Keys);
        Assert.Contains("Practitioners", tables.Keys);
        Assert.DoesNotContain("Doctors", tables.Keys);
        Assert.DoesNotContain("HospitalAdmins", tables.Keys);
        Assert.DoesNotContain("Receptionists", tables.Keys);
        Assert.True(tables["Encounters"].Columns.Single(column => column.Name == "AppointmentId").IsNullable);
    }

    private static ApplicationDbContext DesignContext()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=design_only;Username=unused;Password=unused").Options);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "src", "NexMed360.sln"))) return directory.FullName;
        throw new InvalidOperationException("Run the DTO source compatibility check from the repository checkout.");
    }

    private sealed record Endpoint(string Controller, string Action, string Verb, string Route, string[] Roles, bool Authorized, bool Anonymous);
}
