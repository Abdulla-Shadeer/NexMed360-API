using Asp.Versioning;
using NexMed360Api.Common;
using NexMed360Api.Dtos.Doctor;
using NexMed360Api.Dtos.Search;
using NexMed360Api.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NexMed360Api.Controllers.v1;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
public class SearchController : ControllerBase
{

    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [Route("Entity")]
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Result<object>>> EntitySearch(EntitySearchDto dto)
    {
        var res = await _searchService.EntitySearch(dto, User);
        if (res.IsSuccess)
        {
            return Ok(res);
        }
        return StatusCode(res.Error!.HttpStatus, res);
    }

    [Route("Doctor")]
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Result<List<ViewDoctorDto>>>> DoctorSearch(SearchDoctorDto dto)
    {
        var res = await _searchService.SearchDoctor(dto);
        if (res.IsSuccess)
        {
            return Ok(res);
        }
        return StatusCode(res.Error!.HttpStatus, res);
    }
}
