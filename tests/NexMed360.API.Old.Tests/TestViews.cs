using NexMed360.Application.Shared;
using NexMed360.Infrastructure.Data;
using NexMed360Api.Data;

namespace NexMed360.API.Old.Tests;
internal static class TestViews
{
    internal static PortalViews For(ApplicationDbContext context) => new(new PortalReadStore(context));
    internal static PortalViews Empty() => new(new EmptyReadStore());
    private sealed class EmptyReadStore : IPortalReadStore
    {
        public Task<PortalReadData> Load(PortalReadRequest request) => Task.FromResult(new PortalReadData());
    }
}
