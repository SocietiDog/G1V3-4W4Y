using System;
using System.Threading;
using System.Windows;
using Gw2Giveaway.Services;

namespace Gw2Giveaway
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private Mutex? _singleInstanceMutex;

        private void App_Startup(object sender, StartupEventArgs e)
        {
            AppDataPaths.EnsureInitializedAndMigrateLegacyIfNeeded();

            AppLogger.PruneOldLogs(30);
            AppLogger.LogInfo("Startup.Paths", $"BaseFolder={AppDataPaths.BaseFolder}");
            AppLogger.LogInfo("Startup.Paths", $"DataFolder={AppDataPaths.DataFolder}");
            AppLogger.LogInfo("Startup.Paths", $"SettingsFile={AppDataPaths.SettingsFile}");
            AppLogger.LogInfo("Startup.Paths", $"BankFile={AppDataPaths.BankFile}");
            AppLogger.LogInfo("Startup.Paths", $"ViewersDbFile={AppDataPaths.ViewersDbFile}");
            AppLogger.LogInfo("Startup.Paths", $"ItemsCacheFile={AppDataPaths.ItemsCacheFile}");
            AppLogger.LogInfo("Startup.Paths", $"LogsFolder={AppDataPaths.LogsFolder}");

            bool createdNew;
            _singleInstanceMutex = new Mutex(initiallyOwned: true, name: @"Global\Gw2Giveaway_SingleInstance", createdNew: out createdNew);

            if (!createdNew)
            {
                Shutdown();
                return;
            }

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
    }

}
