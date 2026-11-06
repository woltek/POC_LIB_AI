using System.ComponentModel;
using System.Windows;
using Microsoft.Agents.AI;

namespace POC_LIB_AI;

/// <summary>Example custom skills discovered through the [CustomSkill] class attribute.</summary>
#pragma warning disable MEAI001 // AgentClassSkill and its discovery attributes are experimental in Microsoft Agent Framework.
public static class SampleSkills
{
    /// <summary>A skill that writes an explicitly requested message to the process console.</summary>
    [CustomSkill("Write a short informational message to the application's backend console log when explicitly requested.")]
    public sealed class BackendConsole : AgentClassSkill<BackendConsole>
    {
        public override AgentSkillFrontmatter Frontmatter { get; } = new(
            "backend-console-log",
            "Write a short informational message to the application's backend console log when explicitly requested.");

        protected override string Instructions =>
            "Use this skill only when the user explicitly asks you to log a message. Pass a concise, user-provided message to the log-message script.";

        [AgentSkillScript("log-message")]
        [Description("Writes the supplied message to the application's standard output log.")]
        private static string LogMessage(string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            Console.WriteLine($"[Agent skill] {message}");
            return "The message was written to the application console log.";
        }
    }

    /// <summary>A skill that displays an explicitly requested WPF message box on the UI thread.</summary>
    [CustomSkill("Display a message box in the WPF application when the user explicitly requests a popup.")]
    public sealed class UiMessageBox : AgentClassSkill<UiMessageBox>
    {
        public override AgentSkillFrontmatter Frontmatter { get; } = new(
            "ui-message-box",
            "Display a message box in the WPF application when the user explicitly requests a popup.");

        protected override string Instructions =>
            "Use this skill only when the user explicitly asks to show a popup or message box. Pass concise text to the show-message script. Do not use it for ordinary replies.";

        [AgentSkillScript("show-message")]
        [Description("Displays the supplied text in a WPF message box on the UI thread.")]
        private static string ShowMessage(string message)
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
        }
    }
}
#pragma warning restore MEAI001
