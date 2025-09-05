using FinalProjectApi.Common;
using FinalProjectApi.Dtos.Search;
using FinalProjectApi.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinalProjectApi.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
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
}
