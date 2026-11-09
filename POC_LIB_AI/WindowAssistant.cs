using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Agents.AI;

namespace POC_LIB_AI;

/// <summary>
/// Adds an in-window assistant button and right-side chat panel to a WPF window.
/// The button is overlaid at the top-right of the window's client area; native
/// Windows title-bar buttons cannot be extended without opting into custom chrome.
/// </summary>
public static class WindowAssistant
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(WindowAssistant),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty AgentProperty = DependencyProperty.RegisterAttached(
        "Agent",
        typeof(AIAgent),
        typeof(WindowAssistant),
        new PropertyMetadata(null, OnAgentChanged));

    private static readonly DependencyProperty HostProperty = DependencyProperty.RegisterAttached(
        "Host",
        typeof(AssistantHost),
        typeof(WindowAssistant));

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetAgent(DependencyObject element, AIAgent? value) => element.SetValue(AgentProperty, value);
    public static AIAgent? GetAgent(DependencyObject element) => (AIAgent?)element.GetValue(AgentProperty);

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not Window window)
        {
            throw new InvalidOperationException("WindowAssistant.IsEnabled can only be set on a Window.");
        }

        if ((bool)args.NewValue)
        {
            if (window.GetValue(HostProperty) is AssistantHost)
            {
                return;
            }

            var host = new AssistantHost(window, GetAgent(window));
            window.SetValue(HostProperty, host);
            host.Attach();
        }
        else if (window.GetValue(HostProperty) is AssistantHost host)
        {
            host.Detach();
            window.ClearValue(HostProperty);
        }
    }

    private static void OnAgentChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject.GetValue(HostProperty) is AssistantHost host)
        {
            host.Agent = args.NewValue as AIAgent;
        }
    }

    private sealed class AssistantHost
    {
        private readonly Window _window;
        private readonly Grid _overlay;
        private readonly UIElement _originalContent;
        private readonly Button _toggleButton;
        private readonly Border _chatPanel;
        private readonly StackPanel _messages;
        private readonly ScrollViewer _scrollViewer;
        private readonly TextBox _input;
        private bool _isSending;

        public AssistantHost(Window window, AIAgent? agent)
        {
            _window = window;
            Agent = agent;

            _originalContent = window.Content as UIElement
                ?? new ContentPresenter { Content = window.Content };

            _overlay = new Grid();
            _toggleButton = new Button
            {
                Content = "Chat",
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 8, 12, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                ToolTip = "Open assistant chat",
                MinWidth = 64,
            };
            _toggleButton.Click += (_, _) => TogglePanel();

            _messages = new StackPanel { Margin = new Thickness(12) };
            _scrollViewer = new ScrollViewer
            {
                Content = _messages,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };

            _input = new TextBox
            {
                MinHeight = 34,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 8, 0),
            };
            _input.KeyDown += OnInputKeyDown;

            var sendButton = new Button
            {
                Content = "Send",
                Padding = new Thickness(12, 5, 12, 5),
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            sendButton.Click += async (_, _) => await SendAsync();

            var composer = new DockPanel { Margin = new Thickness(12) };
            DockPanel.SetDock(sendButton, Dock.Right);
            composer.Children.Add(sendButton);
            composer.Children.Add(_input);

            var closeButton = new Button
            {
                Content = "×",
                FontSize = 20,
                Padding = new Thickness(8, 0, 8, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                ToolTip = "Close assistant chat",
            };
            closeButton.Click += (_, _) => _chatPanel.Visibility = Visibility.Collapsed;

            var title = new Grid { Margin = new Thickness(14, 10, 8, 8) };
            title.ColumnDefinitions.Add(new ColumnDefinition());
            title.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            title.Children.Add(new TextBlock
            {
                Text = "Assistant",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            });
            Grid.SetColumn(closeButton, 1);
            title.Children.Add(closeButton);

            var panelLayout = new Grid();
            panelLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panelLayout.RowDefinitions.Add(new RowDefinition());
            panelLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panelLayout.Children.Add(title);
            Grid.SetRow(_scrollViewer, 1);
            panelLayout.Children.Add(_scrollViewer);
            Grid.SetRow(composer, 2);
            panelLayout.Children.Add(composer);

            _chatPanel = new Border
            {
                Width = 360,
                Margin = new Thickness(0, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(1, 0, 0, 0),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.18,
                },
                Child = panelLayout,
                Visibility = Visibility.Collapsed,
            };

            _overlay.Children.Add(_originalContent);
            Panel.SetZIndex(_chatPanel, 1);
            _overlay.Children.Add(_chatPanel);
            Panel.SetZIndex(_toggleButton, 2);
            _overlay.Children.Add(_toggleButton);
        }

        public AIAgent? Agent { get; set; }

        public void Attach()
        {
            _window.Content = _overlay;
        }

        public void Detach()
        {
            _window.Content = _originalContent;
        }

        private void TogglePanel()
        {
            bool open = _chatPanel.Visibility != Visibility.Visible;
            _chatPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            _toggleButton.Content = open ? "Close chat" : "Chat";
            _toggleButton.ToolTip = open ? "Close assistant chat" : "Open assistant chat";
        }

        private async void OnInputKeyDown(object sender, System.Windows.Input.KeyEventArgs args)
        {
            if (args.Key == System.Windows.Input.Key.Enter &&
                !System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
            {
                args.Handled = true;
                await SendAsync();
            }
        }

        private async Task SendAsync()
        {
            string prompt = _input.Text.Trim();
            if (prompt.Length == 0 || _isSending)
            {
                return;
            }

            if (Agent is null)
            {
                AddMessage("Assistant", "No agent is configured for this window.");
                return;
            }

            _input.Clear();
            AddMessage("You", prompt);
            _isSending = true;
            _input.IsEnabled = false;

            try
            {
                var response = await Agent.RunAsync(prompt);
                AddMessage("Assistant", response.Text ?? string.Empty);
            }
            catch (Exception exception)
            {
                AddMessage("Assistant", $"Request failed: {exception.Message}");
            }
            finally
            {
                _isSending = false;
                _input.IsEnabled = true;
                _input.Focus();
            }
        }

        private void AddMessage(string speaker, string text)
        {
            var message = new Border
            {
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(6),
                Background = speaker == "You"
                    ? new SolidColorBrush(Color.FromRgb(235, 243, 255))
                    : new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = speaker, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) },
                        new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
                    },
                },
            };

            _messages.Children.Add(message);
            _scrollViewer.ScrollToEnd();
        }
    }
}
