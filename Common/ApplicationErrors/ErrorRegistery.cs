namespace FinalProjectApi.Common.ApplicationErrors;

public static class ErrorRegistery
{
    private static readonly Dictionary<ErrorCode, ApiError> _errorRegistry = new()
    {
        { ErrorCode.APPT_NotFound, new ApiError(ErrorCode.APPT_NotFound, "No appointments found!", 404) },
        { ErrorCode.APPT_PatientUnavailable, new ApiError(ErrorCode.APPT_PatientUnavailable, "Patient is not available at the requested time!", 409) },
        { ErrorCode.APPT_DoctorUnavailable, new ApiError(ErrorCode.APPT_DoctorUnavailable, "Doctor is not available at the requested time!", 409) },
        { ErrorCode.SYS_InvalidGuid, new ApiError(ErrorCode.SYS_InvalidGuid, "An invalid GUID format was received!", 400) },
        { ErrorCode.APPT_MinimumFilterRequirement, new ApiError(ErrorCode.APPT_MinimumFilterRequirement,
            "At least one person ID (Patient ID or Doctor ID) must be provided to filter appointments.", 400)
        },
        { ErrorCode.APPT_FromDateAfterToDate, new ApiError(ErrorCode.APPT_FromDateAfterToDate, "From date cannot be after To date!", 400) },
        { ErrorCode.Internal, new ApiError(ErrorCode.Internal, "An internal error occurred. Please try again later.", 500) },
        { ErrorCode.APPT_DoctorNotFound, new ApiError(ErrorCode.APPT_DoctorNotFound, "No doctor found with the given ID!", 404) },
        { ErrorCode.APPT_PatientNotFound, new ApiError(ErrorCode.APPT_PatientNotFound, "No patient found with the given ID!", 404) },
        { ErrorCode.USR_NotFound, new ApiError(ErrorCode.USR_NotFound, "User not found!", 404) },
        { ErrorCode.APPT_Cancelled, new ApiError(ErrorCode.APPT_Cancelled, "The appointment has been cancelled and is no longer accessible.", 410) },
        { ErrorCode.APPT_CancellationAlreadyRequested, new ApiError(ErrorCode.APPT_CancellationAlreadyRequested, "Cancellation request for this appointment has already been made.", 409) },
        { ErrorCode.USR_RoleNotFound, new ApiError(ErrorCode.USR_RoleNotFound, "User role not found!", 404) },
        { ErrorCode.HOS_NotFound, new ApiError(ErrorCode.HOS_NotFound, "Hospital not found!", 404) },
        { ErrorCode.USR_RegistrationFailed, new ApiError(ErrorCode.USR_RegistrationFailed, "User registration failed!", 500) },
        { ErrorCode.CLNRD_NotFound, new ApiError(ErrorCode.CLNRD_NotFound, "Clinic record not found!", 404) },
        { ErrorCode.GNRL_MultipleErrors, new ApiError(ErrorCode.GNRL_MultipleErrors, "Multiple errors occurred", 500) }

    };

    public static ApiError GetError(ErrorCode errorCode)
    {
        if (_errorRegistry.TryGetValue(errorCode, out var apiError))
        {
            return apiError;
        }

        throw new KeyNotFoundException($"Error code '{errorCode}' not found in the registry.");
    }
}
