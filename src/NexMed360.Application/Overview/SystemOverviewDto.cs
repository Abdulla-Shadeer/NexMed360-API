namespace NexMed360Api.Dtos.Overview;

public class SystemOverviewDto : OverviewDto
{
    public override string Name { get; } = "System";
    public int TotalPatients { get; set; } = 0;
    public int TotalOrganizations { get; set; } = 0;
    public int TotalMedicalStaff { get; set; } = 0;
    public int TotalUsers { get; set; } = 0;
    public int TotalEHRs { get; set; } = 0;
}
