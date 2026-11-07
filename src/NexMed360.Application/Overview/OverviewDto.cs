using System.Text.Json.Serialization;

namespace NexMed360Api.Dtos.Overview;

[JsonDerivedType(typeof(PractitionerRoleOverviewDto))]
[JsonDerivedType(typeof(OrganizationOverviewDto))]
[JsonDerivedType(typeof(PatientOverviewDto))]
[JsonDerivedType(typeof(SystemOverviewDto))]
public abstract class OverviewDto {
    public abstract string Name { get; }
}
