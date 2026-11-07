using NexMed360Api.Common.Auth;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Controllers.v1;
using NexMed360Api.Dtos.Overview;
using NexMed360Api.Dtos.User;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.API.Old.Tests;

public class UserControllerCallerTests
{
    [Theory]
    [InlineData("signed-in-user")]
    [InlineData(null)]
    public async Task UpdatePassesTokenIdentitySeparatelyFromSubmittedTarget(string? requesterUserId)
    {
        var service = new RecordingUserService();
        var claims = requesterUserId == null ? Array.Empty<Claim>()
            : new[] { new Claim(ClaimTypes.NameIdentifier, requesterUserId) };
        var controller = new UsersController(null!, service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                }
            }
        };
        var dto = new UpdateUserDto { Id = "submitted-target", FirstName = "Updated" };

        var result = await controller.UpdateUser(dto);

        Assert.Same(dto, service.Dto);
        Assert.Equal("submitted-target", service.Dto!.Id);
        Assert.Equal(requesterUserId, service.RequesterUserId);
        var response = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, response.StatusCode);
        var body = Assert.IsType<Result<ViewUserDto>>(response.Value);
        Assert.False(body.IsSuccess);
        Assert.Null(body.Data);
    }

    [Fact]
    public void UpdateContinuesToRequireAuthentication()
    {
        var method = typeof(UsersController).GetMethod(nameof(UsersController.UpdateUser))!;

        Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
    }

    private sealed class RecordingUserService : IUserService
    {
        public UpdateUserDto? Dto { get; private set; }
        public string? RequesterUserId { get; private set; }

        public Task<Result<ViewUserDto>> UpdateUser(UpdateUserDto updateDto, string? requesterUserId)
        {
            (Dto, RequesterUserId) = (updateDto, requesterUserId);
            return Task.FromResult(Result<ViewUserDto>.Failure(
                ErrorCode.SYS_Unauthorized, "You can update only your own account.", 403));
        }

        public Task<Result<List<OverviewDto>>> GetOverview(CallerContext user) => throw new NotSupportedException();
        public Task<Result<ViewUserDto>> GetUserAsync(string userId) => throw new NotSupportedException();
    }
}
