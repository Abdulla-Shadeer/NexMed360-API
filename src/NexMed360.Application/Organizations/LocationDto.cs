namespace NexMed360.Application.Organizations;

public sealed record SaveLocationDto(string Name, string Type, Guid? ParentId, string? Address, bool IsActive);
public sealed record ManagedLocationDto(Guid Id, string Name, string Type, Guid? ParentId, string? Address, bool IsActive);

