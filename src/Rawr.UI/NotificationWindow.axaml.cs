using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Rawr.ViewModels;
using System;
using Avalonia.Media;
using Rawr.Core.Configuration;

namespace Rawr;

public partial class NotificationWindow : Window
{
    private readonly NotificationConfig _config;

    public NotificationWindow()
    {
        InitializeComponent();
        _config = new NotificationConfig(); // Default for preview/designer
#if DEBUG
        this.AttachDevTools();
#endif
    }

    public NotificationWindow(NotificationViewModel viewModel, NotificationConfig config) : this()
    {
        DataContext = viewModel;
        _config = config;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        PositionWindow();
    }

    private void PositionWindow()
    {
        if (Screens.Primary != null)
        {
            var screen = Screens.Primary;
            var workArea = screen.WorkingArea;
            
            double scaling = screen.Scaling;
            double padding = 10 * scaling;

            // Use the desired width/height if available, otherwise use defaults
            double windowWidth = (double.IsNaN(this.Width) ? 300 : this.Width) * scaling;
            double windowHeight = (double.IsNaN(this.Height) ? 150 : this.Height) * scaling;

            double x = 0;
            double y = 0;

            switch (_config.Position)
            {
                case PopupPosition.BottomRight:
                    x = workArea.X + workArea.Width - windowWidth - padding;
                    y = workArea.Y + workArea.Height - windowHeight - padding;
                    break;
                case PopupPosition.TopRight:
                    x = workArea.X + workArea.Width - windowWidth - padding;
                    y = workArea.Y + padding;
                    break;
                case PopupPosition.TopLeft:
                    x = workArea.X + padding;
                    y = workArea.Y + padding;
                    break;
                case PopupPosition.BottomLeft:
                    x = workArea.X + padding;
                    y = workArea.Y + workArea.Height - windowHeight - padding;
                    break;
            }
            
            Position = new PixelPoint((int)x, (int)y);
        }
    }
}
