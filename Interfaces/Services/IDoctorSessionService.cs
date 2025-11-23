using NexMed360Api.Common;
using NexMed360Api.Dtos.doctorSession;

namespace NexMed360Api.Interfaces.Services;

public interface IDoctorSessionService
{
    public Task<Result<ViewDoctorSessionDto>> Create(CreateDoctorSessionDto dto);
    public Task<Result<List<ViewDoctorSessionDto>>> Get(FilterDoctorSessionDto filter);
    public Task<Result<List<ViewSessionSlotDto>>> GetSlots(GetSessionSlotsDto dto);
    public Task<Result<ViewSessionHistoryDto>> Start(string sessionId);
    public Task<Result<ViewSessionHistoryDto>> UpdateHistory(UpdateSessionHistoryDto dto);
    public Task<Result<List<ViewSessionHistoryDto>>> GetHistory(FilterSessionHistoryDto filter);
    public Task<Result<ViewCurrentOrNextSessionDto>> GetCurrentOrUpcoming(FilterSessionsForCurrentDayDto filter);
    public Result<bool> HasAnySessionToday(FilterSessionsForCurrentDayDto filter);
    public Task<Result<List<ViewSessionHistoryDto>>> GetOngoing(string receptionistUserId);
}
