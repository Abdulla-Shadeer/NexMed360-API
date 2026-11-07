namespace NexMed360Api.Entities;

public class Doctor: Entity
{
    public string Speciality { get; set; }

    public Guid HospitalId { get; set; }

    public string UserId { get; set; }

}
