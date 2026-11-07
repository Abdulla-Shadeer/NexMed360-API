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

    public static string[] StatusRoles(IReadOnlyCollection<string> roles, AppointmentStatus status)
        => roles.Where(role => role == UserRoles.DOCTOR
            || (role == UserRoles.RECEPTIONIST && status != AppointmentStatus.InProgress)).ToArray();
}
