using NexMed360.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Common.Auth;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Services;
using BookingFixture = NexMed360.API.Old.Tests.AppointmentBookingEligibilityTests.Fixture;

namespace NexMed360.API.Old.Tests;

public class AppointmentReadAccessTests
{
    [Theory]
    [InlineData("patient")]
    [InlineData(Permissions.EncountersStart)]
    public async Task PatientAndAssignedPractitionerRoleCanReadTheAppointment(string role)
    {
        using var fixture = new BookingFixture();
        var id = await Book(fixture);
        var caller = role == "patient"
            ? TestProfiles.UserId(await fixture.Context.Patients.SingleAsync())
            : TestProfiles.UserId(await fixture.Context.PractitionerRoles.SingleAsync());
        fixture.Context.ChangeTracker.Clear();

        var result = await fixture.Service.Get(id, caller, [role]);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(id, result.Data.BookingReference);
        Assert.Equal(fixture.PatientId.ToString(), result.Data.PatientId);
        Assert.Equal("Synthetic Patient", result.Data.PatientName);
        Assert.Equal("Synthetic PractitionerRole", result.Data.PractitionerName);
        Assert.Equal("Synthetic organization", result.Data.OrganizationName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OrganizationMemberAccessIsLimitedToTheAppointmentsOrganization(bool sameOrganization)
    {
        using var fixture = new BookingFixture();
        var id = await Book(fixture);
        var caller = await AddOrganizationMember(fixture, sameOrganization);

        var result = await fixture.Service.Get(id, caller, [Permissions.AppointmentsManage]);

        Assert.Equal(sameOrganization, result.IsSuccess);
        if (sameOrganization)
            Assert.Equal(id, result.Data!.BookingReference);
        else
        {
            Assert.Null(result.Data);
            Assert.Equal(404, result.Error!.HttpStatus);
        }
    }

    [Fact]
    public async Task AnotherPractitionerRoleAtTheSameOrganizationCannotReadAnUnassignedAppointment()
    {
        using var fixture = new BookingFixture();
        var id = await Book(fixture);
        var organization = await fixture.Context.Organizations.SingleAsync();
        var user = new ApplicationUser { Id = "other-practitionerRole", FirstName = "Other", LastName = "PractitionerRole" };
        fixture.Context.Users.Add(user);
        if (fixture.Context.Entry(organization).State == EntityState.Detached) fixture.Context.Organizations.Add(organization);
        fixture.Context.PractitionerRoles.Add(new PractitionerRole
        {
            Id = Guid.NewGuid(), PractitionerId = TestProfiles.PractitionerId(user.Id),
            OrganizationId = organization.Id,  Specialty = "Test specialty"
        });
        await TestProfiles.SaveAsync(fixture.Context);
        fixture.Context.ChangeTracker.Clear();

        var result = await fixture.Service.Get(id, user.Id, [Permissions.EncountersStart]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(404, result.Error!.HttpStatus);
    }

    [Fact]
    public async Task UnrelatedPatientOrCreatorCannotReadAndGetsTheMissingTargetResponse()
    {
        using var fixture = new BookingFixture();
        var id = await Book(fixture);
        var unrelated = new Patient
        {
            Id = Guid.NewGuid(), PersonId = TestProfiles.PersonId(fixture.RequesterId), Gender = "Test",
            BirthDate = new DateOnly(2000, 1, 1)
        };
        fixture.Context.Patients.Add(unrelated);
        await TestProfiles.SaveAsync(fixture.Context);
        fixture.Context.ChangeTracker.Clear();

        // This caller created the booking but is not its patient, practitionerRole or organizationMember.
        var denied = await fixture.Service.Get(id, fixture.RequesterId, ["patient"]);
        var missing = await fixture.Service.Get(Guid.NewGuid().ToString(), fixture.RequesterId, ["patient"]);

        Assert.False(denied.IsSuccess);
        Assert.Null(denied.Data);
        Assert.Equal(404, denied.Error!.HttpStatus);
        Assert.Equal(missing.Error!.Code, denied.Error.Code);
        Assert.Equal(missing.Error.Title, denied.Error.Title);
    }

    [Theory]
    [InlineData("patient")]
    [InlineData(UserRoles.PLATFORM_ADMIN)]
    public async Task OrganizationMemberProfileWithoutTheOrganizationMemberTokenRoleDoesNotGrantOrganizationAccess(string tokenRole)
    {
        using var fixture = new BookingFixture();
        var id = await Book(fixture);
        var caller = await AddOrganizationMember(fixture, true);

        var result = await fixture.Service.Get(id, caller, [tokenRole]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(404, result.Error!.HttpStatus);
    }

    [Fact]
    public async Task PractitionerRoleProfileWithoutThePractitionerRoleTokenRoleDoesNotGrantAssignedPractitionerRoleAccess()
    {
        using var fixture = new BookingFixture();
        var id = await Book(fixture);
        var caller = TestProfiles.UserId(await fixture.Context.PractitionerRoles.SingleAsync());

        var result = await fixture.Service.Get(id, caller, ["patient"]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(404, result.Error!.HttpStatus);
    }

    [Fact]
    public async Task MultipleRolesAllowAnExistingOrganizationMemberScope()
    {
        using var fixture = new BookingFixture();
        var id = await Book(fixture);
        var caller = await AddOrganizationMember(fixture, true);

        var result = await fixture.Service.Get(id, caller, ["patient", Permissions.AppointmentsManage]);

        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Data!.BookingReference);
    }

    [Fact]
    public async Task CallerIdsAreComparedAsExactStringsAndEmptyRolesGrantNoAccess()
    {
        using var fixture = new BookingFixture();
        var id = await Book(fixture);
        var caller = TestProfiles.UserId(await fixture.Context.Patients.SingleAsync());

        var wrongCase = await fixture.Service.Get(id, caller.ToUpperInvariant(), ["patient"]);
        var noRoles = await fixture.Service.Get(id, caller, []);

        Assert.Equal(404, wrongCase.Error!.HttpStatus);
        Assert.Null(wrongCase.Data);
        Assert.Equal(404, noRoles.Error!.HttpStatus);
        Assert.Null(noRoles.Data);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task MissingIdentityIsRejectedBeforeStorageAccess(string? caller)
    {
        using var context = new ApplicationDbContext(new DbContextOptions<ApplicationDbContext>());
        var service = new AppointmentService(new AppointmentRepository(context));

        var result = await service.Get(Guid.NewGuid().ToString(), caller, [Permissions.EncountersStart]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(401, result.Error!.HttpStatus);
    }

    [Fact]
    public async Task MalformedIdKeepsTheExistingBadRequestResponseBeforeStorageAccess()
    {
        using var context = new ApplicationDbContext(new DbContextOptions<ApplicationDbContext>());
        var service = new AppointmentService(new AppointmentRepository(context));

        var result = await service.Get("invalid", "signed-in-user", ["patient"]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCode.SYS_InvalidGuid, result.Error!.Code);
        Assert.Equal(400, result.Error.HttpStatus);
    }

    internal static async Task<string> Book(BookingFixture fixture)
    {
        var result = await fixture.Service.Create(fixture.Request(), fixture.RequesterId);
        Assert.True(result.IsSuccess);
        fixture.Context.ChangeTracker.Clear();
        return result.Data!.BookingReference;
    }

    internal static async Task<string> AddOrganizationMember(BookingFixture fixture, bool sameOrganization)
    {
        var organization = sameOrganization ? await fixture.Context.Organizations.SingleAsync() : new Organization
        {
            Id = Guid.NewGuid(), Name = "Other organization", Street = "Test street", City = "Test city",
            District = "Test district", Phone = "0000000000", Grade = "Test"
        };
        var user = new ApplicationUser { Id = "organizationMember-user", FirstName = "Synthetic", LastName = "OrganizationMembership" };
        fixture.Context.Users.Add(user);
        if (fixture.Context.Entry(organization).State == EntityState.Detached) fixture.Context.Organizations.Add(organization);
        fixture.Context.OrganizationMemberships.Add(new OrganizationMembership
        {
            Id = Guid.NewGuid(), UserId = user.Id, OrganizationId = organization.Id,

        });
        await TestProfiles.SaveAsync(fixture.Context);
        fixture.Context.ChangeTracker.Clear();
        return user.Id;
    }
}
