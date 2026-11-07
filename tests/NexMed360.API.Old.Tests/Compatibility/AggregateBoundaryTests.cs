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
            [typeof(ClinicalRecord)] = typeof(ClinicalRecord), [typeof(Prescription)] = typeof(ClinicalRecord),
            [typeof(PractitionerSchedule)] = typeof(PractitionerSchedule), [typeof(SessionSlot)] = typeof(PractitionerSchedule), [typeof(SessionHistory)] = typeof(PractitionerSchedule),
            [typeof(Patient)] = typeof(Patient), [typeof(VisitHistory)] = typeof(Patient),
            [typeof(PractitionerRole)] = typeof(PractitionerRole), [typeof(Organization)] = typeof(Organization),
            [typeof(OrganizationMembership)] = typeof(OrganizationMembership)
        };
        foreach (var (model, owner) in owners)
            foreach (var property in model.GetProperties())
            {
                var related = property.PropertyType.IsGenericType ? property.PropertyType.GetGenericArguments().SingleOrDefault() : property.PropertyType;
                if (related != null && owners.TryGetValue(related, out var relatedOwner)) Assert.Equal(owner, relatedOwner);
            }
        Assert.Null(typeof(Organization).GetProperty("PractitionerRoles"));
        Assert.Null(typeof(PractitionerRole).GetProperty("Organization"));
        Assert.Equal(typeof(Guid), typeof(PractitionerRole).GetProperty("OrganizationId")!.PropertyType);
    }

    [Fact]
    public void RepositoryContractsContainNoTransportDtosOrPersistenceTypes()
    {
        foreach (var repository in new[] { typeof(IAppointmentRepository), typeof(IPatientRepository), typeof(IOrganizationRepository), typeof(IPractitionerScheduleRepository), typeof(IPractitionerRoleRepository), typeof(IOrganizationMembershipRepository), typeof(IOrganizationMembershipRepository), typeof(IClinicalRecordRepository) })
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
