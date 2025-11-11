using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NotificationHelper;

public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _timer;
    private const int DisplayDurationSeconds = 5;

    public ToastWindow(string title, string message, NotificationType type = NotificationType.Info)
    {
        InitializeComponent();
        
        TitleText.Text = title;
        MessageText.Text = message;
        
        // Icon'u type'a göre ayarla
        IconText.Text = type switch
        {
            NotificationType.Success => "✓",
            NotificationType.Warning => "⚠",
            NotificationType.Error => "✕",
            _ => "ℹ"
        };
        
        // Auto-close timer
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(DisplayDurationSeconds)
        };
        _timer.Tick += (s, e) => CloseWithAnimation();
        
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Sol alt köşede pozisyonla
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + 20;
        Top = workArea.Bottom - Height - 20 - (ToastManager.ActiveToasts * (Height + 10));
        
        // Fade-in animasyonu
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
        BeginAnimation(OpacityProperty, fadeIn);
        
        _timer.Start();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseWithAnimation();
    }

    private void CloseWithAnimation()
    {
        _timer.Stop();
        
        // Fade-out animasyonu
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
        fadeOut.Completed += (s, e) =>
        {
            ToastManager.RemoveToast(this);
            Close();
        };
        BeginAnimation(OpacityProperty, fadeOut);
    }
}

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}

public static class ToastManager
{
    private static readonly List<ToastWindow> _toasts = new();
    
    public static int ActiveToasts => _toasts.Count;

    public static void ShowToast(string title, string message, NotificationType type = NotificationType.Info)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var toast = new ToastWindow(title, message, type);
            _toasts.Add(toast);
            toast.Show();
        });
    }

    public static void RemoveToast(ToastWindow toast)
    {
        _toasts.Remove(toast);
        RepositionToasts();
    }

    private static void RepositionToasts()
    {
        var workArea = SystemParameters.WorkArea;
        for (int i = 0; i < _toasts.Count; i++)
        {
            var toast = _toasts[i];
            var targetTop = workArea.Bottom - toast.Height - 20 - (i * (toast.Height + 10));
            
            var animation = new DoubleAnimation(toast.Top, targetTop, TimeSpan.FromMilliseconds(200));
            toast.BeginAnimation(Window.TopProperty, animation);
        }
    }
}
