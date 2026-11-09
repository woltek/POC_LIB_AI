# POC_LIB_AI

A small WPF library project using Microsoft Agent Framework. It includes a local Ollama agent wrapper and examples of file-based and C#-defined skills.

## Local Ollama agent

Start Ollama and make sure the model you want is installed (for example, `ollama pull qwen`). Then create the agent:

```csharp
using POC_LIB_AI;

using var localAgent = new OllamaAgent(
    url: "http://localhost:11434",
    modelName: "qwen");

var response = await localAgent.Agent.RunAsync("Say hello in French.");
Console.WriteLine(response.Text);
```

## Discover C# skills with `[CustomSkill]`

Mark a concrete `AgentSkill` class with `[CustomSkill("description")]`. It must have a parameterless constructor. The attribute description must exactly match `AgentSkill.Frontmatter.Description`, and the skill must derive from `AgentSkill`. Pass the assembly explicitly to `OllamaAgent` to discover its marked skills.

The repository's sample skills are nested in `SampleSkills`:

```csharp
using POC_LIB_AI;

using var localAgent = new OllamaAgent(
    url: "http://localhost:11434",
    modelName: "qwen",
    customSkillAssemblies: [typeof(SampleSkills).Assembly]);

var response = await localAgent.Agent.RunAsync(
    "Show a popup that says Hello from the assistant.");
```

The sample assembly contains two marked skills:

- `SampleSkills.BackendConsole` provides a `log-message` script that writes to `Console.WriteLine` when explicitly requested.
- `SampleSkills.UiMessageBox` provides a `show-message` script that displays a WPF `MessageBox` on the application's dispatcher when explicitly requested.

### Skills from another project or library

Reference the project/package that defines the custom skill, then pass that skill's assembly. The external skill class should be public, concrete, derive from `AgentSkill` (commonly `AgentClassSkill<TSkill>`), have a parameterless constructor, and use matching descriptions for `[CustomSkill]` and `Frontmatter`.

```csharp
using POC_LIB_AI;
using MyCustomSkills;

using var localAgent = new OllamaAgent(
    url: "http://localhost:11434",
    modelName: "qwen",
    customSkillAssemblies: [typeof(CustomerLookupSkill).Assembly]);
```

A skill in the referenced project can look like this:

```csharp
using System.ComponentModel;
using Microsoft.Agents.AI;
using POC_LIB_AI;

namespace MyCustomSkills;

[CustomSkill("Look up customer information when the user asks about a customer.")]
public sealed class CustomerLookupSkill : AgentClassSkill<CustomerLookupSkill>
{
    public override AgentSkillFrontmatter Frontmatter { get; } = new(
        "customer-lookup",
        "Look up customer information when the user asks about a customer.");

    protected override string Instructions => "Use this skill for customer lookups.";

    [AgentSkillScript("lookup-customer")]
    [Description("Looks up a customer by identifier.")]
    private static string LookupCustomer(string customerId)
    {
        // Call the application's own service here.
        return $"Customer lookup requested for {customerId}.";
    }
}
```

The custom skill project needs references to this library and the Microsoft Agent Framework package/API used by its skill classes. Pass only assemblies you trust: discovery instantiates marked types.

## File-based skills

You can also provide directories containing Agent Skills. Each skill should be in its own subfolder and include a `SKILL.md` file:

```csharp
using var localAgent = new OllamaAgent(
    url: "http://localhost:11434",
    modelName: "qwen",
    skillFolders: [@"C:\AI\skills", @"C:\project\custom-skills"]);
```

File-based skill scripts run as local subprocesses and are disabled by default. Enable them only for trusted skill folders:

```csharp
using var localAgent = new OllamaAgent(
    url: "http://localhost:11434",
    modelName: "qwen",
    skillFolders: [@"C:\AI\skills"],
    enableSkillScripts: true);
```

## Supply skill instances directly

For dependency-injected skills or skills that do not have a parameterless constructor, create the instances yourself and pass them through `skills` instead of assembly discovery:

```csharp
IEnumerable<AgentSkill> skills = [CreateSkill(myService)];
using var localAgent = new OllamaAgent(
    "http://localhost:11434",
    "qwen",
    skills: skills);
```

C# skills and file-based skills can be used together. Avoid duplicate skill names across registered sources.

## Safety note

Skills can cause side effects. Keep their instructions specific and include only skills you trust. The sample UI skill can open a message box, and custom scripts run with the application's permissions.
