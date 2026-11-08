using NexMed360Api.Common.Auth;
using NexMed360Api.Entities.Enums;

namespace NexMed360.Domain.Rules;

public enum BookingEligibility { Allowed, InactiveSession, UnavailableSlot }

public static class AppointmentRules
{
    public static BookingEligibility Booking(bool sessionActive, bool available, bool bookingAllowed)
        => !sessionActive ? BookingEligibility.InactiveSession
            : !available || !bookingAllowed ? BookingEligibility.UnavailableSlot
            : BookingEligibility.Allowed;

    public static string[] StatusPermissions(IReadOnlyCollection<string> permissions, AppointmentStatus status)
        => permissions.Where(role => role == Permissions.EncountersStart
            || (role == Permissions.AppointmentsManage && status != AppointmentStatus.InProgress)).ToArray();
}
