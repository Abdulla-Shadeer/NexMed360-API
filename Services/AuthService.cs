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
        public AuthService(UserManager<User> userManager, IConfiguration configuration) { 
            _userManager = userManager;
            _config = configuration;
        }

        public async Task<bool> ValidateUser(LoginUserDto user)
        {
            _user = await _userManager.FindByEmailAsync(user.Email);

            if (_user != null)
            {
                return await _userManager.CheckPasswordAsync(_user, user.Password);
            }
            else { 
                return false;
            }
        }

        public async Task<string> GenerateToken()
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var signingKey = Encoding.UTF8.GetBytes(_config["Auth:Jwt:Signing-Key"]!);
            var role = (await _userManager.GetRolesAsync(_user!)).First();

            var tokenDescriptor = new SecurityTokenDescriptor {
                Subject = new ClaimsIdentity(
                    new Claim[] {
                        new Claim(ClaimTypes.Sid, _user!.Id),
                        new Claim(ClaimTypes.Email, _user!.Email),
                        new Claim("name", _user!.FirstName + " " + _user!.LastName),
                        new Claim(ClaimTypes.Role, role)
                    }
                    ),
                Expires = DateTime.Now.AddDays(int.Parse(_config["Auth:Jwt:Validity"]!)),
                Issuer = _config["Auth:Jwt:Issuer"]!,
                Audience = _config["Auth:Jwt:Audience"]!,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(signingKey), SecurityAlgorithms.HmacSha256)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
