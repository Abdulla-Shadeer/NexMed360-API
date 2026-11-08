using NexMed360Api.Dtos.User;
using NexMed360.Application.Accounts.Internal;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Common.Auth;
using NexMed360Api.Entities;
using NexMed360Api.Extensions;
using NexMed360Api.Interfaces.Services;
using Microsoft.AspNetCore.Identity;
using NexMed360.Domain.Rules;

namespace NexMed360Api.Services;

public class IdentityGateway : IIdentityGateway
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<IdentityGateway> _logger;

    private readonly IConfiguration _config;

    public IdentityGateway(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<IdentityGateway> logger,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
        _config = configuration;
    }

    public async Task<Result<ViewUserDto>> RegisterUser(RegisterUserDto user, string role)
    {
        var identityRole = await _roleManager.FindByNameAsync(role);
        if (identityRole == null)
            return ApiError.From(ErrorCode.USR_RoleNotFound, $"The {role} role does not exist.");

        // Reuse the account when adding another profile or organization assignment.
        var existingUser = await _userManager.FindByEmailAsync(user.Email);
        if (existingUser != null)
        {
            if (!existingUser.IsActive) return ApiError.From(ErrorCode.SYS_Unauthorized, "This account is inactive.", 403);

            if (!await _userManager.IsInRoleAsync(existingUser, role))
            {
                var assigned = await _userManager.AddToRoleAsync(existingUser, role);
                if (!assigned.Succeeded) return ApiError.From(ErrorCode.USR_RegistrationFailed, "Could not grant account access.");

                var viewUserDto = Summary(existingUser);
                _logger.LogInformation("Added account role {Role}.", role);
                return viewUserDto;
            }

            return Summary(existingUser);
        }

        var newUser = new ApplicationUser
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            UserName = user.Email,
            PhoneNumber = user.PhoneNumber,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        newUser.Person.Email = user.Email;
        newUser.Person.PhoneNumber = user.PhoneNumber;
        newUser.Person.FirstName = user.FirstName;
        newUser.Person.LastName = user.LastName;

        var passwordValidationErrors = new List<IdentityError>();
        foreach (var validator in _userManager.PasswordValidators)
        {
            var validationResult = await validator.ValidateAsync(_userManager, newUser, user.Password);
            if (!validationResult.Succeeded)
                passwordValidationErrors.AddRange(validationResult.Errors);
        }

        if (passwordValidationErrors.Count > 0)
        {
            string stringErrors = string.Join(Environment.NewLine, passwordValidationErrors.Select(e => $"{e.Code}: {e.Description}"));
            return ApiError.From(ErrorCode.USR_RegistrationFailed, stringErrors, 400);
        }

        var identityResult = await _userManager.CreateAsync(newUser, user.Password);

        if (!identityResult.Succeeded)
        {
            var errDetail = IdentityErrorExtension.IdentityError(identityResult);
            _logger.LogError(errDetail);
            return ApiError.From(ErrorCode.USR_RegistrationFailed, errDetail);
        }

        var roleResult = await _userManager.AddToRoleAsync(newUser, role);
        if (!roleResult.Succeeded) return ApiError.From(ErrorCode.USR_RegistrationFailed, "Could not grant account access.");
        var userId = await _userManager.GetUserIdAsync(newUser);
        return Summary(newUser);
    }

    private static ViewUserDto Summary(ApplicationUser user)
        => new()
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            PersonId = user.PersonId.ToString()
        };

    public async Task<Result<AuthenticatedAccount>> AuthenticateAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !user.IsActive || !await _userManager.CheckPasswordAsync(user, password))
            return ApiError.From(ErrorCode.SYS_Unauthorized, "Incorrect username or password!");

        return new AuthenticatedAccount(user.Id, await GenerateToken(user));
    }

    private async Task<string> GenerateToken(ApplicationUser user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var signingKey = Encoding.UTF8.GetBytes(_config["Auth:Jwt:Signing-Key"]!);
        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>() {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Sid, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: _config["Auth:Jwt:Issuer"]!,
            audience: _config["Auth:Jwt:Audience"]!,
            claims: claims,
            expires: DateTime.Now.AddDays(int.Parse(_config["Auth:Jwt:Validity"]!)),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(signingKey), SecurityAlgorithms.HmacSha256),
            notBefore: null
            );

        return tokenHandler.WriteToken(token);
    }
}
