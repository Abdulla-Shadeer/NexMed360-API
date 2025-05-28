using FinalProjectApi.Data;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectApi.Common;

public static class AppointmentHelper
{
    public static async Task<bool> IsTimeSlotAvailableAsync
        (
        ApplicationDbContext dbContext,
        Guid personId,
        DateTime startTime,
        TimeSpan duration,
        bool isDoctor
        )
    {
        var EndTime = startTime.Add(duration);

        return ! await dbContext.Appointments
            .Where(ap => isDoctor? ap.DoctorId.Equals(personId) : ap.PatientId.Equals(personId))
            .AnyAsync(ap => ap.AppointmentDateTime < EndTime
            && ap.AppointmentDateTime.Add(ap.Duration) > startTime);
    }
}
