using FinalProjectApi.Common;
using FinalProjectApi.Common.ApplicationErrors;
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
        try
        {
            var res = await _searchService.EntitySearch(dto, User);
            if (res.IsSuccess)
            {
                return Ok(res);
            }
            return StatusCode(res.Error!.HttpStatus, res);
        }
        catch (Exception e)
        {
            return StatusCode(
            500,
            Result<object>.Failure(
                new ApiError(ErrorCode.Internal, $"An error occurred while processing the request.", 500, e.Message)
            ));
        }
    }
}
