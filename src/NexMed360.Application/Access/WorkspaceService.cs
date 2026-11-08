namespace NexMed360.Application.Access;

public sealed class WorkspaceService(IWorkspaceRepository repository)
{
    public Task<List<WorkspaceOptionDto>> List(string userId) => repository.List(userId);
    public Task<WorkspaceScope?> Resolve(string userId, string key) => repository.Resolve(userId, key);
}
