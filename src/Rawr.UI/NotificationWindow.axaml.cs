using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Rawr.ViewModels;
using System;
using Avalonia.Media;

namespace Rawr;

public partial class NotificationWindow : Window
{
    public NotificationWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
    }

    public NotificationWindow(NotificationViewModel viewModel) : this()
    {
        DataContext = viewModel;
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
        // Simple positioning logic for bottom-right corner of primary screen
        if (Screens.Primary != null)
        {
            var screen = Screens.Primary;
            var workArea = screen.WorkingArea;
            
            // 10px padding from edges
            var x = workArea.Width - this.Width - 10;
            var y = workArea.Height - this.Height - 10;
            
            // Adjust for screen position
            x += workArea.X;
            y += workArea.Y;

            Position = new PixelPoint((int)x, (int)y);
        }
    }
}
