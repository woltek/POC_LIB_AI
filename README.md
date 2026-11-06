# POC_LIB_AI

A small WPF library project using [WPF-UI](https://github.com/lepoco/wpfui) and Microsoft Agent Framework.

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

You can optionally provide directories containing Agent Skills. Each skill should be in its own subfolder and include a `SKILL.md` file:

```csharp
using var localAgent = new OllamaAgent(
    url: "http://localhost:11434",
    modelName: "qwen",
    skillFolders: [@"C:\AI\skills", @"C:\project\custom-skills"]);
```

Skills written directly in C# can also be supplied. This example registers the two samples below:

```csharp
using var localAgent = new OllamaAgent(
    url: "http://localhost:11434",
    modelName: "qwen",
    skills:
    [
        SampleSkills.CreateBackendConsoleSkill(),
        SampleSkills.CreateUiMessageBoxSkill(),
    ]);

var response = await localAgent.Agent.RunAsync("Show a popup that says Hello from the assistant.");
```

### Backend skill: console log

`SampleSkills.CreateBackendConsoleSkill()` creates a C# skill with a `log-message` script. When the agent calls it, the script writes to `Console.WriteLine`. In a WPF application without an attached console window, standard output may not be visible; use a configured logger or redirect output if you need persistent application logs.

### UI skill: WPF MessageBox

`SampleSkills.CreateUiMessageBoxSkill()` creates a C# skill with a `show-message` script. It dispatches the WPF `MessageBox` call to `Application.Current.Dispatcher`, so it can safely be invoked even if the agent run is not on the UI thread. Call it from a running WPF application with an active `Application` instance.

These skills can trigger side effects. Keep their instructions specific and only include skills you trust. Skill script execution for file-based skills is **not enabled by default**. If you enable it with `enableSkillScripts: true`, those scripts run as local subprocesses and are not sandboxed.
