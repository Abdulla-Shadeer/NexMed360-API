using NexMed360.Application.Access;
using Asp.Versioning;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;
using NexMed360Api.Dtos.PractitionerRole;
using NexMed360Api.Dtos.Organization;
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
    private readonly WorkspaceAccess? _workspace;

    public OrganizationController(IOrganizationService organizationService, WorkspaceAccess? workspace = null)
    {
        _organizationService = organizationService;
        _workspace = workspace;
    }

    [HttpPost]
    [Route("Add")]
    [Authorize]
    [Workspace(WorkspaceModes.Platform)]
    public async Task<ActionResult<Result<ViewOrganizationDto>>> AddOrganization(CreateOrganizationDto dto)
    {
        return HandleCreationResult(await _organizationService.Add(dto));
    }

    [HttpGet]
    [Route("GetAll")]
    [Authorize]
    [Workspace(WorkspaceModes.Platform + "," + WorkspaceModes.Patient)]
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
    [Authorize]
    [Workspace(WorkspaceModes.Platform)]
    public async Task<ActionResult<Result<ViewUserDto>>> AddAdmin(CreateOrganizationManagerDto dto)
    {
        return HandleCreationResult(await _organizationService.AssignAdmin(dto));
    }

    [HttpPut]
    [Route("Update")]
    [Authorize]
    [Workspace(WorkspaceModes.Work + "," + WorkspaceModes.Platform, Permissions.OrganizationsManage)]
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
    [Workspace(WorkspaceModes.Platform + "," + WorkspaceModes.Work + "," + WorkspaceModes.Patient)]
    public async Task<ActionResult<Result<ViewOrganizationDto>>> FindById(string id)
    {
        var searchResult = await _organizationService.FindById(id, _workspace?.Mode == WorkspaceModes.Platform);
        if (searchResult.IsSuccess)
        {
            return Ok(searchResult);
        }

        return StatusCode(searchResult.Error!.HttpStatus, searchResult);
    }

    [HttpPost]
    [Route("AddOrganizationMember")]
    [Authorize]
    [Workspace(WorkspaceModes.Work, Permissions.MembersManage)]
    public async Task<ActionResult<Result<ViewUserDto>>> AddOrganizationMember(CreateOrganizationMemberDto dto)
    {
        return HandleCreationResult(await _organizationService.AddOrganizationMember(dto));
    }

    [HttpPost]
    [Route("AddPractitionerRole")]
    [Authorize]
    [Workspace(WorkspaceModes.Work, Permissions.MembersManage)]
    public async Task<ActionResult<Result<ViewUserDto>>> AddPractitionerRole(CreatePractitionerRoleDto dto)
    {
        return HandleCreationResult(await _organizationService.AddPractitionerRole(dto));
    }

    [HttpGet]
    [Route("GetMembers/{organizationId}")]
    [Authorize]
    [Workspace(WorkspaceModes.Work, Permissions.MembersManage)]
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
