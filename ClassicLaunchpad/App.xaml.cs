using Microsoft.UI.Xaml;

namespace ClassicLaunchpad
{
    public partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg.Equals("--restore-taskbar", System.StringComparison.OrdinalIgnoreCase))
                {
                    MainWindow.ShowTaskbar();
                    Exit();
                    return;
                }
            }

            m_window = new MainWindow();
            m_window.Activate();
        }

        private Window? m_window;
    }
}
