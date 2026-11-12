using NexMed360.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using NexMed360.Infrastructure.Data;
using NexMed360Api.Common.Auth;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Services;

namespace NexMed360.API.Old.Tests;

public class Phase1ModelTests
{
    [Fact]
    public async Task OnePersonCanBePatientAndPractitionerAtTwoOrganizations()
    {
        using var context = Context();
        var person = new Person { FirstName = "Sam", LastName = "Example" };
        var user = new ApplicationUser { Id = "one-account", Person = person, PersonId = person.Id };
        var patient = new Patient { PersonId = person.Id };
        var practitioner = new Practitioner { PersonId = person.Id };
        var first = new Organization { Name = "First clinic" };
        var second = new Organization { Name = "Second clinic" };
        context.AddRange(user, patient, practitioner, first, second);
        foreach (var organization in new[] { first, second })
        {
            context.Add(new OrganizationMember { OrganizationId = organization.Id, UserId = user.Id });
            context.Add(new OrganizationPatient { OrganizationId = organization.Id, PatientId = patient.Id });
            context.Add(new PractitionerRole { OrganizationId = organization.Id, PractitionerId = practitioner.Id, RoleName = "Nurse" });
        }
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        Assert.Single(await context.Users.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await context.People.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await context.Patients.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await context.Practitioners.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await context.PractitionerRoles.CountAsync(TestContext.Current.CancellationToken));
        var reusedPatient = await new PatientRepository(context).Add(new Patient { PersonId = person.Id });
        Assert.Equal(patient.Id, reusedPatient.Id);
        var choices = await new WorkspaceRepository(context).List(user.Id);
        Assert.Equal(3, choices.Count);
        Assert.DoesNotContain(choices, row => row.Key.Split(':').Length == 3);
        Assert.Equal(2, choices.Count(row => row.Key.StartsWith(WorkspaceModes.Work + ":") && row.Key.Split(':').Length == 2));
        var ownPatient = await new WorkspaceRepository(context).Resolve(user.Id, WorkspaceModes.Patient);
        Assert.Equal(patient.Id, ownPatient!.PatientId);
    }

