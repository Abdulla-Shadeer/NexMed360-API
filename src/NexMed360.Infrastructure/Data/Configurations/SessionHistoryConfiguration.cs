using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data.Configurations;

internal sealed class SessionHistoryConfiguration : IEntityTypeConfiguration<SessionHistory>
{
    public void Configure(EntityTypeBuilder<SessionHistory> builder)
    {
        EntityConfiguration.Configure(builder, "SessionHistories");

    }
}
