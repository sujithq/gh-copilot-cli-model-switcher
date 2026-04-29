using System.Text.Json;

namespace CopilotX;

internal static class FoundryImportHelpers
{
    internal static bool IsApplicableAccount(JsonElement item)
    {
        var kind = item.TryGetProperty("kind", out var kindProp) ? kindProp.GetString() ?? string.Empty : string.Empty;
        var endpoint = string.Empty;

        if (item.TryGetProperty("properties", out var props) && props.TryGetProperty("endpoint", out var endpointProp))
        {
            endpoint = endpointProp.GetString() ?? string.Empty;
        }

        return endpoint.Contains(".openai.azure.com", StringComparison.OrdinalIgnoreCase)
            || kind.Contains("openai", StringComparison.OrdinalIgnoreCase);
    }

    internal static FoundryDeployment MapDeployment(JsonElement item)
    {
        var deploymentName = item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty;
        var modelName = deploymentName;
        var modelVersion = string.Empty;

        if (item.TryGetProperty("properties", out var properties)
            && properties.TryGetProperty("model", out var model))
        {
            if (model.TryGetProperty("name", out var modelNameProp))
            {
                modelName = modelNameProp.GetString() ?? modelName;
            }

            if (model.TryGetProperty("version", out var modelVersionProp))
            {
                modelVersion = modelVersionProp.GetString() ?? string.Empty;
            }
        }
        else if (item.TryGetProperty("model", out var rootModel))
        {
            if (rootModel.TryGetProperty("name", out var modelNameProp))
            {
                modelName = modelNameProp.GetString() ?? modelName;
            }

            if (rootModel.TryGetProperty("version", out var modelVersionProp))
            {
                modelVersion = modelVersionProp.GetString() ?? string.Empty;
            }
        }

        return new FoundryDeployment
        {
            DeploymentName = deploymentName,
            ModelName = modelName,
            ModelVersion = modelVersion
        };
    }

    internal static string BuildUniqueProfileName(string accountName, string deploymentName, IEnumerable<string> existingNames)
    {
        var baseName = $"foundry-{SanitizeProfilePart(accountName)}-{SanitizeProfilePart(deploymentName)}";
        var taken = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        if (!taken.Contains(baseName))
        {
            return baseName;
        }

        var counter = 2;
        while (taken.Contains($"{baseName}-{counter}"))
        {
            counter += 1;
        }

        return $"{baseName}-{counter}";
    }

    internal static Profile BuildImportedProfile(string accountName, string endpoint, FoundryDeployment deployment, IEnumerable<string> existingNames)
    {
        var normalizedEndpoint = (string.IsNullOrWhiteSpace(endpoint)
            ? $"https://{accountName}.openai.azure.com"
            : endpoint).TrimEnd('/');

        return new Profile
        {
            Name = BuildUniqueProfileName(accountName, deployment.DeploymentName, existingNames),
            Type = "byok",
            BaseUrl = $"{normalizedEndpoint}/openai/deployments/{deployment.DeploymentName}",
            Model = deployment.ModelName,
            ProviderType = "azure",
            AzureCliToken = "auto",
            TokenScope = "https://cognitiveservices.azure.com/.default"
        };
    }

    internal static string SanitizeProfilePart(string value)
    {
        var chars = value
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' ? ch : '-')
            .ToArray();

        var normalized = new string(chars);
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return normalized.Trim('-');
    }
}
