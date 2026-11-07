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
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<IdentityGateway> _logger;

    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config;

    public IdentityGateway(
        UserManager<User> userManager,
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

        // create user if no user already exists with the email
        // add only to new role if a user profile already exists
        var existingUser = await _userManager.FindByEmailAsync(user.Email);
        if (existingUser != null)
        {
            if (!await _userManager.IsInRoleAsync(existingUser, role))
            {
                await _userManager.AddToRoleAsync(existingUser, role);
                var updatedRoles = await _userManager.GetRolesAsync(existingUser);
                var viewUserDto = Summary(existingUser, updatedRoles.ToList(), includeProfiles: false);
                _logger.LogInformation($"User already exists, assigned to new role: '{role}'");
                return Result<AccountSummary>.Success(viewUserDto);
            }

            var error = $"User already exists and already has the '{role}' role.";
            _logger.LogError(error);
            return Result<AccountSummary>.Failure(ErrorCode.USR_RegistrationFailed, error, 400);
        }

        var newUser = new User
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            UserName = user.Email,
            PhoneNumber = user.PhoneNumber,
            CreatedDate = DateTime.Now,
            UpdatedDate = DateTime.Now
        };

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
            Result<AccountSummary>.Failure(ErrorCode.USR_RegistrationFailed, errDetail);
        }

        await _userManager.AddToRoleAsync(newUser, role);
        var userId = await _userManager.GetUserIdAsync(newUser);
        return await GetUserAsync(userId);
    }

    public async Task<Result<AccountSummary>> GetUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return Result<AccountSummary>.Failure(ErrorCode.SYS_Unauthorized, $"User with id {userId} not found.");
        return Result<AccountSummary>.Success(Summary(user, (await _userManager.GetRolesAsync(user)).ToList()));
    }
    private static AccountSummary Summary(User user, List<string> roles, bool includeProfiles = true)
        => new(user.Id, user.FirstName, user.LastName, user.Email, user.PhoneNumber) { Roles = roles, IncludeProfiles = includeProfiles };

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

    private async Task<string> GenerateToken(User user)
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
