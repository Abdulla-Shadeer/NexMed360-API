using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NexMed360.Application.Access;
using NexMed360.Application.Fhir;
using NexMed360.Infrastructure;
using NexMed360.Infrastructure.Fhir;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;

namespace NexMed360.API.Old.Tests;

public class FhirPartnerAuthenticationTests
{
    [Fact]
    public async Task AuthenticationUsesTheConfiguredIdentityAndDoesNotGrantPortalClaims()
    {
        using var f = new Fixture();
        var context = f.Context(Fixture.Key);
        context.Request.Headers["X-Workspace"] = "platform";
        context.Request.Headers["X-Partner-Id"] = "another-partner";
        context.Request.Headers["X-Organization-Id"] = Guid.NewGuid().ToString();
        var result = await context.AuthenticateAsync(FhirPartnerAuthentication.Scheme);
        Assert.True(result.Succeeded);
        var access = context.RequestServices.GetRequiredService<FhirPartnerAccess>();
        Assert.Equal("instance-b", access.PartnerId);
        Assert.Equal(f.LocalOrganization, access.LocalOrganizationId);
        Assert.Equal(f.RemoteOrganization, access.RemoteOrganizationId);
        Assert.True(access.Has(FhirOperations.RecordsRead));
        Assert.Null(result.Principal!.FindFirst(ClaimTypes.NameIdentifier));
        Assert.Null(result.Principal.FindFirst(ClaimTypes.Role));
        Assert.False(context.RequestServices.GetRequiredService<WorkspaceAccess>().IsSelected);
        var defaultScheme = await context.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>().GetDefaultAuthenticateSchemeAsync();
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, defaultScheme!.Name);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong")]
    [InlineData("hash")]
    [InlineData("short")]
    [InlineData("multiple")]
    [InlineData("http")]
    [InlineData("disabled-feature")]
    [InlineData("disabled-partner")]
    [InlineData("query-only")]
    public async Task InvalidKeysAndUntrustedTransportNeverPopulatePartnerAccess(string reason)
    {
        using var f = new Fixture();
        if (reason == "disabled-feature") f.Configuration["Fhir:Enabled"] = "false";
        if (reason == "disabled-partner") f.Configuration["Fhir:Partners:0:Enabled"] = "false";
        var supplied = reason == "wrong" ? new string('x', 48) : reason == "hash" ? Fixture.KeyHash : reason == "short" ? "short" : Fixture.Key;
        var context = f.Context(reason is "missing" or "query-only" ? null : supplied);
        if (reason == "http") context.Request.Scheme = "http";
        if (reason == "multiple") context.Request.Headers.Append(FhirPartnerAuthentication.KeyHeader, Fixture.Key);
        if (reason == "query-only") context.Request.QueryString = new("?api_key=" + Fixture.Key);
        var result = await context.AuthenticateAsync(FhirPartnerAuthentication.Scheme);
        Assert.False(result.Succeeded);
        Assert.False(context.RequestServices.GetRequiredService<FhirPartnerAccess>().IsAuthenticated);
        if (result.Failure != null) Assert.Equal("Partner authentication failed.", result.Failure.Message);
        await context.ChallengeAsync(FhirPartnerAuthentication.Scheme);
        Assert.Equal(401, context.Response.StatusCode);
        Assert.Equal("ApiKey", context.Response.Headers.WWWAuthenticate.ToString());
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task DisabledPartnersAreRejectedOnTheNextRequest()
    {
        using var f = new Fixture();
        Assert.True((await f.Context(Fixture.Key).AuthenticateAsync(FhirPartnerAuthentication.Scheme)).Succeeded);
        f.Configuration["Fhir:Partners:0:Enabled"] = "false";
        var next = f.Context(Fixture.Key);
        Assert.False((await next.AuthenticateAsync(FhirPartnerAuthentication.Scheme)).Succeeded);
        Assert.False(next.RequestServices.GetRequiredService<FhirPartnerAccess>().IsAuthenticated);
    }

    [Fact]
    public async Task PartnerKeysAndPortalTokensDoNotSubstituteForEachOther()
    {
        using var f = new Fixture();
        var keyOnly = f.Context(Fixture.Key);
        Assert.False((await keyOnly.AuthenticateAsync()).Succeeded);
        var jwtOnly = f.Context();
        jwtOnly.Request.Headers.Authorization = "Bearer " + f.PortalToken();
        Assert.True((await jwtOnly.AuthenticateAsync()).Succeeded);
        Assert.False((await jwtOnly.AuthenticateAsync(FhirPartnerAuthentication.Scheme)).Succeeded);
        Assert.False(jwtOnly.RequestServices.GetRequiredService<FhirPartnerAccess>().IsAuthenticated);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ThePartnerPolicyAcceptsPartnerKeysAndRejectsPortalTokens(bool usePartnerKey)
    {
        using var f = new Fixture();
        var context = f.Context(usePartnerKey ? Fixture.Key : null);
        if (!usePartnerKey) context.Request.Headers.Authorization = "Bearer " + f.PortalToken();
        var policy = (await context.RequestServices.GetRequiredService<IAuthorizationPolicyProvider>().GetPolicyAsync(FhirPartnerAuthentication.Policy))!;
        Assert.Equal(new[] { FhirPartnerAuthentication.Scheme }, policy.AuthenticationSchemes);
        var evaluator = context.RequestServices.GetRequiredService<IPolicyEvaluator>();
        var authentication = await evaluator.AuthenticateAsync(policy, context);
        var authorization = await evaluator.AuthorizeAsync(policy, authentication, context, null);
        Assert.Equal(usePartnerKey, authorization.Succeeded);
        Assert.Equal(!usePartnerKey, authorization.Challenged);
        Assert.Equal(usePartnerKey, context.RequestServices.GetRequiredService<FhirPartnerAccess>().IsAuthenticated);
    }

    [Fact]
    public async Task TheWorkspaceFilterSkipsWorkspaceLoadingForPartnerProtectedRoutes()
    {
        using var f = new Fixture();
        var context = f.Context(Fixture.Key);
        var ticket = await context.AuthenticateAsync(FhirPartnerAuthentication.Scheme);
        context.User = ticket.Principal!;
        var workspace = new WorkspaceAccess();
        var filter = new WorkspaceFilter(new WorkspaceService(null!), workspace);
        var (executing, action) = Action(context, new AuthorizeAttribute { Policy = FhirPartnerAuthentication.Policy });
        var called = false;
        await filter.OnActionExecutionAsync(executing, () =>
        {
            called = true; return Task.FromResult(new ActionExecutedContext(action, [], new object()));
        });
        Assert.True(called);
        Assert.Null(executing.Result);

        Assert.False(workspace.IsSelected);
    }

    [Fact]
    public async Task APartnerIdentityCannotUsePortalWorkspaceRoutes()
    {
        using var f = new Fixture();
        var context = f.Context(Fixture.Key);
        context.User = (await context.AuthenticateAsync(FhirPartnerAuthentication.Scheme)).Principal!;
        var (executing, _) = Action(context, new WorkspaceAttribute(WorkspaceModes.Work, Permissions.ClinicalRecordsRead));
        var filter = new WorkspaceFilter(new WorkspaceService(null!), new WorkspaceAccess());
        await filter.OnActionExecutionAsync(executing, () => throw new InvalidOperationException("The action must not run."));
        Assert.IsType<UnauthorizedResult>(executing.Result);
    }

    private static (ActionExecutingContext Executing, ActionContext Action) Action(HttpContext context, object rule)
    {
        var descriptor = new ActionDescriptor { EndpointMetadata = [rule] };
        var action = new ActionContext(context, new RouteData(), descriptor);
        return (new ActionExecutingContext(action, [], new Dictionary<string, object?>(), new object()), action);
    }

    private sealed class Fixture : IDisposable
    {
        public const string Key = "test-only-partner-key-not-used-outside-this-fixture-123456";
        public static string KeyHash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Key)));
        public Guid LocalOrganization { get; } = Guid.NewGuid();
        public Guid RemoteOrganization { get; } = Guid.NewGuid();
        public IConfigurationRoot Configuration { get; }
        private readonly ServiceProvider provider;
        private readonly List<IServiceScope> scopes = [];
        private const string SigningKey = "test-only-portal-signing-key-for-authentication-isolation-1234567890";

