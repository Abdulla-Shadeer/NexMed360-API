namespace FinalProjectApi.Common.ApplicationErrors;

public static class ErrorRegistery
{
    private static readonly Dictionary<ErrorCode, ApiError> _errorRegistry = new()
    {
        { ErrorCode.APPT_NotFound, new ApiError(ErrorCode.APPT_NotFound, "No appointments found!", 404) },
        { ErrorCode.APPT_PatientUnavailable, new ApiError(ErrorCode.APPT_PatientUnavailable, "Patient is not available at the requested time!", 409) },
        { ErrorCode.APPT_DoctorUnavailable, new ApiError(ErrorCode.APPT_DoctorUnavailable, "Doctor is not available at the requested time!", 409) },
        { ErrorCode.APPT_InvalidId, new ApiError(ErrorCode.APPT_InvalidId, "Invalid appointment Id!", 400) },
        { ErrorCode.APPT_MinimumFilterRequirement, new ApiError(
            ErrorCode.APPT_MinimumFilterRequirement,
            "At least one person ID (Patient ID or Doctor ID) must be provided to filter appointments.", 400)
        },
        { ErrorCode.APPT_FromDateAfterToDate, new ApiError(ErrorCode.APPT_FromDateAfterToDate, "From date cannot be after To date!", 400) },
        { ErrorCode.Internal, new ApiError(ErrorCode.Internal, "An internal error occurred. Please try again later.", 500) },
        { ErrorCode.APPT_DoctorNotFound, new ApiError(ErrorCode.APPT_DoctorNotFound, "No doctor found with the given ID!", 404) },
        { ErrorCode.APPT_PatientNotFound, new ApiError(ErrorCode.APPT_PatientNotFound, "No patient found with the given ID!", 404) }

    };

    public static ApiError GetError(ErrorCode errorCode)
    {
        if (_errorRegistry.TryGetValue(errorCode, out var apiError))
        {
            return apiError;
        }

        throw new KeyNotFoundException($"Error code {errorCode} not found in the registry.");
    }
}
