using System.Windows;
using Microsoft.Agents.AI;

namespace POC_LIB_AI;

/// <summary>
/// Example skills implemented in C# and registered with the Microsoft Agent Framework.
/// </summary>
public static class SampleSkills
{
    /// <summary>
    /// Creates a backend-oriented skill that writes a message to the process console log.
    /// </summary>
    public static AgentSkill CreateBackendConsoleSkill()
    {
#pragma warning disable MEAI001 // AgentInlineSkill is experimental in Microsoft Agent Framework.
        return new AgentInlineSkill(
            name: "backend-console-log",
            description: "Write a short informational message to the application's backend console log when explicitly requested.",
            instructions: "Use this skill only when the user explicitly asks you to log a message. Pass a concise, user-provided message to the log-message script.")
            .AddScript(
                name: "log-message",
                method: (string message) =>
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(message);
                    Console.WriteLine($"[Agent skill] {message}");
                    return "The message was written to the application console log.";
                },
                description: "Writes the supplied message to the application's standard output log.");
#pragma warning restore MEAI001
    }

    /// <summary>
    /// Creates a WPF UI skill that displays a message box on the application's UI thread.
    /// </summary>
    public static AgentSkill CreateUiMessageBoxSkill()
    {
#pragma warning disable MEAI001 // AgentInlineSkill is experimental in Microsoft Agent Framework.
        return new AgentInlineSkill(
            name: "ui-message-box",
            description: "Display a message box in the WPF application when the user explicitly requests a popup.",
            instructions: "Use this skill only when the user explicitly asks to show a popup or message box. Pass concise text to the show-message script. Do not use it for ordinary replies.")
            .AddScript(
                name: "show-message",
                method: (string message) =>
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(message);

                    Application application = Application.Current
                        ?? throw new InvalidOperationException("There is no active WPF Application.");

                    application.Dispatcher.Invoke(() =>
                        MessageBox.Show(
                            application.MainWindow,
                            message,
                            "AI assistant",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information));

                    return "The message box was displayed.";
                },
                description: "Displays the supplied text in a WPF message box on the UI thread.");
#pragma warning restore MEAI001
    }
}
