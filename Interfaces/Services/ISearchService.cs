using NexMed360Api.Common;
using NexMed360Api.Dtos.Doctor;
using NexMed360Api.Dtos.Search;
using System.Security.Claims;

namespace NexMed360Api.Interfaces.Services;

public interface ISearchService
{
    Task<Result<List<ViewDoctorDto>>> SearchDoctor(SearchDoctorDto dto);
    Task<Result<object>> EntitySearch(EntitySearchDto dto, ClaimsPrincipal user);
}
