using NexMed360Api.Common.Auth;
using Asp.Versioning;
using NexMed360Api.Common;
using NexMed360Api.Dtos.PractitionerRole;
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
    [Workspace(WorkspaceModes.Work + "," + WorkspaceModes.Patient + "," + WorkspaceModes.Platform)]
    public async Task<ActionResult<Result<object>>> EntitySearch(EntitySearchDto dto)
    {
        var res = await _searchService.EntitySearch(dto, User.ToCallerContext());
        if (res.IsSuccess)
        {
            return Ok(res);
        }
        return StatusCode(res.Error!.HttpStatus, res);
    }

    [Route("PractitionerRole")]
    [HttpPost]
    [Authorize]
    [Workspace(WorkspaceModes.Work + "," + WorkspaceModes.Patient)]
    public async Task<ActionResult<Result<List<ViewPractitionerRoleDto>>>> PractitionerRoleSearch(SearchPractitionerRoleDto dto)
    {
        var res = await _searchService.SearchPractitionerRole(dto);
        if (res.IsSuccess)
        {
            return Ok(res);
        }
        return StatusCode(res.Error!.HttpStatus, res);
    }
}
