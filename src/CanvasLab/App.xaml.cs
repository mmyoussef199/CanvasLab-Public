using System.IO;
using System.Windows;
using CanvasLab.Services;
using CanvasLab.ViewModels;
using CanvasLab.Views;

namespace CanvasLab;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var viewModel = new MainWindowViewModel(
            new ImageLoaderService(), new FileDialogService(), new ColorAnalysisService(), new ClipboardService(),
            new ImageOptimizationService());
        MainWindow = new MainWindow(viewModel);
        MainWindow.Show();
        if (e.Args.Length == 1 && File.Exists(e.Args[0]))
            await viewModel.LoadImageAsync(e.Args[0]);
    }
}
