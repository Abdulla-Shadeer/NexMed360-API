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
            [typeof(OrganizationMember)] = typeof(OrganizationMember),
            [typeof(MemberRoleAssignment)] = typeof(OrganizationMember),
            [typeof(AccessRole)] = typeof(AccessRole), [typeof(Person)] = typeof(Person),
            [typeof(Practitioner)] = typeof(Practitioner), [typeof(Location)] = typeof(Location),
            [typeof(OrganizationPatient)] = typeof(OrganizationPatient),
            [typeof(Encounter)] = typeof(Encounter), [typeof(EncounterParticipant)] = typeof(Encounter)
        };
        foreach (var (model, owner) in owners)
            foreach (var property in model.GetProperties())
            {
                var related = property.PropertyType.IsGenericType ? property.PropertyType.GetGenericArguments().SingleOrDefault() : property.PropertyType;
                if (related != null && owners.TryGetValue(related, out var relatedOwner)) Assert.Equal(owner, relatedOwner);
                if (related != null) Check(related);
            }
        Assert.Null(typeof(Organization).GetProperty("PractitionerRoles"));
        Assert.Null(typeof(PractitionerRole).GetProperty("Organization"));
        Assert.Equal(typeof(Guid), typeof(PractitionerRole).GetProperty("OrganizationId")!.PropertyType);
    }

    [Fact]
    public void RepositoryContractsKeepPersistenceAndHttpTypesPrivate()
    {
        foreach (var repository in new[] { typeof(IAppointmentRepository), typeof(IPatientRepository), typeof(IOrganizationRepository), typeof(IPractitionerScheduleRepository), typeof(IPractitionerRoleRepository), typeof(IOrganizationMemberRepository), typeof(IClinicalRecordRepository), typeof(NexMed360.Application.Access.IWorkspaceRepository), typeof(NexMed360.Application.Access.IAccessRepository), typeof(NexMed360.Application.Accounts.IUserRepository), typeof(NexMed360.Application.Overview.IOverviewRepository), typeof(NexMed360.Application.Search.ISearchRepository) })
            foreach (var method in repository.GetMethods())
            {
                Check(method.ReturnType);
                foreach (var parameter in method.GetParameters()) Check(parameter.ParameterType);
            }
    }
    private static void Check(Type type)
    {
        Assert.False((type.Namespace ?? "").StartsWith("Microsoft.AspNetCore.Mvc"));
        Assert.False((type.Namespace ?? "").StartsWith("Microsoft.EntityFrameworkCore"));
        Assert.False((type.Namespace ?? "").StartsWith("Microsoft.AspNetCore.Identity"));
        Assert.NotEqual(typeof(IQueryable<>), type.IsGenericType ? type.GetGenericTypeDefinition() : type);
        foreach (var argument in type.GetGenericArguments()) Check(argument);
    }
}
