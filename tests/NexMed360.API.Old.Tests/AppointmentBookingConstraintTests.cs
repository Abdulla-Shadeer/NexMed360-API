using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using Npgsql;

namespace NexMed360.API.Old.Tests;

public class AppointmentBookingConstraintTests
{
    [Fact]
    public void ModelAndSnapshotContainTheAdditionalFilteredUniqueIndex()
    {
        // Match AddApplicationDbContext before constructing the Npgsql model.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        // Building/comparing models does not connect to this deliberately unused target.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=model_only;Username=unused;Password=unused")
            .Options;
        using var context = new ApplicationDbContext(options);
        var model = context.GetService<IDesignTimeModel>().Model;
        var indexes = model.FindEntityType(typeof(Appointment))!.GetIndexes().ToArray();
        var unique = Assert.Single(indexes.Where(index =>
            index.GetDatabaseName() == ApplicationDbContext.NonCancelledSlotIndexName));

        Assert.True(unique.IsUnique);
        Assert.Equal("\"Status\" <> 5", unique.GetFilter());
        Assert.Equal(nameof(Appointment.SessionSlotId), Assert.Single(unique.Properties).Name);
        Assert.Contains(indexes, index => index.GetDatabaseName() == "IX_Appointments_SessionSlotId_OrganizationId"
            && !index.IsUnique && index.GetFilter() == null);
        var requester = model.FindEntityType(typeof(CancellationRequest))!.FindProperty(nameof(CancellationRequest.RequestedById))!;
        Assert.Equal(typeof(string), requester.ClrType);
        Assert.Equal("text", requester.GetColumnType());
        Assert.False(requester.IsNullable);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task OnlyTheBookingIndexViolationBecomesAConflictAndClearsTheFailedInsert()
    {
        var exception = DatabaseFailure(PostgresErrorCodes.UniqueViolation,
            ApplicationDbContext.NonCancelledSlotIndexName);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new FailAppointmentSave(exception)).Options;
        using var fixture = new AppointmentBookingEligibilityTests.Fixture(options);

        var result = await fixture.Service.Create(fixture.Request(), fixture.RequesterId);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.Error!.HttpStatus);
        Assert.Equal(ErrorCode.APPT_PractitionerRoleUnavailable, result.Error.Code);
        Assert.Null(result.Data);
        Assert.DoesNotContain(fixture.Context.ChangeTracker.Entries<Appointment>(),
            entry => entry.State == EntityState.Added);
        Assert.Empty(await fixture.Context.Appointments.ToListAsync());
        Assert.True((await fixture.Context.SessionSlots.SingleAsync()).IsAvailable);
    }

    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation, "another_unique_index")]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation, ApplicationDbContext.NonCancelledSlotIndexName)]
    public async Task OtherDatabaseFailuresAreNotMisreportedAsBookingConflicts(string sqlState, string constraint)
    {
        var exception = DatabaseFailure(sqlState, constraint);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new FailAppointmentSave(exception)).Options;
        using var fixture = new AppointmentBookingEligibilityTests.Fixture(options);

        var actual = await Assert.ThrowsAsync<DbUpdateException>(() =>
            fixture.Service.Create(fixture.Request(), fixture.RequesterId));

        Assert.Same(exception, actual);
    }

    private static DbUpdateException DatabaseFailure(string sqlState, string constraint) => new(
        "Synthetic database failure", new PostgresException(
            "Synthetic constraint failure", "ERROR", "ERROR", sqlState, constraintName: constraint));

    private sealed class FailAppointmentSave(DbUpdateException exception) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Appointment>()
                .Any(entry => entry.State == EntityState.Added))
                throw exception;
            return ValueTask.FromResult(result);
        }
    }
}
