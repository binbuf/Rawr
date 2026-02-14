using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Rawr.ViewModels;

namespace Rawr
{
    public partial class DashboardWindow : Window
    {
        public bool CanClose { get; set; }

        public DashboardWindow()
        {
            InitializeComponent();
        }

        private void OnSettingsClicked(object? sender, RoutedEventArgs e)
        {
            var settingsVm = App.Current.Services?.GetRequiredService<SettingsViewModel>();
            if (settingsVm != null)
            {
                var settingsWindow = new SettingsWindow
                {
                    DataContext = settingsVm
                };
                settingsWindow.ShowDialog(this);
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (!CanClose)
            {
                e.Cancel = true;
                Hide();
            }
            base.OnClosing(e);
        }
    }
}