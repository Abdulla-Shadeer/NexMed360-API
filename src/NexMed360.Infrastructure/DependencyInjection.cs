using NexMed360.Application.Access;
using NexMed360.Infrastructure.Repositories;
using NexMed360.Application.Accounts.Internal;
using NexMed360.Application.Accounts;
using NexMed360.Application.Overview;
using NexMed360.Application.Search;
using System.Text;
using NexMed360.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using NexMed360Api.Common;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Extensions;
using NexMed360Api.Interfaces.Services;
using NexMed360Api.Services;

namespace NexMed360.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNexMed360Infrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<WorkspaceAccess>();
        services.AddScoped<IAccessRepository, AccessRepository>();
        services.AddScoped<AccessService>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<WorkspaceService>();
        services.AddApplicationDbContext(configuration);
        var identity = services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>();
        identity = new IdentityBuilder(identity.UserType, typeof(IdentityRole), services);
        identity.AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Auth:Jwt:Issuer"], ValidAudience = configuration["Auth:Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Auth:Jwt:Signing-Key"]!))
        });
        services.AddAuthorization();
        services.AddScoped<IOverviewRepository, OverviewRepository>();
        services.AddScoped<ISearchRepository, SearchRepository>();
        services.AddScoped<IPractitionerScheduleRepository, PractitionerScheduleRepository>();
        services.AddScoped<IRegistrationTransactions, RegistrationTransactions>();
        services.AddScoped<IIdentityGateway, IdentityGateway>();
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IClinicalRecordRepository, ClinicalRecordRepository>();
        services.AddScoped<IPractitionerRoleRepository, PractitionerRoleRepository>();
        services.AddScoped<IOrganizationMemberRepository, OrganizationMemberRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOverviewService, OverviewService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IPractitionerScheduleService, PractitionerScheduleService>();
        services.AddScoped<IDbInitializerService, DbInitializerService>();
        return services;
    }
}
