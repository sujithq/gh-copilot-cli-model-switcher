using System.Text.Json;

namespace CopilotX;

internal static class FoundryImportHelpers
{
    internal static bool IsApplicableAccount(JsonElement item)
    {
        var kind = item.TryGetProperty("kind", out var kindProp) ? kindProp.GetString() ?? string.Empty : string.Empty;
        var endpoint = item.TryGetProperty("endpoint", out var flatEndpointProp) ? flatEndpointProp.GetString() ?? string.Empty : string.Empty;

        if (string.IsNullOrWhiteSpace(endpoint)
            && item.TryGetProperty("properties", out var props)
            && props.TryGetProperty("endpoint", out var endpointProp))
        {
            endpoint = endpointProp.GetString() ?? string.Empty;
        }

        return endpoint.Contains(".openai.azure.com", StringComparison.OrdinalIgnoreCase)
            || endpoint.Contains(".cognitiveservices.azure.com", StringComparison.OrdinalIgnoreCase)
            || kind.Contains("openai", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("AIServices", StringComparison.OrdinalIgnoreCase);
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

    internal static bool IsChatCapableDeployment(JsonElement item)
    {
        if (item.TryGetProperty("properties", out var properties)
            && properties.TryGetProperty("capabilities", out var capabilities)
            && capabilities.ValueKind == JsonValueKind.Object)
        {
            if (capabilities.TryGetProperty("chatCompletion", out var chatProp)
                && string.Equals(chatProp.GetString(), "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (capabilities.TryGetProperty("responses", out var responsesProp)
                && string.Equals(responsesProp.GetString(), "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Fallback when capabilities are absent: exclude known embedding models.
        var modelName = string.Empty;
        if (item.TryGetProperty("properties", out var props)
            && props.TryGetProperty("model", out var model)
            && model.TryGetProperty("name", out var modelNameProp))
        {
            modelName = modelNameProp.GetString() ?? string.Empty;
        }
        else if (item.TryGetProperty("model", out var rootModel)
            && rootModel.TryGetProperty("name", out var rootModelNameProp))
        {
            modelName = rootModelNameProp.GetString() ?? string.Empty;
        }
        else if (item.TryGetProperty("name", out var deploymentNameProp))
        {
            modelName = deploymentNameProp.GetString() ?? string.Empty;
        }

        return !modelName.Contains("embedding", StringComparison.OrdinalIgnoreCase);
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
            // For Azure OpenAI BYOK, COPILOT_MODEL must match deployment name.
            Model = deployment.DeploymentName,
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