    [Fact]
    public async Task PatientCanExistAndBeDisplayedWithoutALogin()
    {
        using var context = Context();
        var person = new Person { FirstName = "Pat", LastName = "Example", PhoneNumber = "123" };
        var patient = new Patient { PersonId = person.Id };
        context.AddRange(person, patient);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var view = await new PatientRepository(context).View(patient);
        Assert.Equal("Pat", view!.FirstName);
        Assert.Equal("123", view.PhoneNumber);
        Assert.Empty(await context.Users.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrganizationOwnedRowsRejectAnEmptyOrganization(bool asyncSave)
    {
        using var context = Context();
        context.AccessRoles.Add(new AccessRole { Name = "Invalid role" });
        if (asyncSave)
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        else Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
    }

    [Fact]
    public async Task SavedClinicalOwnershipCannotBeMovedToAnotherOrganization()
    {
        using var context = Context();
        var role = new AccessRole { OrganizationId = Guid.NewGuid(), Name = "Nurse", Permissions = [Permissions.ClinicalRecordsRead] };
        context.Add(role);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        role.OrganizationId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OwnPatientHistorySpansOrganizationsAndExcludesOtherPatientsAndHiddenRecords()
    {
        using var context = Context();
        var person = new Person { FirstName = "Pat", LastName = "Example" };
        var user = new ApplicationUser { Id = "history-owner", Person = person, PersonId = person.Id };
        var patient = new Patient { PersonId = person.Id };
        var other = new Patient { PersonId = Guid.NewGuid() };
        var first = new Organization { Name = "First clinic" };
        var second = new Organization { Name = "Second clinic" };
        context.AddRange(user, patient, other, first, second);
        context.AddRange(
            new ClinicalRecord { OrganizationId = first.Id, PatientId = patient.Id, Diagnosis = "First visit" },
            new ClinicalRecord { OrganizationId = second.Id, PatientId = patient.Id, Diagnosis = "Second visit" },
            new ClinicalRecord { OrganizationId = first.Id, PatientId = other.Id, Diagnosis = "Other patient" },
            new ClinicalRecord { OrganizationId = second.Id, PatientId = patient.Id, Diagnosis = "Internal note", IsPatientVisible = false });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var repository = new ClinicalRecordRepository(context);
        var records = await repository.GetForPatient(patient.Id, user.Id, false);
        Assert.Equal(2, records.Count);
        Assert.Equal(2, records.Select(row => row.OrganizationId).Distinct().Count());
        Assert.Empty(await repository.GetForPatient(other.Id, user.Id, false));
        var views = await repository.Views(records);
        Assert.Equal(new[] { "First clinic", "Second clinic" }, views.Select(row => row.OrganizationName).Order());
    }

    [Fact]
    public void OrganizationReferencesIncludeOrganizationIdAndProfilesAreUniquePerPerson()
    {
        using var context = new ApplicationDbContextFactory().CreateDbContext([]);
        var model = context.GetService<IDesignTimeModel>().Model;
        foreach (var type in model.GetEntityTypes().Where(type => typeof(IOrganizationOwned).IsAssignableFrom(type.ClrType)))
        {
            Assert.False(type.FindProperty(nameof(IOrganizationOwned.OrganizationId))!.IsNullable);
            foreach (var foreignKey in type.GetForeignKeys().Where(key => typeof(IOrganizationOwned).IsAssignableFrom(key.PrincipalEntityType.ClrType)))
                Assert.Contains(foreignKey.Properties, property => property.Name == nameof(IOrganizationOwned.OrganizationId));
        }
        foreach (var type in new[] { typeof(ApplicationUser), typeof(Patient), typeof(Practitioner) })
            Assert.Contains(model.FindEntityType(type)!.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == "PersonId");
        Assert.Equal(Permissions.All.Count, Permissions.All.Distinct().Count());
    }

    [Fact]
    public void MemberRoleLinksCascadeButSharedIdentitiesAndClinicalHistoryStayProtected()
    {
        using var context = new ApplicationDbContextFactory().CreateDbContext([]);
        var model = context.Model;
        var assignment = model.FindEntityType(typeof(MemberRoleAssignment))!;
        var memberRelationship = assignment.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(OrganizationMember));
        Assert.Equal(DeleteBehavior.Cascade, memberRelationship.DeleteBehavior);

        foreach (var type in new[] { typeof(OrganizationMember), typeof(PractitionerRole), typeof(ClinicalRecord), typeof(EncounterParticipant) })
        {
            var foreignKeys = model.FindEntityType(type)!.GetForeignKeys().Where(key =>
                key.PrincipalEntityType.ClrType == typeof(ApplicationUser)
                || key.PrincipalEntityType.ClrType == typeof(Practitioner)
                || key.PrincipalEntityType.ClrType == typeof(PractitionerRole));
            Assert.NotEmpty(foreignKeys);
            Assert.All(foreignKeys, key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));
        }
    }

    [Fact]
    public void RenamedMemberModelsKeepTheirExistingDatabaseMappings()
    {
        using var context = new ApplicationDbContextFactory().CreateDbContext([]);
        var member = context.Model.FindEntityType(typeof(OrganizationMember))!;
        var assignment = context.Model.FindEntityType(typeof(MemberRoleAssignment))!;
        Assert.Equal("OrganizationMemberships", member.GetTableName());
        Assert.Equal("MembershipAccessRoles", assignment.GetTableName());
        var table = StoreObjectIdentifier.Table(assignment.GetTableName()!, assignment.GetSchema());
        Assert.Equal("MembershipId", assignment.FindProperty(nameof(MemberRoleAssignment.OrganizationMemberId))!.GetColumnName(table));
    }

    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
