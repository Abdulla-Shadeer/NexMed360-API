using FinalProjectApi.Data;

namespace FinalProjectApi.Common;

public static class AppointmentHelper
{
    public static bool IsTimeSlotAvailable
        (
        ApplicationDbContext dbContext,
        Guid personId,
        DateTime startTime,
        TimeSpan duration,
        bool isDoctor
        )
    {
        var endTime = startTime.Add(duration);

        return !dbContext.Appointments
            .Where(ap => isDoctor ? ap.DoctorId == personId : ap.PatientId == personId)
            .Select(ap => new
            {
                ap.AppointmentDateTime,
                ap.Duration
            })
            .AsEnumerable()
            .Any(ap => ap.AppointmentDateTime < endTime
                && ap.AppointmentDateTime.Add(ap.Duration) > startTime);
    }
}
