# POC_LIB_AI

Petite bibliothèque WPF utilisant WPF-UI et Microsoft Agent Framework. Elle comprend un wrapper pour un agent Ollama local, des exemples de compétences définies dans des fichiers et en C#, ainsi qu'un panneau de discussion d'assistant intégrable à la demande dans une fenêtre.

## Ajouter un assistant conversationnel à une fenêtre

Créez votre agent comme d'habitude, puis activez l'assistant pour les fenêtres souhaitées. Le bouton de l'assistant et le panneau de discussion latéral sont ajoutés à la zone cliente de la fenêtre :

```csharp
using POC_LIB_AI;

var localAgent = new OllamaAgent("http://localhost:11434", "qwen");
WindowAssistant.SetAgent(MainWindow, localAgent.Agent);
WindowAssistant.SetIsEnabled(MainWindow, true);
```

Conservez `localAgent` tant que la fenêtre l'utilise et libérez-le lorsque l'application n'en a plus besoin. Définissez `IsEnabled` sur `false` pour retirer le panneau.

Cette solution fonctionne avec les fenêtres WPF ordinaires, sans modifier leur barre de titre. Les boutons natifs Windows de réduction, d'agrandissement et de fermeture ne peuvent pas être étendus par un contrôle WPF standard ; le bouton de discussion est donc placé dans la zone cliente de la fenêtre.

Le panneau de discussion utilise l'`AIAgent` Microsoft configuré. Pour le moment, chaque réponse de l'agent est envoyée comme une requête indépendante ; la mémoire des conversations pourra être ajoutée si l'application hôte gère un fil de discussion.

## Agent Ollama local

Démarrez Ollama et vérifiez que le modèle souhaité est installé (par exemple, `ollama pull qwen`). Créez ensuite l'agent :

```csharp
using POC_LIB_AI;

using var localAgent = new OllamaAgent(
    url: "http://localhost:11434",
    modelName: "qwen");

var response = await localAgent.Agent.RunAsync("Say hello in French.");
Console.WriteLine(response.Text);
```

## Découvrir les compétences C# avec `[CustomSkill]`

Ajoutez l'attribut `[CustomSkill("description")]` à une classe concrète dérivée de `AgentSkill`. La classe doit disposer d'un constructeur sans paramètre. La description de l'attribut doit correspondre exactement à `AgentSkill.Frontmatter.Description` ; les compétences sont alors découvertes dans l'assembly fourni à l'agent.

Les compétences d'exemple du dépôt sont imbriquées dans `SampleSkills` :

```csharp
using POC_LIB_AI;

using var localAgent = new OllamaAgent(
    url: "http://localhost:11434",
    modelName: "qwen",
    customSkillAssemblies: [typeof(SampleSkills).Assembly]);

var response = await localAgent.Agent.RunAsync(
    "Show a popup that says Hello from the assistant.");
```

L'assembly d'exemple contient deux compétences annotées :

- `SampleSkills.BackendConsole` fournit le script `log-message`, qui écrit dans `Console.WriteLine` lorsque l'utilisateur le demande explicitement.
- `SampleSkills.UiMessageBox` fournit le script `show-message`, qui affiche une boîte de dialogue WPF sur le répartiteur de l'application lorsque l'utilisateur le demande explicitement.

### Compétences provenant d'un autre projet ou d'une bibliothèque

Référencez le projet ou le package qui définit la compétence personnalisée, puis transmettez l'assembly correspondant. La classe de compétence externe doit être publique, concrète et dérivée de `AgentSkill` (généralement via `AgentClassSkill<T>`). Elle doit aussi respecter les exigences de l'API Microsoft Agent Framework utilisée par le projet.

```csharp
using POC_LIB_AI;
using MyCustomSkills;

using var localAgent = new OllamaAgent(
    url: "http://localhost:11434",
    modelName: "qwen",
    customSkillAssemblies: [typeof(CustomerLookupSkill).Assembly]);
```

Une compétence dans le projet référencé peut ressembler à ceci :

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
        // Appelez ici le service propre à l'application.
        return $"Customer lookup requested for {customerId}.";
    }
}
```

Le projet de compétences personnalisées doit référencer cette bibliothèque ainsi que le package ou l'API Microsoft Agent Framework utilisé par ses classes de compétences. Ne transmettez que des assemblies de confiance : la découverte instancie les types portant l'attribut.

## Compétences définies dans des fichiers

Vous pouvez également fournir des répertoires contenant des compétences Agent Skills. Chaque compétence doit se trouver dans son propre sous-dossier et inclure un fichier `SKILL.md` :

```csharp
using var localAgent = new OllamaAgent(
    url: "http://localhost:11434",
    modelName: "qwen",
    skillFolders: [@"C:\AI\skills", @"C:\project\custom-skills"]);
```

Les scripts des compétences définies dans des fichiers sont exécutés dans des sous-processus locaux et sont désactivés par défaut. Ne les activez que pour des répertoires de compétences de confiance :

```csharp
using var localAgent = new OllamaAgent(
    url: "http://localhost:11434",
    modelName: "qwen",
    skillFolders: [@"C:\AI\skills"],
    enableSkillScripts: true);
```

## Fournir directement des instances de compétences

Pour les compétences utilisant l'injection de dépendances ou ne disposant pas d'un constructeur sans paramètre, créez vous-même les instances et transmettez-les via `skills` au lieu de les découvrir à partir d'un assembly :

```csharp
IEnumerable<AgentSkill> skills = [CreateSkill(myService)];
using var localAgent = new OllamaAgent(
    "http://localhost:11434",
    "qwen",
    skills: skills);
```

Les compétences C# et celles définies dans des fichiers peuvent être utilisées ensemble. Évitez les noms de compétences en double parmi les différentes sources enregistrées.

## Note de sécurité

Les compétences peuvent entraîner des effets de bord. Rédigez des instructions précises et n'incluez que des compétences de confiance. La compétence d'interface fournie en exemple peut ouvrir une boîte de dialogue, et les scripts personnalisés s'exécutent avec les autorisations de l'application.
