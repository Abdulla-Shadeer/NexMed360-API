using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace FinalProjectApi.Common.ApplicationErrors;

[JsonConverter(typeof(EnumMemberValueConverter<ErrorCode>))]
public enum ErrorCode
{
    [EnumMember(Value = "SYS_001")]
    Internal,

    [EnumMember(Value = "SYS_002")]
    SYS_InvalidGuid,

    [EnumMember(Value = "SYS_003")]
    SYS_Unauthorized,

    [EnumMember(Value = "SYS_004")]
    SYS_BadRequest,

    [EnumMember(Value = "SYS_005")]
    SYS_DuplicateEntry,

    [EnumMember(Value = "APPT_001")]
    APPT_NotFound,

    [EnumMember(Value = "APPT_002")]
    APPT_DoctorUnavailable,

    [EnumMember(Value = "APPT_003")]
    APPT_PatientUnavailable,

    [EnumMember(Value = "APPT_004")]
    APPT_MinimumFilterRequirement,

    [EnumMember(Value = "APPT_005")]
    APPT_FromDateAfterToDate,

    [EnumMember(Value = "APPT_006")]
    APPT_DoctorNotFound,

    [EnumMember(Value = "APPT_007")]
    APPT_PatientNotFound,

    [EnumMember(Value = "APPT_008")]
    APPT_Cancelled,

    [EnumMember(Value = "APPT_009")]
    APPT_CancellationAlreadyRequested,

    [EnumMember(Value = "APPT_010")]
    APPT_UnauthorizedEdit,

    [EnumMember(Value = "USR_001")]
    USR_NotFound,

    [EnumMember(Value = "USR_002")]
    USR_RoleNotFound,

    [EnumMember(Value = "USR_003")]
    USR_RegistrationFailed,

    [EnumMember(Value = "HOS_001")]
    HOS_NotFound,

    [EnumMember(Value = "CLNRD_001")]
    CLNRD_NotFound,

    [EnumMember(Value = "DRSSN_001")]
    DRSSN_NotFound,

    [EnumMember(Value = "DRSSN_002")]
    DRSSN_HSTRY_NotFound,
}
