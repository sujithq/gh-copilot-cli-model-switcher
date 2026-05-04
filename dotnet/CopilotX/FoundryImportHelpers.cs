using System.Text.Json;

namespace CopilotX;

internal static class FoundryImportHelpers
{
    internal static string BuildBaseProfileName(string accountName, string deploymentName)
    {
        return $"foundry-{SanitizeProfilePart(accountName)}-{SanitizeProfilePart(deploymentName)}";
    }

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

        var (suggestedTpm, suggestedRpm) = ExtractRateLimits(item);
        var (suggestedMaxOutputTokens, suggestedMaxPromptTokens, outputSource, promptSource) =
            ExtractSuggestedTokenLimits(item, modelName, deploymentName);

        if (!suggestedMaxPromptTokens.HasValue && suggestedTpm.HasValue)
        {
            suggestedMaxPromptTokens = suggestedTpm;
            promptSource = "rate-limit-token";
        }

        return new FoundryDeployment
        {
            DeploymentName = deploymentName,
            ModelName = modelName,
            ModelVersion = modelVersion,
            SuggestedMaxOutputTokens = suggestedMaxOutputTokens,
            SuggestedMaxPromptTokens = suggestedMaxPromptTokens,
            SuggestedMaxOutputTokensSource = outputSource,
            SuggestedMaxPromptTokensSource = promptSource,
            SuggestedTpm = suggestedTpm,
            SuggestedRpm = suggestedRpm
        };
    }

    private static (int? MaxOutputTokens, int? MaxPromptTokens, string OutputSource, string PromptSource) ExtractSuggestedTokenLimits(
        JsonElement deployment,
        string modelName,
        string deploymentName)
    {
        var maxOutputTokens = TryGetFirstPositiveInt(deployment,
            "maxOutputTokens",
            "maxCompletionTokens",
            "completionTokenLimit",
            "outputTokenLimit",
            "max_output_tokens");

        var maxPromptTokens = TryGetFirstPositiveInt(deployment,
            "maxPromptTokens",
            "maxInputTokens",
            "inputTokenLimit",
            "promptTokenLimit",
            "contextWindow",
            "maxContextTokens",
            "max_context_tokens",
            "max_prompt_tokens");

        if (maxOutputTokens.HasValue || maxPromptTokens.HasValue)
        {
            return (maxOutputTokens, maxPromptTokens,
                maxOutputTokens.HasValue ? "metadata" : "none",
                maxPromptTokens.HasValue ? "metadata" : "none");
        }

        var (heuristicOutputTokens, heuristicPromptTokens) = SuggestTokenLimitsFromModelName(
            string.IsNullOrWhiteSpace(modelName) ? deploymentName : modelName);

        if (heuristicOutputTokens.HasValue || heuristicPromptTokens.HasValue)
        {
            return (heuristicOutputTokens, heuristicPromptTokens,
                heuristicOutputTokens.HasValue ? "model-family" : "none",
                heuristicPromptTokens.HasValue ? "model-family" : "none");
        }

        return (null, null, "none", "none");
    }

    private static (int? Tpm, int? Rpm) ExtractRateLimits(JsonElement deployment)
    {
        if (!deployment.TryGetProperty("properties", out var properties)
            || !properties.TryGetProperty("rateLimits", out var rateLimits)
            || rateLimits.ValueKind != JsonValueKind.Array)
        {
            return (null, null);
        }

        int? tpm = null;
        int? rpm = null;

        foreach (var rateLimit in rateLimits.EnumerateArray())
        {
            if (!rateLimit.TryGetProperty("key", out var keyProp))
            {
                continue;
            }

            var key = keyProp.GetString() ?? string.Empty;
            if (!rateLimit.TryGetProperty("count", out var countProp)
                || !TryParsePositiveInt(countProp, out var count))
            {
                continue;
            }

            var renewalSeconds = 60;
            if (rateLimit.TryGetProperty("renewalPeriod", out var renewalProp)
                && TryParsePositiveInt(renewalProp, out var parsedRenewal))
            {
                renewalSeconds = parsedRenewal;
            }

            var perMinute = (int)Math.Round(count * (60.0 / renewalSeconds));

            if (key.Equals("token", StringComparison.OrdinalIgnoreCase))
            {
                tpm = perMinute;
            }
            else if (key.Equals("request", StringComparison.OrdinalIgnoreCase))
            {
                rpm = perMinute;
            }
        }

        return (tpm, rpm);
    }

    private static (int? MaxOutputTokens, int? MaxPromptTokens) SuggestTokenLimitsFromModelName(string modelName)
    {
        var normalized = (modelName ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized))
        {
            return (null, null);
        }

        if (normalized.Contains("o1") || normalized.Contains("o3"))
        {
            return (16384, 128000);
        }

        if (normalized.Contains("mini"))
        {
            return (4096, 64000);
        }

        if (normalized.Contains("gpt-4.1")
            || normalized.Contains("gpt-4o")
            || normalized.Contains("gpt-5")
            || normalized.Contains("kimi"))
        {
            return (8192, 128000);
        }

        return (null, null);
    }

    private static int? TryGetFirstPositiveInt(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetPositiveIntRecursive(root, propertyName, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool TryGetPositiveIntRecursive(JsonElement element, string propertyName, out int value)
    {
        value = 0;

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                    && TryParsePositiveInt(property.Value, out value))
                {
                    return true;
                }

                if (TryGetPositiveIntRecursive(property.Value, propertyName, out value))
                {
                    return true;
                }
            }

            return false;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                if (TryGetPositiveIntRecursive(child, propertyName, out value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryParsePositiveInt(JsonElement valueElement, out int value)
    {
        value = 0;

        if (valueElement.ValueKind == JsonValueKind.Number)
        {
            if (valueElement.TryGetInt32(out var parsed) && parsed > 0)
            {
                value = parsed;
                return true;
            }

            if (valueElement.TryGetInt64(out var parsedLong)
                && parsedLong > 0
                && parsedLong <= int.MaxValue)
            {
                value = (int)parsedLong;
                return true;
            }

            return false;
        }

        if (valueElement.ValueKind == JsonValueKind.String)
        {
            var raw = valueElement.GetString();
            if (int.TryParse(raw, out var parsed) && parsed > 0)
            {
                value = parsed;
                return true;
            }
        }

        return false;
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

        return !modelName.Contains("embed", StringComparison.OrdinalIgnoreCase);
    }

    internal static string BuildUniqueProfileName(string accountName, string deploymentName, IEnumerable<string> existingNames)
    {
        var baseName = BuildBaseProfileName(accountName, deploymentName);
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

    internal static Profile BuildImportedProfile(
        string accountName,
        string endpoint,
        FoundryDeployment deployment,
        IEnumerable<string> existingNames,
        int? maxOutputTokens = null,
        int? maxPromptTokens = null)
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
            TokenScope = "https://cognitiveservices.azure.com/.default",
            MaxOutputTokens = maxOutputTokens,
            MaxPromptTokens = maxPromptTokens
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
