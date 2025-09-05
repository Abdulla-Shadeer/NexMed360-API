using System.Text;
using FinalProjectApi.Common;
using FinalProjectApi.Common.Exceptions;
using FinalProjectApi.Data;
using FinalProjectApi.Entities;
using FinalProjectApi.Extensions;
using FinalProjectApi.Interfaces.Services;
using FinalProjectApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace FinalProjectApi
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            IConfiguration _config = builder.Configuration;

            // Configure microsoft extension logging
            builder.Logging.AddConfiguration();
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
            builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

            // Add DB context
            builder.Services.AddApplicationDbContext(_config);

            // Configure identity system
            var identityBuilder = builder.Services.AddIdentityCore<User>().AddRoles<IdentityRole>();
            identityBuilder = new IdentityBuilder(identityBuilder.UserType, typeof(IdentityRole), builder.Services);
            identityBuilder.AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();

            // Configure JWT
            builder
                .Services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = _config["Auth:Jwt:Issuer"],
                        ValidAudience = _config["Auth:Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(_config["Auth:Jwt:Signing-Key"]!)
                        ),
                    };
                });

            builder.Services.AddAuthorization();

            // Add services to the container.
            builder.Services.AddAutoMapper(typeof(MappingProfile));
            builder.Services.AddScoped<IDbInitializerService, DbInitializerService>();
            builder.Services.AddScoped<IOverviewService, OverviewService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IHospitalService, HospitalService>();
            builder.Services.AddScoped<IPatientService, PatientService>();
            builder.Services.AddScoped<IAppointmentService, AppointmentService>();
            builder.Services.AddScoped<ISearchService, SearchService>();
            builder.Services.AddScoped<GlobalExceptionMiddleware>();

            // Migrate and seed database
            await MigrateAndSeedDatabaseAsync(builder.Services.BuildServiceProvider());

            builder.Services.AddControllers()
                .ConfigureApiBehaviorOptions(opt => 
                opt.InvalidModelStateResponseFactory = InvalidModelStateResponseFactory.Function );
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
                app.UseCors(opt => opt.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
            }

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseMiddleware<GlobalExceptionMiddleware>();

            app.MapControllers();

            app.Run();

            async Task MigrateAndSeedDatabaseAsync(IServiceProvider serviceProvider)
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var dbInitializerService =
                    scope.ServiceProvider.GetRequiredService<IDbInitializerService>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                if (dbContext.Database.IsNpgsql())
                {
                    await dbContext.Database.MigrateAsync();
                }

                logger.LogInformation("Seeding data...");
                await dbInitializerService.SeedData();
            }
        }
    }
}
