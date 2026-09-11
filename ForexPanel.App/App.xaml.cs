using System;
using System.Windows;
using ForexPanel.App.ChartLayout;

namespace ForexPanel.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                if (MainWindow != null)
                    CandleSymbolWidthSynchronizer.Attach(MainWindow);
            }));
    }
}