        public Fixture()
        {
            Configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=1;Database=design_only;Username=unused;Password=unused",
                ["Auth:Jwt:Issuer"] = "test", ["Auth:Jwt:Audience"] = "test", ["Auth:Jwt:Signing-Key"] = SigningKey,
                ["Fhir:Enabled"] = "true", ["Fhir:PublicBaseUrl"] = "https://localhost:7101/fhir",
                ["Fhir:IdentifierSystem"] = "urn:nexmed360:instance-a:patients", ["Fhir:Partners:0:Id"] = "instance-b",
                ["Fhir:Partners:0:Name"] = "Peer hospital", ["Fhir:Partners:0:Contact"] = "peer@example.test",
                ["Fhir:Partners:0:BaseUrl"] = "https://localhost:7102", ["Fhir:Partners:0:LocalOrganizationId"] = LocalOrganization.ToString(),
                ["Fhir:Partners:0:RemoteOrganizationId"] = RemoteOrganization.ToString(),
                ["Fhir:Partners:0:IncomingApiKeyHash"] = KeyHash, ["Fhir:Partners:0:OutgoingApiKeySecretName"] = "Fhir:Secrets:Peer:Key",
                ["Fhir:Partners:0:AllowedOperations:0"] = FhirOperations.PatientsSearch,
                ["Fhir:Partners:0:AllowedOperations:1"] = FhirOperations.RecordsRead,
                ["Fhir:Partners:0:AllowedOperations:2"] = FhirOperations.SharingRequest
            }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddNexMed360Infrastructure(Configuration);
            provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        public DefaultHttpContext Context(string? key = null)
        {
            var scope = provider.CreateScope(); scopes.Add(scope);
            var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            context.Request.Scheme = "https";
            context.Response.Body = new MemoryStream();
            if (key != null) context.Request.Headers[FhirPartnerAuthentication.KeyHeader] = key;
            return context;
        }

        public string PortalToken() => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("test", "test",
            [new Claim(ClaimTypes.NameIdentifier, "portal-user"), new Claim(FhirPartnerAuthentication.PartnerClaim, "instance-b")],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)));

        public void Dispose()
        {
            foreach (var scope in scopes) scope.Dispose();
            provider.Dispose(); (Configuration as IDisposable)?.Dispose();
        }
    }
}
