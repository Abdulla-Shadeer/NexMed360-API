using Microsoft.EntityFrameworkCore.Storage;
using NexMed360Api.Data;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.Infrastructure.Data;

public sealed class RegistrationTransactions(ApplicationDbContext context) : IRegistrationTransactions
{
    public async Task<IRegistrationTransaction> BeginAsync()
        => new RegistrationTransaction(await context.Database.BeginTransactionAsync());

    private sealed class RegistrationTransaction(IDbContextTransaction transaction) : IRegistrationTransaction
    {
        public Task CommitAsync() => transaction.CommitAsync();
        // An uncommitted transaction rolls back when disposed, including on early return.
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
