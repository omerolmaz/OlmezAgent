using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace NotificationHelper
{
    public partial class ChatWindow : Window
    {
        private readonly ObservableCollection<ChatMessage> _messages;
        private readonly Action<string>? _sendMessageCallback;

        public ChatWindow(string initialMessage, string sender, Action<string>? sendMessageCallback = null)
        {
            InitializeComponent();

            _messages = new ObservableCollection<ChatMessage>();
            MessagesPanel.ItemsSource = _messages;
            _sendMessageCallback = sendMessageCallback;

            // İlk mesajı ekle
            if (!string.IsNullOrWhiteSpace(initialMessage))
            {
                AddMessage(initialMessage, sender, false);
            }

            MessageInput.Focus();
        }

        public void AddMessage(string message, string sender, bool isMe)
        {
            Dispatcher.Invoke(() =>
            {
                _messages.Add(new ChatMessage
                {
                    Message = message,
                    Sender = sender,
                    Time = DateTime.Now.ToString("HH:mm"),
                    Alignment = isMe ? HorizontalAlignment.Right : HorizontalAlignment.Left
                });

                // Scroll to bottom
                MessageScrollViewer.ScrollToBottom();
            });
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void MessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                SendMessage();
            }
        }

        private void SendMessage()
        {
            var message = MessageInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(message))
                return;

            // Kendi mesajını ekle
            AddMessage(message, "Ben", true);
            MessageInput.Clear();

            // Callback varsa çağır (agent'a geri gönderim için)
            _sendMessageCallback?.Invoke(message);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Kapat yerine gizle
            e.Cancel = true;
            Hide();
        }
    }

    public class ChatMessage
    {
        public string Message { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public HorizontalAlignment Alignment { get; set; }
    }
}
