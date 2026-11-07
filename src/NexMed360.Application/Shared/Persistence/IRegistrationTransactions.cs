namespace NexMed360Api.Interfaces.Services;

// One local transaction covers the Identity account and its role-specific profile.
public interface IRegistrationTransactions
{
    Task<IRegistrationTransaction> BeginAsync();
}

public interface IRegistrationTransaction : IAsyncDisposable
{
    Task CommitAsync();
}
