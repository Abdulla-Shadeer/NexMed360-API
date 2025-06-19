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

    [EnumMember(Value = "USR_001")]
    USR_NotFound
}
