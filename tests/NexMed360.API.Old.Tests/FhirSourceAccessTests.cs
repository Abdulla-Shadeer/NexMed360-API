using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NexMed360.Application.Fhir;
using NexMed360.Infrastructure.Repositories;
using NexMed360Api.Entities;

namespace NexMed360.API.Old.Tests;

public class FhirSourceAccessTests
{
    [Fact]
    public async Task DiscoveryFindsSharedIdentitiesWithoutMedicalOrAccountData()
    {
        using var f = new RecordSharingTests.Fixture();
        var access = Access(f, FhirOperations.PatientsSearch);
        var service = Service(f, access);
        var patient = Assert.Single((await service.SearchPatients(new(Name: "MARY"))).Data!);
        Assert.Equal(f.Patient.Id, patient.Id);
        Assert.Equal(f.Patient.Id, Assert.Single((await service.SearchPatients(new(Email: "MARY@EXAMPLE.TEST"))).Data!).Id);
        Assert.Equal(f.Patient.Id, (await service.GetPatient(f.Patient.Id)).Data!.Id);
        var json = JsonSerializer.Serialize(patient, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.DoesNotContain("personId", json);
        Assert.DoesNotContain("userId", json);
        Assert.DoesNotContain("phoneNumber", json);
        Assert.DoesNotContain("diagnosis", json);
        Assert.Equal(403, (await service.GetRecords(f.Patient.Id)).Error!.HttpStatus);
    }

    [Fact]
    public async Task DiscoveryIsBoundedAndRejectsEmptyOrInvalidFilters()
    {
        using var f = new RecordSharingTests.Fixture();
        var service = Service(f, Access(f, FhirOperations.PatientsSearch));
        Assert.Equal(400, (await service.SearchPatients(new(Name: " "))).Error!.HttpStatus);
        Assert.Equal(400, (await service.SearchPatients(new(Name: "Mary", Count: 31))).Error!.HttpStatus);
        Assert.Equal(400, (await service.SearchPatients(new(Name: "Mary", Offset: -1))).Error!.HttpStatus);
        Assert.Equal(400, (await service.SearchPatients(new(Name: new string('x', 201)))).Error!.HttpStatus);
        Assert.Single((await service.SearchPatients(new(Name: "Patient", Count: 1))).Data!);
        Assert.Single((await service.SearchPatients(new(Name: "Patient", Offset: 1, Count: 1))).Data!);
        Assert.Equal(404, (await service.GetPatient(Guid.NewGuid())).Error!.HttpStatus);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("pending")]
    [InlineData("rejected")]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("other-partner")]
    [InlineData("other-patient")]
    [InlineData("local-recipient")]
    [InlineData("future-approval")]
    public async Task APartnerKeyAloneDoesNotAuthorizeHistory(string state)
    {
        using var f = new RecordSharingTests.Fixture();
        if (state != "none")
        {
            var request = Grant(f);
            if (state != "pending")
                request.Decide(state != "rejected", f.PatientUserId, state == "future-approval" ? DateTime.UtcNow.AddMinutes(5) : DateTime.UtcNow);
            if (state == "expired") request.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            if (state == "revoked") request.Revoke(f.PatientUserId, DateTime.UtcNow);
            if (state == "other-partner") request.ReceivingPartnerId = "instance-c";
            if (state == "other-patient") request.PatientId = f.Other.Id;
            if (state == "local-recipient")
            {
                request.ReceivingPartnerId = null;
                request.ReceivingOrganizationId = f.ReceivingOrganization.Id;
                request.PractitionerRoleId = f.ReceivingRole.Id;
            }
            f.Context.Add(request); await f.Context.SaveChangesAsync();
        }
        var service = Service(f, Access(f, FhirOperations.RecordsRead));
        Assert.Equal(403, (await service.GetRecords(f.Patient.Id)).Error!.HttpStatus);
        Assert.Equal(404, (await service.GetRecord(f.Source.Id)).Error!.HttpStatus);
        Assert.Equal(404, (await service.GetPrescription(f.Source.Prescriptions![0].Id)).Error!.HttpStatus);
        if (state == "other-patient")
        {
            // A different patient's approval can expose the same author, but not this patient's records.
            Assert.True((await service.GetRecords(f.Other.Id)).IsSuccess);
            Assert.True((await service.GetPractitioner(f.SourceRole.PractitionerId)).IsSuccess);
            return;
        }
        Assert.Equal(404, (await service.GetOrganization(f.SourceOrganization.Id)).Error!.HttpStatus);
        Assert.Equal(404, (await service.GetPractitionerRole(f.SourceRole.Id)).Error!.HttpStatus);
        Assert.Equal(404, (await service.GetPractitioner(f.SourceRole.PractitionerId)).Error!.HttpStatus);
    }

