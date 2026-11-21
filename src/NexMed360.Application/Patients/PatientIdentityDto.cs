namespace NexMed360.Application.Patients;

public sealed record PatientIdentityDto(Guid Id, string FirstName, string LastName, DateOnly BirthDate, string Gender, string? Email);
public sealed record PatientIdentityFilter(string? Name = null, string? Email = null, Guid? PatientId = null, int Offset = 0, int Count = 30);
