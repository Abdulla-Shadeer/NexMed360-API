using Asp.Versioning;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;
using NexMed360Api.Dtos.PractitionerRole;
using NexMed360Api.Dtos.Organization;
using NexMed360Api.Dtos.OrganizationMember;
using NexMed360Api.Dtos.OrganizationMember;
using NexMed360Api.Dtos.User;
using NexMed360Api.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NexMed360Api.Controllers.v1;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class OrganizationController : ControllerBase
{

    private readonly IOrganizationService _organizationService;

    public OrganizationController(IOrganizationService organizationService)
    {
        _organizationService = organizationService;
    }

    [HttpPost]
    [Route("Add")]
    [Authorize(Roles = UserRoles.PLATFORM_ADMIN)]
    public async Task<ActionResult<Result<ViewOrganizationDto>>> AddOrganization(CreateOrganizationDto dto)
    {
        return HandleCreationResult(await _organizationService.Add(dto));
    }

    [HttpGet]
    [Route("GetAll")]
    [Authorize(Roles = $"{UserRoles.PLATFORM_ADMIN}, {UserRoles.PATIENT}")]
    public async Task<ActionResult<Result<List<ViewOrganizationDto>>>> GetAll()
    {

        var organizationsResult = await _organizationService.GetAll();
        if (organizationsResult.IsSuccess)
        {
            return Ok(organizationsResult);
        }

        return StatusCode(organizationsResult.Error!.HttpStatus, organizationsResult);
    }

    [HttpPost]
    [Route("AssignAdmin")]
    [Authorize(Roles = UserRoles.PLATFORM_ADMIN)]
    public async Task<ActionResult<Result<ViewUserDto>>> AddAdmin(CreateOrganizationManagerDto dto)
    {
        return HandleCreationResult(await _organizationService.AssignAdmin(dto));
    }

    [HttpPut]
    [Route("Update")]
    [Authorize(Roles = $"{UserRoles.PLATFORM_ADMIN},{UserRoles.ORGANIZATION_MANAGER}")]
    public async Task<ActionResult<Result<ViewOrganizationDto>>> UpdateOrganization(UpdateOrganizationDto updateDto)
    {
        var updateResult = await _organizationService.Update(updateDto);
        if (updateResult.IsSuccess)
        {
            return Ok(updateResult);
        }

        return StatusCode(updateResult.Error!.HttpStatus, updateResult);
    }

    [HttpGet]
    [Route("FindById/{id}")]
    [Authorize]
    public async Task<ActionResult<Result<ViewOrganizationDto>>> FindById(string id)
    {
        var searchResult = await _organizationService.FindById(id, User.IsInRole(UserRoles.PLATFORM_ADMIN));
        if (searchResult.IsSuccess)
        {
            return Ok(searchResult);
        }

        return StatusCode(searchResult.Error!.HttpStatus, searchResult);
    }

    [HttpPost]
    [Route("AddOrganizationMember")]
    [Authorize(Roles = UserRoles.ORGANIZATION_MANAGER)]
    public async Task<ActionResult<Result<ViewUserDto>>> AddOrganizationMember(CreateOrganizationMemberDto dto)
    {
        return HandleCreationResult(await _organizationService.AddOrganizationMember(dto));
    }

    [HttpPost]
    [Route("AddPractitionerRole")]
    [Authorize(Roles = UserRoles.ORGANIZATION_MANAGER)]
    public async Task<ActionResult<Result<ViewUserDto>>> AddPractitionerRole(CreatePractitionerRoleDto dto)
    {
        return HandleCreationResult(await _organizationService.AddPractitionerRole(dto));
    }

    [HttpGet]
    [Route("GetMembers/{organizationId}")]
    [Authorize(Roles = UserRoles.ORGANIZATION_MANAGER)]
    public async Task<ActionResult<Result<List<ViewUserDto>>>> GetMembers(string organizationId)
    {
        var getMembersResult = await _organizationService.GetMembers(organizationId, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (getMembersResult.IsSuccess)
        {
            return Ok(getMembersResult);
        }

        return StatusCode(getMembersResult.Error!.HttpStatus, getMembersResult);
    }

    private ActionResult<Result<T>> HandleCreationResult<T>(Result<T> result)
    {
        if (!result.IsSuccess)
            return StatusCode(result.Error!.HttpStatus, result);

        return Created(string.Empty, result);
    }
}
