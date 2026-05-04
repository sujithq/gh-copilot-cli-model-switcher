namespace CopilotX;

internal class FoundryAccount
{
    public string Name { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
}

internal class FoundryDeployment
{
    public string DeploymentName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public int? SuggestedMaxOutputTokens { get; set; }
    public int? SuggestedMaxPromptTokens { get; set; }
    public string SuggestedMaxOutputTokensSource { get; set; } = "none";
    public string SuggestedMaxPromptTokensSource { get; set; } = "none";
    public int? SuggestedTpm { get; set; }
    public int? SuggestedRpm { get; set; }
}
