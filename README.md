# POC_LIB_AI

A small WPF library project using [WPF-UI](https://github.com/lepoco/wpfui) and Microsoft Agent Framework.

## Local Ollama agent

Start Ollama and make sure the model you want is installed (for example, `ollama pull qwen`). Then create the agent:

```csharp
using POC_LIB_AI;

var localAgent = new OllamaAgent(
    url: "http://localhost:11434",
    modelName: "qwen");

var response = await localAgent.Agent.RunAsync("Say hello in French.");
Console.WriteLine(response.Text);
```

You can optionally provide directories containing Agent Skills. Each skill should be in its own subfolder and include a `SKILL.md` file:

```csharp
var localAgent = new OllamaAgent(
    url: "http://localhost:11434",
    modelName: "qwen",
    skillFolders: [@"C:\AI\skills", @"C:\project\custom-skills"]);
```

Skill scripts are **not executed by default**. If you explicitly trust all supplied skill directories and want to allow their scripts to run as local subprocesses, set `enableSkillScripts: true`. This is not a sandbox; only use it with skill folders you trust.
