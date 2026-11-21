namespace NexMed360.Application.Practitioners;

public interface IPractitionerRepository
{
    Task<PractitionerProfileDto?> FindProfile(Guid id);
}

public sealed record PractitionerProfileDto(Guid Id, string FirstName, string LastName, string? Email,
    string? PhoneNumber, string? RegistrationNumber, string? Qualifications);
