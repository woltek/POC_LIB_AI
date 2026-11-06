using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace POC_LIB_AI;

/// <summary>
/// Creates a Microsoft Agent Framework agent backed by a local Ollama model.
/// </summary>
public sealed class OllamaAgent
{
    /// <summary>
    /// Creates an agent connected to an Ollama server.
    /// </summary>
    /// <param name="url">Base URL for the Ollama server, for example http://localhost:11434.</param>
    /// <param name="modelName">The installed Ollama model name, for example qwen.</param>
    /// <param name="skillFolders">Optional directories containing Agent Skills (with SKILL.md files).</param>
    /// <param name="enableSkillScripts">
    /// Enables execution of scripts included in skills. Only enable this for trusted skill folders:
    /// scripts run as local subprocesses and are not sandboxed by this class.
    /// </param>
    public OllamaAgent(
        string url,
        string modelName,
        string[]? skillFolders = null,
        bool enableSkillScripts = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("The Ollama URL must be an absolute HTTP or HTTPS URL.", nameof(url));
        }

        IChatClient chatClient = new OllamaApiClient(endpoint, modelName);
        var options = new ChatClientAgentOptions
        {
            Name = "POC_LIB_AI",
            ChatOptions = new ChatOptions
            {
                Instructions = "You are a helpful assistant.",
            },
        };

        string[] folders = skillFolders?
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (folders.Length > 0)
        {
#pragma warning disable MEAI001 // AgentSkillsProvider is experimental in Microsoft Agent Framework.
            var skillsProvider = new AgentSkillsProvider(
                folders,
                enableSkillScripts ? SubprocessScriptRunner.RunAsync : null);
#pragma warning restore MEAI001

            options.AIContextProviders = [skillsProvider];
        }

        Agent = chatClient.AsAIAgent(options);
    }

    /// <summary>
    /// Gets the configured Microsoft Agent Framework agent.
    /// </summary>
    public AIAgent Agent { get; }
}
