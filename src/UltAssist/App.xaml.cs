using System.Windows;

namespace UltAssist
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 启动V2版本的主窗口
            var mainWindow = new MainWindowV2();
            mainWindow.Show();
        }
    }
}

