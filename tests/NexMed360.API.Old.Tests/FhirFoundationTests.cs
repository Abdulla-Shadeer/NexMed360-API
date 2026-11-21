using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NexMed360.Infrastructure.Fhir;
using NexMed360.Infrastructure.Repositories;
using NexMed360.Application.Patients;
using NexMed360Api.Entities;

namespace NexMed360.API.Old.Tests;

public class FhirFoundationTests
{
    [Theory]
    [InlineData(PrescriptionStatus.Active, true)]
    [InlineData(PrescriptionStatus.OnHold, false)]
    [InlineData(PrescriptionStatus.Cancelled, false)]
    [InlineData(PrescriptionStatus.Completed, false)]
    [InlineData(PrescriptionStatus.EnteredInError, false)]
    [InlineData(PrescriptionStatus.Stopped, false)]
    [InlineData(PrescriptionStatus.Draft, false)]
    [InlineData(PrescriptionStatus.Unknown, false)]
    public void ActiveFlagComesFromThePrescriptionStatus(PrescriptionStatus status, bool active)
    {
        Assert.Equal(active, new Prescription { Status = status }.IsActive);
        var created = new Prescription();
        Assert.Equal(PrescriptionStatus.Active, created.Status);
        Assert.Equal(PrescriptionIntent.Order, created.Intent);
    }

    [Fact]
    public async Task PrescriptionViewsKeepStatusAndIntentInsteadOfCallingEveryInactiveItemCompleted()
    {
        using var f = new RecordSharingTests.Fixture();
        var prescription = await f.Context.Prescriptions.SingleAsync();
        prescription.Status = PrescriptionStatus.Unknown;
        await f.Context.SaveChangesAsync();
        f.PatientMode();
        var record = await f.Records.FindReadable(f.Source.Id, f.PatientUserId, false);
        var view = (await f.Records.View(record))!;
        var item = Assert.Single(view.Prescriptions!);
        Assert.Equal("Unknown", item.Status);
        Assert.Equal("Order", item.Intent);
        Assert.False(item.IsActive);
    }

    [Fact]
    public async Task PatientInboxIncludesExternalRequestSnapshotsWithoutTreatingRemoteIdsAsLocalRoles()
    {
        using var f = new RecordSharingTests.Fixture();
        var local = new RecordSharingRequest { PatientId = f.Patient.Id, ReceivingOrganizationId = f.ReceivingOrganization.Id,
            PractitionerRoleId = f.ReceivingRole.Id, Purpose = "Local care", ExpiresAt = DateTime.UtcNow.AddDays(1) };
        var external = new RecordSharingRequest { PatientId = f.Patient.Id, ReceivingPartnerId = "instance-b",
            RequesterName = "Remote nurse", RequesterRole = "Nurse", RequesterContact = "nurse@example.test",
            Purpose = "Continuing care", ExpiresAt = DateTime.UtcNow.AddDays(1) };
        var other = new RecordSharingRequest { PatientId = f.Other.Id, ReceivingPartnerId = "instance-b",
            RequesterName = "Remote nurse", RequesterRole = "Nurse", RequesterContact = "nurse@example.test",
            Purpose = "Other care", ExpiresAt = DateTime.UtcNow.AddDays(1) };
        f.Context.AddRange(local, external, other);
        await f.Context.SaveChangesAsync();
        var settings = new Settings(new FhirOptions { Partners = [new() { Id = "instance-b", Name = "Peer hospital", Contact = "peer@example.test" }] });
        var repository = new RecordSharingRepository(f.Context, settings: settings);
        f.PatientMode();
        var inbox = await repository.ForPatient(f.Patient.Id);
        Assert.Equal(2, inbox.Count);
        var request = Assert.Single(inbox.Where(row => row.ReceivingPartnerId != null));
        Assert.Null(request.ReceivingOrganizationId);
        Assert.Equal("Peer hospital", request.OrganizationName);
        Assert.Equal("Remote nurse", request.PractitionerName);
        Assert.Equal("Nurse", request.PractitionerRole);
        Assert.Equal("nurse@example.test", request.PractitionerEmail);
        var service = new RecordSharingService(repository, new PatientRepository(f.Context), new PractitionerRoleRepository(f.Context), f.Access);
        Assert.True((await service.Decide(external.Id, true, f.PatientUserId)).IsSuccess);
        Assert.True((await service.Revoke(external.Id, f.PatientUserId)).IsSuccess);
        f.Work();
        var organizationInbox = await repository.ForOrganization(f.ReceivingOrganization.Id, f.Patient.Id);
        Assert.Equal(local.Id, Assert.Single(organizationInbox).Id);
    }

