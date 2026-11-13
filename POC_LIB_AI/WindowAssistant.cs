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
        private readonly Button _sendButton;
        private readonly Border _chatPanel;
        private readonly StackPanel _messages;
        private readonly ScrollViewer _scrollViewer;
        private readonly TextBox _input;
        private Border? _thinkingMessage;
        private bool _isSending;

        public AssistantHost(Window window, AIAgent? agent)
        {
            _window = window;
            Agent = agent;

            object? content = window.Content;
            window.Content = null;

            _originalContent = content as UIElement
                ?? new ContentPresenter { Content = content };

            _overlay = new Grid();

            _toggleButton = new Button
            {
                Content = new TextBlock
                {
                    Text = "💬",
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                Width = 42,
                Height = 42,
                Margin = new Thickness(0, 10, 12, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                ToolTip = "Ouvrir l'assistant",
                Background = new SolidColorBrush(Color.FromRgb(49, 117, 255)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(14),
            };
            _toggleButton.Click += (_, _) => TogglePanel();

            _messages = new StackPanel { Margin = new Thickness(12) };
            _scrollViewer = new ScrollViewer
            {
                Content = _messages,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Brushes.Transparent,
            };

            _input = new TextBox
            {
                MinHeight = 38,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(210, 218, 230)),
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
            };
            _input.KeyDown += OnInputKeyDown;

            _sendButton = new Button
            {
                Content = new TextBlock
                {
                    Text = "➤",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                Width = 40,
                Height = 40,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(12),
                ToolTip = "Envoyer",
            };
            _sendButton.Click += async (_, _) => await SendAsync();

            var composer = new DockPanel { Margin = new Thickness(12) };
            DockPanel.SetDock(_sendButton, Dock.Right);
            composer.Children.Add(_sendButton);
            composer.Children.Add(_input);

            var closeButton = new Button
            {
                Content = new TextBlock
                {
                    Text = "✕",
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(92, 99, 112)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                Width = 30,
                Height = 30,
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Fermer l'assistant",
            };
            closeButton.Click += (_, _) => ClosePanel();

            var title = new Grid { Margin = new Thickness(14, 10, 8, 8) };
            title.ColumnDefinitions.Add(new ColumnDefinition());
            title.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            title.Children.Add(new TextBlock
            {
                Text = "Assistant",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(27, 39, 59)),
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
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(1, 0, 0, 0),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 14,
                    ShadowDepth = 0,
                    Opacity = 0.22,
                },
                Child = panelLayout,
                Visibility = Visibility.Collapsed,
            };

            _overlay.Children.Add(_originalContent);
            Panel.SetZIndex(_chatPanel, 1);
            _overlay.Children.Add(_chatPanel);
            Panel.SetZIndex(_toggleButton, 2);
            _overlay.Children.Add(_toggleButton);

            UpdateToggleButtonState(false);
            UpdateSendButtonState();
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
            UpdateToggleButtonState(open);
        }

        private void ClosePanel()
        {
            _chatPanel.Visibility = Visibility.Collapsed;
            UpdateToggleButtonState(false);
        }

        private void UpdateToggleButtonState(bool open)
        {
            _toggleButton.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
            if (open)
            {
                _toggleButton.ToolTip = "Fermer l'assistant";
                _toggleButton.Content = new TextBlock
                {
                    Text = "✕",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
            }
            else
            {
                _toggleButton.ToolTip = "Ouvrir l'assistant";
                _toggleButton.Content = new TextBlock
                {
                    Text = "💬",
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
            }
        }

        private void UpdateSendButtonState()
        {
            _sendButton.IsEnabled = !_isSending;
            _sendButton.Background = _isSending
                ? new SolidColorBrush(Color.FromRgb(156, 163, 175))
                : new SolidColorBrush(Color.FromRgb(34, 197, 94));
            _sendButton.ToolTip = _isSending ? "Réflexion en cours..." : "Envoyer";
            _sendButton.Content = new TextBlock
            {
                Text = _isSending ? "…" : "➤",
                FontSize = _isSending ? 16 : 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
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
                AddMessage("Assistant", "Aucun agent n'est configuré pour cette fenêtre.");
                return;
            }

            _input.Clear();
            AddMessage("Vous", prompt);
            _isSending = true;
            _input.IsEnabled = false;
            UpdateSendButtonState();
            ShowThinkingMessage();

            try
            {
                var response = await Agent.RunAsync(prompt);
                HideThinkingMessage();
                AddMessage("Assistant", response.Text ?? string.Empty);
            }
            catch (Exception exception)
            {
                HideThinkingMessage();
                AddMessage("Assistant", $"Échec de la requête : {exception.Message}");
            }
            finally
            {
                _isSending = false;
                _input.IsEnabled = true;
                UpdateSendButtonState();
                _input.Focus();
            }
        }

        private void ShowThinkingMessage()
        {
            if (_thinkingMessage is not null)
            {
                return;
            }

            var bubble = new Border
            {
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(250, 250, 251)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = "Réflexion en cours...",
                    FontStyle = FontStyles.Italic,
                    Foreground = new SolidColorBrush(Color.FromRgb(90, 98, 108)),
                    TextWrapping = TextWrapping.Wrap,
                },
            };

            _thinkingMessage = bubble;
            _messages.Children.Add(bubble);
            _scrollViewer.ScrollToEnd();
        }

        private void HideThinkingMessage()
        {
            if (_thinkingMessage is null)
            {
                return;
            }

            _messages.Children.Remove(_thinkingMessage);
            _thinkingMessage = null;
        }

        private void AddMessage(string speaker, string text)
        {
            var label = NormalizeSpeaker(speaker);
            var timestamp = DateTime.Now.ToString("dd/MM HH:mm");

            var header = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var speakerText = new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.SemiBold,
                Foreground = speaker == "Assistant"
                    ? new SolidColorBrush(Color.FromRgb(34, 40, 49))
                    : new SolidColorBrush(Color.FromRgb(38, 89, 168)),
            };

            var timestampText = new TextBlock
            {
                Text = timestamp,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 118, 129)),
                Margin = new Thickness(8, 0, 0, 0),
            };

            header.Children.Add(speakerText);
            Grid.SetColumn(timestampText, 1);
            header.Children.Add(timestampText);

            var message = new Border
            {
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(8),
                Background = speaker == "Vous"
                    ? new SolidColorBrush(Color.FromRgb(235, 245, 255))
                    : new SolidColorBrush(Color.FromRgb(245, 247, 250)),
                BorderBrush = speaker == "Vous"
                    ? new SolidColorBrush(Color.FromRgb(201, 224, 255))
                    : new SolidColorBrush(Color.FromRgb(228, 232, 240)),
                BorderThickness = new Thickness(1),
                Child = new StackPanel
                {
                    Children =
                    {
                        header,
                        new TextBlock
                        {
                            Text = text,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.FromRgb(32, 39, 47)),
                        },
                    },
                },
            };

            _messages.Children.Add(message);
            _scrollViewer.ScrollToEnd();
        }

        private static string NormalizeSpeaker(string speaker)
        {
            return speaker switch
            {
                "Vous" => "Vous",
                "Assistant" => "Assistant",
                _ => speaker,
            };
        }
    }
}
