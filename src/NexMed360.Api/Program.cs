using Asp.Versioning.ApiExplorer;
using Azure.Identity;
using NexMed360.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using NexMed360Api.Common;
using NexMed360Api.Common.Exceptions;

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
        if (seedOnStartup && string.IsNullOrWhiteSpace(_config["Auth:PlatformAdminPassword"]))
        {
            missingSettings.Add("Auth:PlatformAdminPassword");
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

        builder.Services.AddNexMed360Infrastructure(_config);
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
            await app.Services.InitializeNexMedDatabaseAsync(migrateOnStartup, seedOnStartup);
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

    }
}
