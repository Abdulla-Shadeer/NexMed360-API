using System.Reflection;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.API.Old.Tests.Compatibility;

public class AggregateBoundaryTests
{
    [Fact]
    public void DomainNavigationsStayInsideTheirAggregate()
    {
        var owners = new Dictionary<Type, Type>
        {
            [typeof(Appointment)] = typeof(Appointment), [typeof(CancellationRequest)] = typeof(Appointment),
            [typeof(ClinicRecord)] = typeof(ClinicRecord), [typeof(Prescription)] = typeof(ClinicRecord),
            [typeof(DoctorSession)] = typeof(DoctorSession), [typeof(SessionSlot)] = typeof(DoctorSession), [typeof(SessionHistory)] = typeof(DoctorSession),
            [typeof(Patient)] = typeof(Patient), [typeof(VisitHistory)] = typeof(Patient),
            [typeof(Doctor)] = typeof(Doctor), [typeof(Hospital)] = typeof(Hospital),
            [typeof(HospitalAdmin)] = typeof(HospitalAdmin), [typeof(Receptionist)] = typeof(Receptionist)
        };
        foreach (var (model, owner) in owners)
            foreach (var property in model.GetProperties())
            {
                var related = property.PropertyType.IsGenericType ? property.PropertyType.GetGenericArguments().SingleOrDefault() : property.PropertyType;
                if (related != null && owners.TryGetValue(related, out var relatedOwner)) Assert.Equal(owner, relatedOwner);
            }
        Assert.Null(typeof(Hospital).GetProperty("Doctors"));
        Assert.Null(typeof(Doctor).GetProperty("Hospital"));
        Assert.Equal(typeof(Guid), typeof(Doctor).GetProperty("HospitalId")!.PropertyType);
    }

    [Fact]
    public void RepositoryContractsContainNoTransportDtosOrPersistenceTypes()
    {
        foreach (var repository in new[] { typeof(IAppointmentRepository), typeof(IPatientRepository), typeof(IHospitalRepository), typeof(IDoctorSessionRepository), typeof(IDoctorRepository), typeof(IHospitalAdminRepository), typeof(IReceptionistRepository), typeof(IClinicRecordRepository) })
            foreach (var method in repository.GetMethods())
            {
                Check(method.ReturnType);
                foreach (var parameter in method.GetParameters()) Check(parameter.ParameterType);
            }
    }
    private static void Check(Type type)
    {
        Assert.DoesNotContain(".Dtos.", type.Namespace ?? "");
        Assert.False((type.Namespace ?? "").StartsWith("Microsoft.EntityFrameworkCore"));
        Assert.False((type.Namespace ?? "").StartsWith("Microsoft.AspNetCore.Identity"));
        Assert.NotEqual(typeof(IQueryable<>), type.IsGenericType ? type.GetGenericTypeDefinition() : type);
        foreach (var argument in type.GetGenericArguments()) Check(argument);
    }
}
