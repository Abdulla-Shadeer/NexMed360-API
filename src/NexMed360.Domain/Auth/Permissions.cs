namespace NexMed360Api.Common.Auth;

public static class Permissions
{
    public const string OrganizationsManage = "organizations.manage";
    public const string MembersManage = "members.manage";
    public const string RolesManage = "roles.manage";
    public const string PatientsRead = "patients.read";
    public const string PatientsRegister = "patients.register";
    public const string AppointmentsManage = "appointments.manage";
    public const string SchedulesManage = "schedules.manage";
    public const string EncountersStart = "encounters.start";
    public const string ClinicalRecordsRead = "clinical-records.read";
    public const string ClinicalRecordsWrite = "clinical-records.write";
    public const string PrescriptionsIssue = "prescriptions.issue";

    public static readonly IReadOnlyList<string> Administration = Array.AsReadOnly(new[]
    {
        OrganizationsManage, MembersManage, RolesManage
    });

    public static readonly IReadOnlyList<string> All = Array.AsReadOnly(new[]
    {
        OrganizationsManage, MembersManage, RolesManage, PatientsRead, PatientsRegister,
        AppointmentsManage, SchedulesManage, EncountersStart, ClinicalRecordsRead,
        ClinicalRecordsWrite, PrescriptionsIssue
    });
}
