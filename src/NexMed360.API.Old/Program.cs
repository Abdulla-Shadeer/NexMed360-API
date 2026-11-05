using Asp.Versioning.ApiExplorer;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.IdentityModel.Tokens;
using NexMed360Api.Common;
using NexMed360Api.Common.Exceptions;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Extensions;
using NexMed360Api.Interfaces.Services;
using NexMed360Api.Services;
using System.Text;

namespace NexMed360Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Local development can explicitly bypass Azure without changing deployed settings.
        bool useAzureAppConfiguration = builder.Configuration
            .GetValue("AzureAppConfiguration:Enabled", true);

        if (useAzureAppConfiguration)
        {
            string endpoint = builder.Configuration["APP_CONFIG_ENDPOINT"]
                ?? throw new InvalidOperationException("The setting `APP_CONFIG_ENDPOINT` was not found. "
                    + "For local configuration, set `AzureAppConfiguration:Enabled` to false.");

            builder.Configuration.AddAzureAppConfiguration(options =>
            {
                options
                    .Select("*", builder.Environment.EnvironmentName)
                    .Select("Auth*")
                    .ConfigureRefresh(cfg =>
                    {
                        cfg.SetRefreshInterval(TimeSpan.FromMinutes(10)).RegisterAll();
                    })
                    .Connect(new Uri(endpoint), new DefaultAzureCredential());
            });

            // Explicit environment and command-line values take precedence over Azure.
            builder.Configuration.AddEnvironmentVariables().AddCommandLine(args);
            builder.Services.AddAzureAppConfiguration();
        }

        IConfiguration _config = builder.Configuration;
        bool migrateOnStartup = _config.GetValue("DatabaseInitialization:MigrateOnStartup", false);
        bool seedOnStartup = _config.GetValue("DatabaseInitialization:SeedOnStartup", false);

        // Report missing settings before constructing services or accessing the database.
        string[] requiredSettings = [
            "ConnectionStrings:DefaultConnection",
            "Auth:Jwt:Issuer",
            "Auth:Jwt:Audience",
            "Auth:Jwt:Signing-Key",
            "Auth:Jwt:Validity"
        ];
        var missingSettings = requiredSettings
            .Where(key => string.IsNullOrWhiteSpace(_config[key])).ToList();
        if (seedOnStartup && string.IsNullOrWhiteSpace(_config["Auth:SuperUserPassword"]))
        {
            missingSettings.Add("Auth:SuperUserPassword");
        }
        if (missingSettings.Count > 0)
        {
            throw new InvalidOperationException("Required configuration is missing: "
                + string.Join(", ", missingSettings));
        }

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
        if (migrateOnStartup || seedOnStartup)
        {
            await InitializeDatabaseAsync(app.Services);
        }
        else
        {
            app.Logger.LogInformation("Startup database migration and seeding are disabled.");
        }

        if (useAzureAppConfiguration)
        {
            app.UseAzureAppConfiguration();
        }
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

        async Task InitializeDatabaseAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            if (migrateOnStartup)
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (dbContext.Database.IsNpgsql())
                {
                    logger.LogInformation("Applying database migrations...");
                    await dbContext.Database.MigrateAsync();
                }
            }

            if (seedOnStartup)
            {
                logger.LogInformation("Seeding data...");
                var dbInitializerService = scope.ServiceProvider.GetRequiredService<IDbInitializerService>();
                await dbInitializerService.SeedData();
            }
        }
    }
}
