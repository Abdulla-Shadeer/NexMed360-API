using System.Text.RegularExpressions;
using NexMed360.Application.Fhir;
using Microsoft.Extensions.Options;

namespace NexMed360.Infrastructure.Fhir;

public sealed class FhirOptionsValidator : IValidateOptions<FhirOptions>
{
    public ValidateOptionsResult Validate(string? name, FhirOptions options)
    {
        if (!options.Enabled) return ValidateOptionsResult.Success;
        List<string> errors = [];
        if (!IsHttpsUrl(options.PublicBaseUrl)) errors.Add("Fhir:PublicBaseUrl must be an HTTPS URL without credentials, query, or fragment.");
        if (!Uri.TryCreate(options.IdentifierSystem, UriKind.Absolute, out _)) errors.Add("Fhir:IdentifierSystem must be a stable absolute URI.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keyHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var partner in options.Partners)
        {
            if (!Regex.IsMatch(partner.Id, "^[a-z0-9][a-z0-9-]{0,63}$") || !ids.Add(partner.Id))
                errors.Add("Each FHIR partner needs a unique ID of up to 64 lowercase letters, numbers, or hyphens.");
            if (!partner.Enabled) continue;
            if (string.IsNullOrWhiteSpace(partner.Name) || string.IsNullOrWhiteSpace(partner.Contact))
                errors.Add($"FHIR partner {partner.Id} needs a name and contact.");
            if (!IsHttpsUrl(partner.BaseUrl)) errors.Add($"FHIR partner {partner.Id} needs an HTTPS base URL without credentials, query, or fragment.");
            if (partner.LocalOrganizationId == Guid.Empty || partner.RemoteOrganizationId == Guid.Empty)
                errors.Add($"FHIR partner {partner.Id} needs local and remote organization IDs.");
            if (partner.AllowedOperations.Count == 0 || partner.AllowedOperations.Any(operation => !FhirOperations.All.Contains(operation)))
                errors.Add($"FHIR partner {partner.Id} has missing or unsupported operations.");
            if (!Regex.IsMatch(partner.IncomingApiKeyHash, "^[A-Fa-f0-9]{64}$"))
                errors.Add($"FHIR partner {partner.Id} needs a SHA-256 incoming key hash.");
            else if (!keyHashes.Add(partner.IncomingApiKeyHash))
                errors.Add("Each enabled FHIR partner needs its own incoming API key.");
            if (string.IsNullOrWhiteSpace(partner.OutgoingApiKeySecretName))
                errors.Add($"FHIR partner {partner.Id} needs an outgoing key configuration reference.");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static bool IsHttpsUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
}
