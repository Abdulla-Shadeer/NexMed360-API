using NexMed360Api.Entities.Enums;

namespace NexMed360Api.Dtos.Appointment;

public class ViewAppointmentDto
{
    public string BookingReference { get; set; }
    public string PatientId { get; set; }
    public string PatientName { get; set; }
    public string PatientEmail { get; set; }
    public string PatientPhone { get; set; }
    public string PractitionerRoleId { get; set; }
    public string PractitionerName { get; set; }
    public string PractitionerSpecialty { get; set; }
    public string OrganizationId { get; set; }
    public string OrganizationName { get; set; }
    public string OrganizationAddress { get; set; }
    public string OrganizationPhone { get; set; }
    public string CreatedByUserId { get; set; }
    public string CreatedByUserName { get; set; }
    public string AppointmentDateTime { get; set; }
    public int AppointmentNumber { get; set; }
    public string? Remarks { get; set; }
    public AppointmentStatus Status { get; set; }
}