    [Fact]
    public void DisabledFhirNeedsNoPartnerCredentialsAndValidSettingsCanBeBound()
    {
        var validator = new FhirOptionsValidator();
        Assert.True(validator.Validate(null, new FhirOptions()).Succeeded);
        Assert.True(validator.Validate(null, ValidSettings()).Succeeded);
    }

    [Theory]
    [InlineData("public-url")]
    [InlineData("peer-url")]
    [InlineData("namespace")]
    [InlineData("organization")]
    [InlineData("duplicate")]
    [InlineData("operations")]
    [InlineData("key-hash")]
    [InlineData("duplicate-key")]
    [InlineData("outgoing-reference")]
    public void EnabledFhirRejectsIncompleteOrAmbiguousPartnerSettings(string error)
    {
        var settings = ValidSettings();
        var partner = settings.Partners[0];
        if (error == "public-url") settings.PublicBaseUrl = "http://localhost:7101/fhir";
        if (error == "peer-url") partner.BaseUrl = "https://user:password@localhost:7102";
        if (error == "namespace") settings.IdentifierSystem = "not a URI";
        if (error == "organization") partner.RemoteOrganizationId = Guid.Empty;
        if (error == "duplicate") settings.Partners.Add(new() { Id = partner.Id, Enabled = false });
        if (error == "duplicate-key")
        {
            settings.Partners.Add(new() { Id = "instance-c", Name = partner.Name, Contact = partner.Contact,
                BaseUrl = partner.BaseUrl, LocalOrganizationId = partner.LocalOrganizationId, RemoteOrganizationId = partner.RemoteOrganizationId,
                AllowedOperations = partner.AllowedOperations, IncomingApiKeyHash = partner.IncomingApiKeyHash,
                OutgoingApiKeySecretName = "Fhir:Secrets:InstanceC:OutgoingApiKey" });
        }
        if (error == "operations") partner.AllowedOperations = ["everything"];
        if (error == "key-hash") partner.IncomingApiKeyHash = "not a hash";
        if (error == "outgoing-reference") partner.OutgoingApiKeySecretName = "";
        var result = new FhirOptionsValidator().Validate(null, settings);
        Assert.True(result.Failed);
        Assert.DoesNotContain("user:password", result.FailureMessage);
    }

    private static FhirOptions ValidSettings() => new()
    {
        Enabled = true, PublicBaseUrl = "https://localhost:7101/fhir", IdentifierSystem = "urn:nexmed360:instance-a:patients",
        Partners = [new() { Id = "instance-b", Name = "Peer hospital", Contact = "peer@example.test", BaseUrl = "https://localhost:7102",
            LocalOrganizationId = Guid.NewGuid(), RemoteOrganizationId = Guid.NewGuid(), AllowedOperations = ["patients.search", "records.read", "sharing.request"],
            IncomingApiKeyHash = new string('a', 64), OutgoingApiKeySecretName = "Fhir:Secrets:InstanceB:OutgoingApiKey" }]
    };

    private sealed class Settings(FhirOptions value) : IOptionsSnapshot<FhirOptions>
    {
        public FhirOptions Value => value;
        public FhirOptions Get(string? name) => value;
    }
}
