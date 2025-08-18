using FinalProjectApi.Data;

namespace FinalProjectApi.Common;

public static class AppointmentHelper
{
    public static bool IsTimeSlotAvailable
        (
        ApplicationDbContext dbContext,
        Guid personId,
        DateTime startTime,
        bool isDoctor
        )
    {
        var defaultDuration = TimeSpan.FromMinutes(30);
        var endTime = startTime.Add(defaultDuration);

        return !dbContext.Appointments
            .Where(ap => isDoctor ? ap.DoctorId == personId : ap.PatientId == personId)
            .Select(ap => new
            {
                ap.AppointmentDateTime
            })
            .AsEnumerable()
            .Any(ap => ap.AppointmentDateTime < endTime
                && ap.AppointmentDateTime.Add(defaultDuration) > startTime);
    }
}
