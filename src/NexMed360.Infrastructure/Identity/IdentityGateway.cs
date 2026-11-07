using NexMed360.Application.Accounts.Internal;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using NexMed360.Application.Shared;
using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Common.Auth;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Extensions;
using NexMed360Api.Interfaces.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexMed360.Domain.Rules;

namespace NexMed360Api.Services;

public class IdentityGateway : IIdentityGateway
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<IdentityGateway> _logger;

    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config;

    public IdentityGateway(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<IdentityGateway> logger,
        ApplicationDbContext context,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
        _context = context;
        _config = configuration;
    }

    public async Task<Result<AccountSummary>> RegisterUser(AccountRegistration user, string role)
    {
        var identityRole = await _roleManager.FindByNameAsync(role);
        if (identityRole == null)
            return Result<AccountSummary>.Failure(ErrorCode.USR_RoleNotFound, $"The {role} role does not exist.");

        // Reuse the account when adding another profile or organization assignment.
        var existingUser = await _userManager.FindByEmailAsync(user.Email);
        if (existingUser != null)
        {
            if (!await _userManager.IsInRoleAsync(existingUser, role))
            {
                var assigned = await _userManager.AddToRoleAsync(existingUser, role);
                if (!assigned.Succeeded) return Result<AccountSummary>.Failure(ErrorCode.USR_RegistrationFailed, "Could not grant account access.");
                var updatedRoles = await _userManager.GetRolesAsync(existingUser);
                var viewUserDto = Summary(existingUser, updatedRoles.ToList(), includeProfiles: false);
                _logger.LogInformation("Added account role {Role}.", role);
                return Result<AccountSummary>.Success(viewUserDto);
            }

            var roles = await _userManager.GetRolesAsync(existingUser);
            return Result<AccountSummary>.Success(Summary(existingUser, roles.ToList(), includeProfiles: false));
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
            return Result<AccountSummary>.Failure(ErrorCode.USR_RegistrationFailed, stringErrors, 400);
        }

        var identityResult = await _userManager.CreateAsync(newUser, user.Password);

        if (!identityResult.Succeeded)
        {
            var errDetail = IdentityErrorExtension.IdentityError(identityResult);
            _logger.LogError(errDetail);
            return Result<AccountSummary>.Failure(ErrorCode.USR_RegistrationFailed, errDetail);
        }

        var roleResult = await _userManager.AddToRoleAsync(newUser, role);
        if (!roleResult.Succeeded) return Result<AccountSummary>.Failure(ErrorCode.USR_RegistrationFailed, "Could not grant account access.");
        var userId = await _userManager.GetUserIdAsync(newUser);
        return await GetUserAsync(userId);
    }

    public async Task<Result<AccountSummary>> GetUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return Result<AccountSummary>.Failure(ErrorCode.SYS_Unauthorized, $"Account with ID {userId} not found.");
        return Result<AccountSummary>.Success(Summary(user, (await _userManager.GetRolesAsync(user)).ToList()));
    }
    private static AccountSummary Summary(ApplicationUser user, List<string> roles, bool includeProfiles = true)
        => new(user.Id, user.FirstName, user.LastName, user.Email, user.PhoneNumber) { PersonId = user.PersonId, Roles = roles, IncludeProfiles = includeProfiles };

    public async Task<Result<AccountSummary>> UpdateUser(AccountChanges updateDto, string? requesterUserId)
    {
        if (string.IsNullOrWhiteSpace(requesterUserId))
            return Result<AccountSummary>.Failure(ErrorCode.SYS_Unauthorized);

        if (!AccountRules.IsSelf(updateDto.Id, requesterUserId))
            return Result<AccountSummary>.Failure(ErrorCode.SYS_Unauthorized,
                "You can update only your own account.", 403);

        if (updateDto.Email != null &&
            await _context.Users.AnyAsync(u => u.Email == updateDto.Email))
        {
            return Result<AccountSummary>.Failure(ErrorCode.SYS_DuplicateEntry, $"This email '{updateDto.Email}' is already used by someone.");
        }

        var usr = await _context.Users

            .FirstOrDefaultAsync(u => u.Id == requesterUserId);
        if (usr == null)
            return Result<AccountSummary>.Failure(ErrorCode.USR_NotFound);
        usr.FirstName = updateDto.FirstName ?? usr.FirstName;
        usr.LastName = updateDto.LastName ?? usr.LastName;
        usr.Email = updateDto.Email ?? usr.Email;
        usr.PhoneNumber = updateDto.PhoneNumber ?? usr.PhoneNumber;
        usr.Person.Email = usr.Email;
        usr.Person.PhoneNumber = usr.PhoneNumber;

        await _context.SaveChangesAsync();
        return await GetUserAsync(usr.Id);
    }
    public async Task<Result<AuthenticatedAccount>> AuthenticateAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, password))
            return Result<AuthenticatedAccount>.Failure(ErrorCode.SYS_Unauthorized, "Incorrect username or password!");
        return Result<AuthenticatedAccount>.Success(new AuthenticatedAccount(user.Id, await GenerateToken(user)));
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
