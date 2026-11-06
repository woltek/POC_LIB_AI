using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace POC_LIB_AI;

/// <summary>
/// Creates a Microsoft Agent Framework agent backed by a local Ollama model.
/// </summary>
public sealed class OllamaAgent
{
    private readonly List<AgentSkillsProvider> _skillsProviders = [];

    /// <summary>
    /// Creates an agent connected to an Ollama server.
    /// </summary>
    /// <param name="url">Base URL for the Ollama server, for example http://localhost:11434.</param>
    /// <param name="modelName">The installed Ollama model name, for example qwen.</param>
    /// <param name="skillFolders">Optional directories containing Agent Skills (with SKILL.md files).</param>
    /// <param name="enableSkillScripts">
    /// Enables execution of scripts included in file-based skills. Only enable this for trusted skill folders:
    /// scripts run as local subprocesses and are not sandboxed by this class.
    /// </param>
    /// <param name="skills">Optional skills implemented directly in C#.</param>
    public OllamaAgent(
        string url,
        string modelName,
        string[]? skillFolders = null,
        bool enableSkillScripts = false,
        IEnumerable<AgentSkill>? skills = null)
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
                Instructions = "You are a helpful assistant. Use an available skill when it is relevant to the user's request.",
            },
        };

        string[] folders = skillFolders?
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        var contextProviders = new List<AIContextProvider>();

        if (folders.Length > 0)
        {
#pragma warning disable MEAI001 // AgentSkillsProvider is experimental in Microsoft Agent Framework.
            var fileSkillsProvider = new AgentSkillsProvider(
                folders,
                enableSkillScripts ? SubprocessScriptRunner.RunAsync : null);
#pragma warning restore MEAI001
            _skillsProviders.Add(fileSkillsProvider);
            contextProviders.Add(fileSkillsProvider);
        }

        AgentSkill[] inlineSkills = skills?.ToArray() ?? [];
        if (inlineSkills.Length > 0)
        {
#pragma warning disable MEAI001 // AgentSkillsProvider is experimental in Microsoft Agent Framework.
            var inlineSkillsProvider = new AgentSkillsProvider(inlineSkills);
#pragma warning restore MEAI001
            _skillsProviders.Add(inlineSkillsProvider);
            contextProviders.Add(inlineSkillsProvider);
        }

        if (contextProviders.Count > 0)
        {
            options.AIContextProviders = contextProviders;
        }

        Agent = chatClient.AsAIAgent(options);
    }

    /// <summary>
    /// Gets the configured Microsoft Agent Framework agent.
    /// </summary>
    public AIAgent Agent { get; }

    /// <summary>
    /// Disposes the skill providers owned by this wrapper.
    /// </summary>
    public void Dispose()
    {
        foreach (AgentSkillsProvider provider in _skillsProviders)
        {
            provider.Dispose();
        }

        _skillsProviders.Clear();
    }
}
