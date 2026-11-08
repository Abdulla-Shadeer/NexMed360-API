namespace NexMed360.Application.Access;

public interface IWorkspaceRepository
{
    Task<List<WorkspaceOptionDto>> List(string userId);
    Task<WorkspaceScope?> Resolve(string userId, string key);
}
