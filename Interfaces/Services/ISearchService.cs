using FinalProjectApi.Common;
using FinalProjectApi.Dtos.Doctor;
using FinalProjectApi.Dtos.Search;
using System.Security.Claims;

namespace FinalProjectApi.Interfaces.Services;

public interface ISearchService
{
    Task<Result<List<ViewDoctorDto>>> SearchDoctor(SearchDoctorDto dto);
    Task<Result<object>> EntitySearch(EntitySearchDto dto, ClaimsPrincipal user);
}
