using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Rawr
{
    public partial class MainWindow : Window
    {
        public bool CanClose { get; set; }

        public MainWindow()
        {
            InitializeComponent();
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