using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NexMed360.Application.Fhir;

namespace NexMed360.Infrastructure.Fhir;

public sealed class FhirPartnerAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IOptionsSnapshot<FhirOptions> configuration, FhirPartnerAccess access)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        FhirOptions settings;
        try { settings = configuration.Value; }
        catch (OptionsValidationException)
        {
            Logger.LogWarning("FHIR partner settings are invalid.");
            return Rejected();
        }
        if (!settings.Enabled || !Request.IsHttps) return Rejected();
        var values = Request.Headers[FhirPartnerAuthentication.KeyHeader];
        if (values.Count == 0) return Task.FromResult(AuthenticateResult.NoResult());
        var key = values.Count == 1 ? values[0] : null;
        if (key == null || key.Length < 32 || key.Length > 512) return Rejected();

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        FhirPartnerOptions? matched = null;
        foreach (var partner in settings.Partners.Where(partner => partner.Enabled))
        {
            byte[] expectedHash;
            try { expectedHash = Convert.FromHexString(partner.IncomingApiKeyHash); }
            catch (FormatException) { return Rejected(); }
            if (!CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash)) continue;
            if (matched != null) return Rejected();
            matched = partner;
        }
        if (matched == null) return Rejected();

        // Organization scope and operations come from trusted settings, not request headers or portal claims.
        access.Authenticate(matched.Id, matched.LocalOrganizationId, matched.RemoteOrganizationId, matched.AllowedOperations);
        var identity = new ClaimsIdentity([new Claim(FhirPartnerAuthentication.PartnerClaim, matched.Id)], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers.WWWAuthenticate = "ApiKey";
        Response.Headers.CacheControl = "no-store";
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        Response.Headers.CacheControl = "no-store";
        return Task.CompletedTask;
    }

    private static Task<AuthenticateResult> Rejected() => Task.FromResult(AuthenticateResult.Fail("Partner authentication failed."));
}
