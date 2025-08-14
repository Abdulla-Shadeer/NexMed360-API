using AutoMapper;
using FinalProjectApi.Common;
using FinalProjectApi.Common.ApplicationErrors;
using FinalProjectApi.Dtos.User;
using FinalProjectApi.Entities;
using FinalProjectApi.Interfaces.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FinalProjectApi.Services
{
    public class AuthService: IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly IConfiguration _config;
        private User? _user;
        private readonly IMapper _mapper;
        private readonly ILogger<AuthService> _logger;
        public AuthService(UserManager<User> userManager, IConfiguration configuration, IMapper mapper, ILogger<AuthService> logger) { 
            _userManager = userManager;
            _config = configuration;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<Result<LoginResponseDto>> LoginAsync(LoginUserDto loginUserDto) {
            if (await IsValidUser(loginUserDto))
            {
                var token = await GenerateToken();
                var authUser = _mapper.Map<ViewUserDto>(_user);
                authUser.Roles = (await _userManager.GetRolesAsync(_user!)).ToList();

                return Result<LoginResponseDto>.Success(new LoginResponseDto()
                {
                    AccessToken = token,
                    User = authUser
                });
            }
            else {
                return Result<LoginResponseDto>
                    .Failure(ErrorCode.SYS_Unauthorized, "Incorrect username or password!");
            }
        }

        public async Task<Result<ViewUserDto>> GetUserAsync(ClaimsPrincipal user) {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != null)
            {
                var searchUser = await _userManager.FindByIdAsync(userId);
                if (searchUser != null)
                {
                    var viewUserDto = _mapper.Map<ViewUserDto>(searchUser);
                    viewUserDto.Roles = (await _userManager.GetRolesAsync(searchUser)).ToList();
                    return Result<ViewUserDto>.Success(viewUserDto);
                }
                else
                {
                    return Result<ViewUserDto>.Failure(ErrorCode.SYS_Unauthorized, $"User with id {userId} not found.");
                }
            }
            else {
                return Result<ViewUserDto>.Failure(ErrorCode.SYS_BadRequest, "The received token is missing a valid 'sub' claim");
            }
        }

        private async Task<bool> IsValidUser(LoginUserDto user)
        {
            _user = await _userManager.FindByEmailAsync(user.Email);
            return _user != null && await _userManager.CheckPasswordAsync(_user, user.Password); 
        }

        private async Task<string> GenerateToken()
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var signingKey = Encoding.UTF8.GetBytes(_config["Auth:Jwt:Signing-Key"]!);
            var roles = (await _userManager.GetRolesAsync(_user!));

            var claims = new List<Claim>() {
                new(JwtRegisteredClaimNames.Sub, _user!.Id),
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
}
