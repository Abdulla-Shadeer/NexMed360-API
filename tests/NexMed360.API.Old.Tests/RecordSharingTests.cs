using Microsoft.EntityFrameworkCore;
using NexMed360.Application.Access;
using NexMed360.Application.Patients;
using NexMed360.Infrastructure.Repositories;
using NexMed360Api.Common.Auth;
using NexMed360Api.Data;
using NexMed360Api.Dtos.Search;
using NexMed360Api.Entities;
using NexMed360Api.Services;

namespace NexMed360.API.Old.Tests;

public class RecordSharingTests
{
    [Fact]
    public async Task GlobalDiscoveryDoesNotGrantClinicalAccessOrExposeAccountIds()
    {
        using var f = new Fixture();
        f.Work();
        var search = new SearchRepository(f.Context, null!, null!);
        var matches = await search.Patients(InputTypes.Name, "mary");
        var patient = Assert.Single(matches);
        Assert.Equal(f.Patient.Id.ToString(), patient.Id);
        Assert.Empty(patient.UserId);
        Assert.Empty(patient.PersonId);
        Assert.Empty(patient.PhoneNumber);
        Assert.True((await f.Patients.GetPatient(patient.Id)).IsSuccess);
        Assert.Null(await f.Records.FindReadable(f.Source.Id, f.StaffUserId, true));
        Assert.Empty(await search.Patients(InputTypes.Name, "%%"));
        var denied = new SearchService(search, new WorkspaceAccess());
        Assert.Equal(403, (await denied.EntitySearch(new() { Entity = EntityTypes.Patient, InputType = InputTypes.Name, Input = "Mary" }, null!)).Error!.HttpStatus);
    }

    [Fact]
    public async Task PatientApprovalAllowsScopedHistoryAndRevocationStopsDetailAndPrescriptionReads()
    {
        using var f = new Fixture();
        await AssertFullFlow(f);
    }

