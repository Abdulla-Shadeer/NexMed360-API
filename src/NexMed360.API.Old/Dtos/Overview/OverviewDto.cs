using System.Text.Json.Serialization;

namespace NexMed360Api.Dtos.Overview;

[JsonDerivedType(typeof(DoctorOverviewDto))]
[JsonDerivedType(typeof(HospitalOverviewDto))]
[JsonDerivedType(typeof(PatientOverviewDto))]
[JsonDerivedType(typeof(SystemOverviewDto))]
public abstract class OverviewDto {
    public abstract string Name { get; }
}