    [Fact]
    public async Task ApprovedReadsKeepSourceVisibilityDatesAndCreationCutoff()
    {
        using var f = new RecordSharingTests.Fixture();
        var request = await Approve(f);
        var service = Service(f, Access(f, FhirOperations.RecordsRead));
        Assert.Equal(f.Source.Id, Assert.Single((await service.GetRecords(f.Patient.Id)).Data!).Id);
        Assert.True((await service.GetRecord(f.Source.Id)).IsSuccess);
        Assert.True((await service.GetPrescription(f.Source.Prescriptions![0].Id)).IsSuccess);
        Assert.Equal(404, (await service.GetRecord(f.Hidden.Id)).Error!.HttpStatus);
        Assert.Equal(404, (await service.GetRecord(f.Old.Id)).Error!.HttpStatus);
        Assert.Equal(404, (await service.GetRecord(f.Future.Id)).Error!.HttpStatus);
        Assert.Equal(404, (await service.GetRecord(f.Own.Id)).Error!.HttpStatus);
        Assert.Equal(404, (await service.GetRecord(f.OtherPatientRecord.Id)).Error!.HttpStatus);

        request.RecordFromDate = DateOnly.FromDateTime(f.Source.VisitDate);
        request.RecordToDate = request.RecordFromDate;
        await f.Context.SaveChangesAsync();
        Assert.Equal(f.Source.Id, Assert.Single((await service.GetRecords(f.Patient.Id)).Data!).Id);
    }

    [Fact]
    public async Task UnknownDatesAndPrescriptionsAddedAfterApprovalStayPrivate()
    {
        using var f = new RecordSharingTests.Fixture();
        await Approve(f);
        var missingDate = new ClinicalRecord { PatientId = f.Patient.Id, OrganizationId = f.SourceOrganization.Id,
            PractitionerRoleId = f.SourceRole.Id, Diagnosis = "Unknown creation", CreatedDate = null, VisitDate = f.Source.VisitDate };
        var futureVisit = new ClinicalRecord { PatientId = f.Patient.Id, OrganizationId = f.SourceOrganization.Id,
            PractitionerRoleId = f.SourceRole.Id, Diagnosis = "Future visit", CreatedDate = DateTime.UtcNow.AddHours(-1), VisitDate = DateTime.UtcNow.AddHours(1) };
        var laterPrescription = new Prescription { ClinicalRecordId = f.Source.Id, OrganizationId = f.SourceOrganization.Id,
            MedicineName = "Future medicine", Dosage = "Test", CreatedDate = DateTime.UtcNow.AddMinutes(1) };
        f.Context.AddRange(missingDate, futureVisit, laterPrescription); await f.Context.SaveChangesAsync();
        var service = Service(f, Access(f, FhirOperations.RecordsRead));
        var record = Assert.Single((await service.GetRecords(f.Patient.Id)).Data!);
        Assert.Equal(f.Source.Id, record.Id);
        Assert.Equal(f.Source.Prescriptions![0].Id, Assert.Single(record.Prescriptions!).Id);
        Assert.Single((await service.GetRecord(f.Source.Id)).Data!.Prescriptions!);
        Assert.Equal(404, (await service.GetPrescription(laterPrescription.Id)).Error!.HttpStatus);
    }

    [Fact]
    public async Task AuthorReadsNeedAReadableRecordAndRetainHistoricalAuthors()
    {
        using var f = new RecordSharingTests.Fixture();
        await Approve(f);
        var role = await f.Context.PractitionerRoles.FindAsync(f.SourceRole.Id);
        role!.IsActive = false; role.EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        await f.Context.SaveChangesAsync();
        var service = Service(f, Access(f, FhirOperations.RecordsRead));
        Assert.True((await service.GetOrganization(f.SourceOrganization.Id)).IsSuccess);
        Assert.False((await service.GetPractitionerRole(f.SourceRole.Id)).Data!.IsActive);
        Assert.Equal("Alice", (await service.GetPractitioner(f.SourceRole.PractitionerId)).Data!.FirstName);
        Assert.Equal(404, (await service.GetOrganization(f.ReceivingOrganization.Id)).Error!.HttpStatus);
        Assert.Equal(404, (await service.GetPractitionerRole(f.ReceivingRole.Id)).Error!.HttpStatus);
        Assert.Equal(404, (await service.GetPractitioner(f.ReceivingRole.PractitionerId)).Error!.HttpStatus);
    }

    [Fact]
    public async Task RevocationAndExpiryStopFreshRecordPrescriptionAndAuthorReads()
    {
        using var f = new RecordSharingTests.Fixture();
        var request = await Approve(f);
        var service = Service(f, Access(f, FhirOperations.RecordsRead));
        Assert.True((await service.GetRecord(f.Source.Id)).IsSuccess);
        request.Revoke(f.PatientUserId, DateTime.UtcNow); await f.Context.SaveChangesAsync();
        Assert.Equal(403, (await service.GetRecords(f.Patient.Id)).Error!.HttpStatus);
        Assert.Equal(404, (await service.GetPrescription(f.Source.Prescriptions![0].Id)).Error!.HttpStatus);
        Assert.Equal(404, (await service.GetPractitioner(f.SourceRole.PractitionerId)).Error!.HttpStatus);
        var replacement = await Approve(f);
        Assert.True((await service.GetOrganization(f.SourceOrganization.Id)).IsSuccess);
        replacement.ExpiresAt = DateTime.UtcNow.AddSeconds(-1); await f.Context.SaveChangesAsync();
        Assert.Equal(404, (await service.GetOrganization(f.SourceOrganization.Id)).Error!.HttpStatus);
    }

