using System;
using System.Windows;

namespace UltAssist
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 全局异常处理
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            
            try
            {
                base.OnStartup(e);
                
                // 启动v1.0.0版本的主窗口
                var mainWindow = new MainWindowV2();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用启动失败: {ex.Message}\n\n详细信息:\n{ex}", "启动错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }
        
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show($"未处理的异常: {ex?.Message}\n\n详细信息:\n{ex}", "严重错误", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        
        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"UI线程异常: {e.Exception.Message}\n\n详细信息:\n{e.Exception}", "UI错误", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}

