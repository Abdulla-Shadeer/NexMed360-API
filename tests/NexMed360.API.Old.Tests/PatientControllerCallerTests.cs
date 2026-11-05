using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;
using NexMed360Api.Controllers.v1;
using NexMed360Api.Dtos.ClinicRecord;
using NexMed360Api.Dtos.Patient;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.API.Old.Tests;

public class PatientControllerCallerTests
{
    [Theory]
    [InlineData(UserRoles.PATIENT, false)]
    [InlineData(UserRoles.DOCTOR, true)]
    [InlineData("Patient,Doctor", true)]
    [InlineData("Patient,HospitalAdmin", false)]
    public async Task ReadActionsPassAuthenticatedIdentityAndDoctorRole(string roles, bool expectedIsDoctor)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "signed-in-user") };
        claims.AddRange(roles.Split(',').Select(role => new Claim(ClaimTypes.Role, role)));
        var service = new RecordingPatientService();
        var controller = Controller(service, claims);
        var requestedId = Guid.NewGuid().ToString();

        await controller.GetClinicRecord(requestedId);

        Assert.Equal(requestedId, service.RequestedId);
        Assert.Equal("signed-in-user", service.RequesterUserId);
        Assert.Equal(expectedIsDoctor, service.IsDoctor);

        await controller.GetClinicRecords(requestedId);

        Assert.Equal(requestedId, service.RequestedId);
        Assert.Equal("signed-in-user", service.RequesterUserId);
        Assert.Equal(expectedIsDoctor, service.IsDoctor);
    }

    [Fact]
    public async Task ReadActionsDoNotInventMissingCallerIdentity()
    {
        var service = new RecordingPatientService();
        var controller = Controller(service, [new Claim(ClaimTypes.Role, UserRoles.DOCTOR)]);

        await controller.GetClinicRecord(Guid.NewGuid().ToString());
        Assert.Null(service.RequesterUserId);

        await controller.GetClinicRecords(Guid.NewGuid().ToString());
        Assert.Null(service.RequesterUserId);
    }

    [Theory]
    [InlineData(nameof(PatientController.GetClinicRecord))]
    [InlineData(nameof(PatientController.GetClinicRecords))]
    public void ReadActionsKeepPatientAndDoctorAuthorization(string action)
    {
        var method = typeof(PatientController).GetMethod(action)!;
        var authorization = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
        var roles = authorization.Roles!.Split(',').Select(role => role.Trim()).Order().ToArray();

        Assert.Equal(new[] { UserRoles.DOCTOR, UserRoles.PATIENT }, roles);
    }

    private static PatientController Controller(RecordingPatientService service, IEnumerable<Claim> claims) => new(service)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
            }
        }
    };

    private sealed class RecordingPatientService : IPatientService
    {
        public string? RequestedId { get; private set; }
        public string? RequesterUserId { get; private set; }
        public bool IsDoctor { get; private set; }

        public Task<Result<ViewClinicRecordDto>> ViewClinicRecord(string id, string? requesterUserId, bool isDoctor)
        {
            (RequestedId, RequesterUserId, IsDoctor) = (id, requesterUserId, isDoctor);
            return Task.FromResult(Result<ViewClinicRecordDto>.Success(null));
        }

        public Task<Result<List<ViewClinicRecordDto>>> ViewAllClinicRecords(string patientId, string? requesterUserId, bool isDoctor)
        {
            (RequestedId, RequesterUserId, IsDoctor) = (patientId, requesterUserId, isDoctor);
            return Task.FromResult(Result<List<ViewClinicRecordDto>>.Success([]));
        }

        public Task<Result<ViewPatientDto>> CreatePatient(CreatePatientDto dto) => throw new NotSupportedException();
        public Task<Result<ViewPatientDto>> GetPatient(string id) => throw new NotSupportedException();
        public Task<Result<ViewClinicRecordDto>> AddToClinicRecords(AddClinicRecordDto dto, string doctorUserId) => throw new NotSupportedException();
    }
}
