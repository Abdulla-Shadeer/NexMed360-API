using NexMed360.API.Dtos.practitionerSchedule;
using NexMed360Api.Common;
using NexMed360Api.Dtos.practitionerSchedule;

namespace NexMed360Api.Interfaces.Services;

public interface IPractitionerScheduleService
{
    public Task<Result<ViewPractitionerScheduleDto>> Create(CreatePractitionerScheduleDto dto);
    public Task<Result<List<ViewPractitionerScheduleDto>>> Get(FilterPractitionerScheduleDto filter);
    public Task<Result<List<ViewSessionSlotDto>>> GetSlots(GetSessionSlotsDto dto);
    public Task<Result<ViewSessionHistoryDto>> Start(string sessionId);
    public Task<Result<ViewSessionHistoryDto>> UpdateHistory(UpdateSessionHistoryDto dto);
    public Task<Result<List<ViewSessionHistoryDto>>> GetHistory(FilterSessionHistoryDto filter);
    public Task<Result<ViewCurrentOrNextSessionDto>> GetCurrentOrUpcoming(FilterSessionsForCurrentDayDto filter);
    public Result<bool> HasAnySessionToday(FilterSessionsForCurrentDayDto filter);
    public Task<Result<List<ViewSessionHistoryDto>>> GetOngoing(string organizationMemberUserId);
    public Task<Result<ViewPractitionerScheduleDto>> Update(string sessionId, UpdatePractitionerScheduleDto dto);
    public Task<Result<string>> Delete(string sessionId);
}
