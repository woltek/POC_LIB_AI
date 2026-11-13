using System.Reflection;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace POC_LIB_AI;

/// <summary>Creates a Microsoft Agent Framework agent backed by a local Ollama model.</summary>
public sealed class OllamaAgent : IDisposable
{
    private readonly List<AgentSkillsProvider> _skillsProviders = [];
    private bool _disposed;

    /// <summary>Creates an agent connected to an Ollama server.</summary>
    /// <param name="url">Ollama base URL, such as http://localhost:11434.</param>
    /// <param name="modelName">Installed Ollama model name, such as qwen.</param>
    /// <param name="skillFolders">Optional directories containing Agent Skills with SKILL.md files.</param>
    /// <param name="enableSkillScripts">Enables subprocess execution for file-based skill scripts. Only use with trusted skill folders.</param>
    /// <param name="skills">Optional C# skill instances.</param>
    /// <param name="customSkillAssemblies">Optional explicitly selected assemblies to scan for [CustomSkill] classes. Assemblies are not scanned implicitly.</param>
    public OllamaAgent(
        string url,
        string modelName,
        string[]? skillFolders = null,
        bool enableSkillScripts = false,
        IEnumerable<AgentSkill>? skills = null,
        IEnumerable<Assembly>? customSkillAssemblies = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("The Ollama URL must be an absolute HTTP or HTTPS URL.", nameof(url));
        }

        var options = new ChatClientAgentOptions
        {
            Name = "POC_LIB_AI",
            ChatOptions = new ChatOptions
            {
                Instructions = "You are a helpful assistant. Use an available skill when it is relevant to the user's request.",
            },
        };

        var contextProviders = new List<AIContextProvider>();
        string[] folders = skillFolders?
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

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

        var allSkills = skills?.ToList() ?? [];
        if (customSkillAssemblies is not null)
        {
            allSkills.AddRange(CustomSkillDiscovery.DiscoverSkillsFromAssemblies(customSkillAssemblies));
        }

        if (allSkills.Count > 0)
        {
#pragma warning disable MEAI001 // AgentSkillsProvider is experimental in Microsoft Agent Framework.
            var codeSkillsProvider = new AgentSkillsProvider(allSkills);
#pragma warning restore MEAI001
            _skillsProviders.Add(codeSkillsProvider);
            contextProviders.Add(codeSkillsProvider);
        }

        if (contextProviders.Count > 0)
        {
            options.AIContextProviders = contextProviders;
        }

        Agent = new OllamaApiClient(endpoint, modelName).AsAIAgent(options)
            .AsBuilder()
            .UseToolApproval(new ToolApprovalAgentOptions
            {
                AutoApprovalRules =
                [
                    AgentSkillsProvider.AllToolsAutoApprovalRule
                ]
            })
            .Build();
    }

    /// <summary>Gets the configured Microsoft Agent Framework agent.</summary>
    public AIAgent Agent { get; }

    /// <summary>Disposes skill providers created by this wrapper.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (AgentSkillsProvider provider in _skillsProviders)
        {
            provider.Dispose();
        }

        _skillsProviders.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
