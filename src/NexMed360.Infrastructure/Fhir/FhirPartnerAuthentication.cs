using Microsoft.AspNetCore.Authentication;
using NexMed360.Application.Fhir;

namespace NexMed360.Infrastructure.Fhir;

public static class FhirPartnerAuthentication
{
    public const string Scheme = "FhirPartner";
    public const string Policy = "FhirPartner";
    public const string PartnerClaim = "fhir.partner";
    public const string KeyHeader = "X-Api-Key";

    public static IServiceCollection AddFhirPartnerAuthentication(this IServiceCollection services)
    {
        services.AddScoped<FhirPartnerAccess>();
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, FhirPartnerAuthenticationHandler>(Scheme, _ => { });
        services.AddAuthorization(options => options.AddPolicy(Policy, policy =>
            policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser().RequireClaim(PartnerClaim)));
        return services;
    }
}
