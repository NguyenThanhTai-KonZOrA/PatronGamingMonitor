using NLog;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PatronGamingMonitor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static Mutex _singleInstanceMutex;
        protected override void OnStartup(StartupEventArgs e)
        {
            const string mutexName = "PatronResponsibleGamingAlert_SingleInstance_Mutex";
            bool isNewInstance = false;

            try
            {
                _singleInstanceMutex = new Mutex(true, mutexName, out isNewInstance);

                if (!isNewInstance)
                {
                    MessageBox.Show(
                        "PatronResponsibleGamingAlert is already running.\nOnly one instance can be open at a time.",
                        "Application Already Running",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    Logger.Warn("Duplicate instance detected. Exiting startup.");
                    Shutdown(); // clean shutdown
                    return;
                }

                Logger.Info("Application starting up...");

                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

                //FreezeMonitor.Start();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "An error occurred during application startup.");
                MessageBox.Show("An unexpected error occurred. Please check the logs for details.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }

            base.OnStartup(e);
        }
        protected override void OnExit(ExitEventArgs e)
        {
            Logger.Info("Application shutting down...");
            LogManager.Shutdown(); // Ensure logs are flushed

            try
            {
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
                _singleInstanceMutex = null;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error releasing single-instance mutex.");
            }

            base.OnExit(e);
        }
    }
}