    public static async Task AssertFullFlow(Fixture f)
    {
        f.Work();
        var id = await f.Request();
        Assert.Null(await f.Records.FindReadable(f.Source.Id, f.StaffUserId, true));
        f.PatientMode();
        var inbox = await f.Sharing.List(f.Other.Id, f.PatientUserId);
        var request = Assert.Single(inbox.Data!);
        Assert.Equal(id, request.Id);
        Assert.Equal(DateTimeKind.Utc, request.ExpiresAt.Kind);
        Assert.Equal("Receiving hospital", request.OrganizationName);
        Assert.Equal("Ben Clinician", request.PractitionerName);
        Assert.Equal("Nurse", request.PractitionerRole);
        Assert.True((await f.Sharing.Decide(id, true, f.PatientUserId)).IsSuccess);
        f.Work();
        var records = await f.Records.GetForPatient(f.Patient.Id, f.StaffUserId, true);
        Assert.Equal(2, records.Count);
        var shared = await f.Patients.ViewClinicalRecord(f.Source.Id.ToString(), f.StaffUserId, true);
        Assert.True(shared.Data!.IsShared);
        Assert.Equal("Source hospital", shared.Data.OrganizationName);
        Assert.Equal("Alice Author", shared.Data.PractitionerName);
        Assert.Equal("Synthetic medicine", Assert.Single(shared.Data.Prescriptions!).MedicineName);
        Assert.Null(await f.Records.FindReadable(f.OtherPatientRecord.Id, f.StaffUserId, true));
        f.PatientMode();
        Assert.True((await f.Sharing.Revoke(id, f.PatientUserId)).IsSuccess);
        f.Work();
        Assert.Null(await f.Records.FindReadable(f.Source.Id, f.StaffUserId, true));
        Assert.Equal(f.Own.Id, Assert.Single(await f.Records.GetForPatient(f.Patient.Id, f.StaffUserId, true)).Id);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Rejected")]
    [InlineData("Expired")]
    public async Task UnapprovedOrExpiredRequestsGrantNothing(string state)
    {
        using var f = new Fixture();
        f.Work();
        var id = await f.Request();
        f.PatientMode();
        if (state == "Rejected") Assert.True((await f.Sharing.Decide(id, false, f.PatientUserId)).IsSuccess);
        if (state == "Expired")
        {
            Assert.True((await f.Sharing.Decide(id, true, f.PatientUserId)).IsSuccess);
            var request = await f.Context.RecordSharingRequests.SingleAsync(row => row.Id == id);
            request.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await f.Context.SaveChangesAsync();
            Assert.Equal("Expired", Assert.Single((await f.Sharing.List(null, f.PatientUserId)).Data!).Status);
        }
        f.Work();
        Assert.Null(await f.Records.FindReadable(f.Source.Id, f.StaffUserId, true));
    }

    [Fact]
    public async Task DateRangeVisibilityAndApprovalCutoffExcludeOtherRecords()
    {
        using var f = new Fixture();
        f.Work();
        var id = await f.Request();
        f.PatientMode();
        Assert.True((await f.Sharing.Decide(id, true, f.PatientUserId)).IsSuccess);
        f.Work();
        var ids = (await f.Records.GetForPatient(f.Patient.Id, f.StaffUserId, true)).Select(row => row.Id).ToArray();
        Assert.Contains(f.Source.Id, ids);
        Assert.Contains(f.Own.Id, ids);
        Assert.DoesNotContain(f.Hidden.Id, ids);
        Assert.DoesNotContain(f.Old.Id, ids);
        Assert.DoesNotContain(f.Future.Id, ids);
    }

    [Fact]
    public async Task OpenDateRangeIncludesPastHistoryButStillExcludesHiddenAndFutureRecords()
    {
        using var f = new Fixture();
        f.Work();
        var created = await f.Sharing.Create(new() { PatientId = f.Patient.Id, PractitionerRoleId = f.ReceivingRole.Id, Purpose = "Care review" }, f.StaffUserId);
        Assert.True(created.IsSuccess);
        f.PatientMode();
        await f.Sharing.Decide(created.Data, true, f.PatientUserId);
        f.Work();
        var records = await f.Records.GetForPatient(f.Patient.Id, f.StaffUserId, true);
        Assert.Contains(records, row => row.Id == f.Old.Id);
        Assert.DoesNotContain(records, row => row.Id == f.Future.Id || row.Id == f.Hidden.Id);
    }

    [Fact]
    public async Task AnotherPatientCannotDecideOrRevokeAndInboxIgnoresSuppliedPatientId()
    {
        using var f = new Fixture();
        f.Work(); var id = await f.Request();
        f.PatientMode(f.Other.Id);
        Assert.Equal(404, (await f.Sharing.Decide(id, true, f.OtherUserId)).Error!.HttpStatus);
        Assert.Equal(404, (await f.Sharing.Revoke(id, f.OtherUserId)).Error!.HttpStatus);
        Assert.Empty((await f.Sharing.List(f.Patient.Id, f.OtherUserId)).Data!);
        f.Work();
        Assert.Equal(403, (await f.Sharing.Decide(id, true, f.StaffUserId)).Error!.HttpStatus);
        Assert.Equal(RecordSharingStatus.Pending, (await f.Context.RecordSharingRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task GrantRequiresRecipientOrganizationReadPermissionAndLocationScope()
    {
        using var f = new Fixture();
        f.Work(); var id = await f.Request();
        f.PatientMode(); await f.Sharing.Decide(id, true, f.PatientUserId);
        f.Work(f.ThirdOrganization.Id);
        Assert.Null(await f.Records.FindReadable(f.Source.Id, f.StaffUserId, true));
        f.Work(read: false);
        Assert.Null(await f.Records.FindReadable(f.Source.Id, f.StaffUserId, true));
        Assert.Equal(403, (await f.Sharing.List(null, f.StaffUserId)).Error!.HttpStatus);
        f.Work();
        f.Access.Current!.LocationIds = [Guid.NewGuid()];
        Assert.Null(await f.Records.FindReadable(f.Source.Id, f.StaffUserId, true));
        Assert.Empty((await f.Sharing.List(null, f.StaffUserId)).Data!);
        f.Work();
        Assert.NotNull(await f.Records.FindReadable(f.Source.Id, f.StaffUserId, true));
    }

    [Fact]
    public async Task SharedReadDoesNotPermitSourceOrganizationWrites()
    {
        using var f = new Fixture();
        f.Work(); var id = await f.Request();
        f.PatientMode(); await f.Sharing.Decide(id, true, f.PatientUserId);
        f.Work();
        var source = (await f.Records.FindReadable(f.Source.Id, f.StaffUserId, true))!;
        source.Diagnosis = "Changed by recipient";
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task NewClinicalCareReusesGlobalPatientWithoutCopyingOrReassigningIdentity()
    {
        using var f = new Fixture(); f.Work();
        var patientCount = await f.Context.Patients.CountAsync();
        var created = await f.Patients.AddToClinicalRecords(new()
        {
            PatientId = f.Patient.Id.ToString(), PractitionerRoleId = f.ReceivingRole.Id,
            Diagnosis = "New care", IsPatientVisible = false
        }, f.StaffUserId);
        Assert.True(created.IsSuccess);
        Assert.Equal(f.ReceivingOrganization.Id, created.Data!.OrganizationId);
        Assert.False(created.Data.IsPatientVisible);
        Assert.Equal(patientCount, await f.Context.Patients.CountAsync());
        f.PatientMode();
        Assert.Null(await f.Records.FindReadable(Guid.Parse(created.Data.Id), f.PatientUserId, false));
    }

    [Fact]
    public async Task RequestValidationRequiresActiveOwnedAssignmentPortalAccountAndValidScope()
    {
        using var f = new Fixture(); f.Work();
        var dto = new CreateRecordSharingRequestDto { PatientId = f.Patient.Id, PractitionerRoleId = f.ReceivingRole.Id, Purpose = "   " };
        Assert.Equal(400, (await f.Sharing.Create(dto, f.StaffUserId)).Error!.HttpStatus);
        dto.Purpose = "Care"; dto.RecordFromDate = DateOnly.FromDateTime(DateTime.UtcNow); dto.RecordToDate = dto.RecordFromDate.Value.AddDays(-1);
        Assert.Equal(400, (await f.Sharing.Create(dto, f.StaffUserId)).Error!.HttpStatus);
        dto.RecordFromDate = dto.RecordToDate = null; dto.AccessExpiresOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        Assert.Equal(400, (await f.Sharing.Create(dto, f.StaffUserId)).Error!.HttpStatus);
        dto.AccessExpiresOn = null; dto.PractitionerRoleId = f.SourceRole.Id;
        Assert.Equal(403, (await f.Sharing.Create(dto, f.StaffUserId)).Error!.HttpStatus);
        dto.PractitionerRoleId = f.ReceivingRole.Id;
        var role = await f.Context.PractitionerRoles.SingleAsync(row => row.Id == f.ReceivingRole.Id);
        role.IsActive = false; await f.Context.SaveChangesAsync();
        Assert.Equal(403, (await f.Sharing.Create(dto, f.StaffUserId)).Error!.HttpStatus);
        role.IsActive = true; await f.Context.SaveChangesAsync();
        var user = await f.Context.Users.SingleAsync(row => row.Id == f.PatientUserId);
        user.IsActive = false; await f.Context.SaveChangesAsync();
        Assert.Equal(409, (await f.Sharing.Create(dto, f.StaffUserId)).Error!.HttpStatus);
    }

    [Fact]
    public async Task RepeatedDecisionsCannotChangeApprovalAndOverlappingGrantSurvivesOneRevocation()
    {
        using var f = new Fixture(); f.Work();
        var first = await f.Request(); var second = await f.Request();
        f.PatientMode();
        Assert.True((await f.Sharing.Decide(first, true, f.PatientUserId)).IsSuccess);
        Assert.Equal(409, (await f.Sharing.Decide(first, false, f.PatientUserId)).Error!.HttpStatus);
        Assert.True((await f.Sharing.Decide(second, true, f.PatientUserId)).IsSuccess);
        Assert.True((await f.Sharing.Revoke(first, f.PatientUserId)).IsSuccess);
        f.Work();
        Assert.NotNull(await f.Records.FindReadable(f.Source.Id, f.StaffUserId, true));
    }

    [Fact]
    public async Task CompetingDecisionsKeepTheFirstSavedDecision()
    {
        using var f = new Fixture(); f.Work(); var id = await f.Request();
        var access = new WorkspaceAccess(); access.Select(new() { Mode = WorkspaceModes.Patient, PatientId = f.Patient.Id });
        using var other = new ApplicationDbContext(f.Options, access);
        var firstRepo = new RecordSharingRepository(f.Context);
        var secondRepo = new RecordSharingRepository(other);
        var first = (await firstRepo.FindForPatient(id, f.Patient.Id))!;
        var second = (await secondRepo.FindForPatient(id, f.Patient.Id))!;
        first.Decide(true, f.PatientUserId, DateTime.UtcNow);
        second.Decide(false, f.PatientUserId, DateTime.UtcNow);
        Assert.True(await firstRepo.Save(first));
        Assert.False(await secondRepo.Save(second));
        Assert.Equal(RecordSharingStatus.Approved, (await other.RecordSharingRequests.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public void ExpiredAndCompletedRequestsCannotBeApprovedAgain()
    {
        var request = new RecordSharingRequest { ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
        Assert.Throws<InvalidOperationException>(() => request.Decide(true, "patient", DateTime.UtcNow));
        request.ExpiresAt = DateTime.UtcNow.AddDays(1);
        request.Decide(false, "patient", DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => request.Decide(true, "patient", DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => request.Revoke("patient", DateTime.UtcNow));
    }

    public sealed class Fixture : IDisposable
    {
        public DbContextOptions<ApplicationDbContext> Options { get; }
        public WorkspaceAccess Access { get; } = new();
        public ApplicationDbContext Context { get; }
        public ClinicalRecordRepository Records { get; }
        public PatientService Patients { get; }
        public RecordSharingService Sharing { get; }
        public Organization SourceOrganization { get; } = new() { Name = "Source hospital" };
        public Organization ReceivingOrganization { get; } = new() { Name = "Receiving hospital", Phone = "0000000000" };
        public Organization ThirdOrganization { get; } = new() { Name = "Third hospital" };
        public string PatientUserId { get; } = "patient-" + Guid.NewGuid();
        public string OtherUserId { get; } = "other-" + Guid.NewGuid();
        public string StaffUserId { get; } = "staff-" + Guid.NewGuid();
        public Patient Patient { get; }
        public Patient Other { get; }
        public PractitionerRole ReceivingRole { get; }
        public PractitionerRole SourceRole { get; }
        public ClinicalRecord Source { get; }
        public ClinicalRecord Hidden { get; }
        public ClinicalRecord Old { get; }
        public ClinicalRecord Future { get; }
        public ClinicalRecord Own { get; }
        public ClinicalRecord OtherPatientRecord { get; }

        public Fixture(DbContextOptions<ApplicationDbContext>? options = null)
        {
            Options = options ?? new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            Context = new(Options, Access);
            var patientPerson = new Person { FirstName = "Mary", LastName = "Patient", Email = "mary@example.test" };
            var otherPerson = new Person { FirstName = "Other", LastName = "Patient", Email = "other@example.test" };
            var staff = new Person { FirstName = "Ben", LastName = "Clinician", Email = "ben@example.test" };
            var author = new Person { FirstName = "Alice", LastName = "Author" };
            Patient = new() { PersonId = patientPerson.Id, BirthDate = new(2000, 1, 1), Gender = "Other" };
            Other = new() { PersonId = otherPerson.Id, BirthDate = new(1990, 1, 1), Gender = "Other" };
            var receivingPractitioner = new Practitioner { PersonId = staff.Id };
            var sourcePractitioner = new Practitioner { PersonId = author.Id };
            ReceivingRole = new() { PractitionerId = receivingPractitioner.Id, OrganizationId = ReceivingOrganization.Id, RoleName = "Nurse" };
            SourceRole = new() { PractitionerId = sourcePractitioner.Id, OrganizationId = SourceOrganization.Id };
            Source = Record(SourceRole, Patient.Id, "Source care");
            Hidden = Record(SourceRole, Patient.Id, "Hidden care"); Hidden.IsPatientVisible = false;
            Old = Record(SourceRole, Patient.Id, "Old care"); Old.VisitDate = DateTime.UtcNow.AddDays(-80);
            Future = Record(SourceRole, Patient.Id, "Future care"); Future.CreatedDate = DateTime.UtcNow.AddHours(1);
            Own = Record(ReceivingRole, Patient.Id, "Local care");
            OtherPatientRecord = Record(SourceRole, Other.Id, "Other patient");
            Source.Prescriptions = [new() { OrganizationId = SourceOrganization.Id, MedicineName = "Synthetic medicine", Dosage = "Test dose", Duration = "1 day" }];
            Context.AddRange(SourceOrganization, ReceivingOrganization, ThirdOrganization, patientPerson, otherPerson, staff, author,
                Patient, Other, receivingPractitioner, sourcePractitioner, ReceivingRole, SourceRole, Source, Hidden, Old, Future, Own, OtherPatientRecord,
                new ApplicationUser { Id = PatientUserId, PersonId = patientPerson.Id, Person = patientPerson },
                new ApplicationUser { Id = OtherUserId, PersonId = otherPerson.Id, Person = otherPerson },
                new ApplicationUser { Id = StaffUserId, PersonId = staff.Id, Person = staff });
            Context.SaveChanges(); Context.ChangeTracker.Clear();
            Records = new(Context);
            Patients = new(null!, new PatientRepository(Context), null!, Records, new PractitionerRoleRepository(Context), Access);
            Sharing = new(new RecordSharingRepository(Context), new PatientRepository(Context), new PractitionerRoleRepository(Context), Access);
        }

        private static ClinicalRecord Record(PractitionerRole role, Guid patientId, string diagnosis) => new()
        {
            OrganizationId = role.OrganizationId, PractitionerRoleId = role.Id, PatientId = patientId,
            Diagnosis = diagnosis, VisitDate = DateTime.UtcNow.AddDays(-10), CreatedDate = DateTime.UtcNow.AddHours(-1)
        };

        public void Work(Guid? organizationId = null, bool read = true) => Access.Select(new()
        {
            Key = "work:" + (organizationId ?? ReceivingOrganization.Id), Mode = WorkspaceModes.Work,
            OrganizationId = organizationId ?? ReceivingOrganization.Id, PractitionerRoleId = ReceivingRole.Id,
            PractitionerRoleIds = [ReceivingRole.Id], Permissions = read
                ? [Permissions.PatientsRead, Permissions.ClinicalRecordsRead, Permissions.ClinicalRecordsWrite, Permissions.PrescriptionsIssue] : []
        });

        public void PatientMode(Guid? patientId = null) => Access.Select(new()
        {
            Key = WorkspaceModes.Patient, Mode = WorkspaceModes.Patient, PatientId = patientId ?? Patient.Id
        });

        public async Task<Guid> Request()
        {
            var result = await Sharing.Create(new()
            {
                PatientId = Patient.Id, PractitionerRoleId = ReceivingRole.Id, Purpose = "Continuing care",
                RecordFromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)), RecordToDate = DateOnly.FromDateTime(DateTime.UtcNow)
            }, StaffUserId);
            Assert.True(result.IsSuccess, result.Error?.Description);
            return result.Data;
        }

        public void Dispose() => Context.Dispose();
    }
}