    [Fact]
    public async Task MultipleApprovalsFormAUnionWithoutChangingSourceOwnership()
    {
        using var f = new RecordSharingTests.Fixture();
        await Approve(f);
        var older = Grant(f); older.RecordFromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-90));
        older.RecordToDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-70));
        older.Decide(true, f.PatientUserId, DateTime.UtcNow);
        f.Context.Add(older); await f.Context.SaveChangesAsync();
        var access = Access(f, FhirOperations.RecordsRead);
        var rows = (await Service(f, access).GetRecords(f.Patient.Id)).Data!;
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal(f.SourceOrganization.Id, row.OrganizationId));
        access.Authenticate("instance-b", f.ReceivingOrganization.Id, f.SourceOrganization.Id, [FhirOperations.RecordsRead]);
        var ownRows = (await Service(f, access).GetRecords(f.Patient.Id)).Data!;
        Assert.Equal(f.Own.Id, Assert.Single(ownRows).Id);
    }

    [Fact]
    public async Task MissingOperationPermissionsFailBeforeAnyRepositoryIsUsed()
    {
        var access = new FhirPartnerAccess();
        IFhirSourceService service = new FhirSourceService(access, null!, null!, null!, null!, null!, null!);
        Assert.Equal(403, (await service.GetPatient(Guid.NewGuid())).Error!.HttpStatus);
        access.Authenticate("instance-b", Guid.NewGuid(), Guid.NewGuid(), [FhirOperations.SharingRequest]);
        Assert.Equal(403, (await service.SearchPatients(new(Name: "Mary"))).Error!.HttpStatus);
        Assert.Equal(403, (await service.GetRecords(Guid.NewGuid())).Error!.HttpStatus);
        Assert.Equal(403, (await service.GetRecord(Guid.NewGuid())).Error!.HttpStatus);
        Assert.Equal(403, (await service.GetPrescription(Guid.NewGuid())).Error!.HttpStatus);
        Assert.Equal(403, (await service.GetOrganization(Guid.NewGuid())).Error!.HttpStatus);
        Assert.Equal(403, (await service.GetPractitionerRole(Guid.NewGuid())).Error!.HttpStatus);
        Assert.Equal(403, (await service.GetPractitioner(Guid.NewGuid())).Error!.HttpStatus);
    }

    [Fact]
    public void ApprovalExpiryIsExclusiveAndRecordDateBoundsAreInclusive()
    {
        var now = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
        var record = new ClinicalRecord { PatientId = Guid.NewGuid(), CreatedDate = now, VisitDate = now, IsPatientVisible = true };
        var request = new RecordSharingRequest { PatientId = record.PatientId, RecordFromDate = new(2026, 11, 21), RecordToDate = new(2026, 11, 21), ExpiresAt = now.AddHours(1) };
        request.Decide(true, "patient", now);
        Assert.True(request.AllowsRecord(record, now));
        Assert.False(request.IsApprovedAt(request.ExpiresAt));
        Assert.False(request.IsApprovedAt(now.AddSeconds(-1)));
    }

    private static FhirPartnerAccess Access(RecordSharingTests.Fixture f, params string[] operations)
    {
        var access = new FhirPartnerAccess();
        access.Authenticate("instance-b", f.SourceOrganization.Id, f.ReceivingOrganization.Id, operations);
        return access;
    }

    private static IFhirSourceService Service(RecordSharingTests.Fixture f, FhirPartnerAccess access) => new FhirSourceService(access,
        new PatientRepository(f.Context), new ClinicalRecordRepository(f.Context), new RecordSharingRepository(f.Context),
        new OrganizationRepository(f.Context), new PractitionerRoleRepository(f.Context), new PractitionerRepository(f.Context));

    private static RecordSharingRequest Grant(RecordSharingTests.Fixture f) => new()
    {
        PatientId = f.Patient.Id, ReceivingPartnerId = "instance-b", RequesterName = "Remote nurse", RequesterRole = "Nurse",
        RequesterContact = "nurse@example.test", Purpose = "Continuing care", ExpiresAt = DateTime.UtcNow.AddDays(1),
        RecordFromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)), RecordToDate = DateOnly.FromDateTime(DateTime.UtcNow)
    };

    private static async Task<RecordSharingRequest> Approve(RecordSharingTests.Fixture f)
    {
        var request = Grant(f); request.Decide(true, f.PatientUserId, DateTime.UtcNow);
        f.Context.Add(request); await f.Context.SaveChangesAsync(); return request;
    }
}
