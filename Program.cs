using Asp.Versioning.ApiExplorer;
using Azure.Identity;
using NexMed360Api.Common;
using NexMed360Api.Common.Exceptions;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Extensions;
using NexMed360Api.Interfaces.Services;
using NexMed360Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace NexMed360Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Retrieve the app config endpoint
        string endpoint = builder.Configuration["APP_CONFIG_ENDPOINT"]
            ?? throw new InvalidOperationException("The setting `APP_CONFIG_ENDPOINT` was not found.");

        // Load configuration from Azure App Configuration 
        builder.Configuration.AddAzureAppConfiguration(options =>
        {
            options
            .Select("*", builder.Environment.EnvironmentName)
            .Select("Auth*")
            .ConfigureRefresh(cfg =>
            {
                // Max 1000 requests per day in free tier
                // 1440 minutes in a day / 10 minutes interval = 144 requests per day by one instance
                // There is enough room for other instances to refresh as well
                cfg.SetRefreshInterval(TimeSpan.FromMinutes(10))
                .RegisterAll();
            })
            .Connect(new Uri(endpoint), new DefaultAzureCredential());
        });

        builder.Services.AddAzureAppConfiguration();

        IConfiguration _config = builder.Configuration;

        // Configure microsoft extension logging
        builder.Logging.AddConfiguration();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        builder.Logging
            .AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning)
            .AddFilter("Microsoft.AspNetCore.Watch*", LogLevel.Error);

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
        builder.Services.AddAutoMapper(cfg => cfg.LicenseKey = _config["AutoMapper:LicenceKey"], typeof(MappingProfile));
        builder.Services.AddScoped<IDbInitializerService, DbInitializerService>();
        builder.Services.AddScoped<IOverviewService, OverviewService>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IHospitalService, HospitalService>();
        builder.Services.AddScoped<IPatientService, PatientService>();
        builder.Services.AddScoped<IAppointmentService, AppointmentService>();
        builder.Services.AddScoped<ISearchService, SearchService>();
        builder.Services.AddScoped<IDoctorSessionService, DoctorSessionService>();
        builder.Services.AddScoped<GlobalExceptionMiddleware>();

        // Migrate and seed database
        await MigrateAndSeedDatabaseAsync(builder.Services.BuildServiceProvider());

        builder.Services.AddControllers()
            .ConfigureApiBehaviorOptions(opt => 
            opt.InvalidModelStateResponseFactory = InvalidModelStateResponseFactory.Function );
        builder.Services.AddApiVersioning(opt => {
            opt.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
            opt.AssumeDefaultVersionWhenUnspecified = true;
            opt.ReportApiVersions = true;
        }).AddApiExplorer(opt => {
            opt.GroupNameFormat = "'v'VVV";
            opt.SubstituteApiVersionInUrl = true;
        });

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(opt => {
            var provider = builder.Services.BuildServiceProvider()
            .GetRequiredService<IApiVersionDescriptionProvider>();

            foreach (var description in provider.ApiVersionDescriptions)
            {
                opt.SwaggerDoc(description.GroupName, new Microsoft.OpenApi.OpenApiInfo
                {
                    Title = $"NexMed 360 API {description.ApiVersion}",
                    Version = description.ApiVersion.ToString(),
                    Description = description.IsDeprecated ? "This API version has been deprecated." : ""
                });
            }
        });

        var app = builder.Build();
        app.UseAzureAppConfiguration();
        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

                foreach (var description in provider.ApiVersionDescriptions)
                {
                    options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json",
                        $"API {description.ApiVersion}");
                }
            });

            app.UseCors(opt => opt.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        }

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
