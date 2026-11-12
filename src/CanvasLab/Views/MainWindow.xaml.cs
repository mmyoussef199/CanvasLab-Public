using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CanvasLab.Utilities;
using CanvasLab.ViewModels;

namespace CanvasLab.Views;

public partial class MainWindow : Window
{
    private Point _panStart;
    private double _startX, _startY;
    private bool _panCandidate;
    private bool _panning;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    private void Window_Loaded(object sender, RoutedEventArgs e) => UpdateViewport();
    private void WorkspaceHost_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateViewport();

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsColorPickingActive))
            ImageScrollViewer.Cursor = ViewModel.IsColorPickingActive ? Cursors.Cross : Cursors.Arrow;
    }

    private void UpdateViewport()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        ViewModel.UpdateViewport(
            ImageScrollViewer.ViewportWidth > 0 ? ImageScrollViewer.ViewportWidth : WorkspaceHost.ActualWidth,
            ImageScrollViewer.ViewportHeight > 0 ? ImageScrollViewer.ViewportHeight : WorkspaceHost.ActualHeight,
            dpi.DpiScaleX, dpi.DpiScaleY);
    }

    private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || !ViewModel.HasImage) return;
        if (e.Delta > 0) ViewModel.RequestZoomIn(); else ViewModel.RequestZoomOut();
        e.Handled = true;
    }

    private void ImageScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!ViewModel.HasImage) return;
        var canPan = ImageScrollViewer.ScrollableWidth > 0 || ImageScrollViewer.ScrollableHeight > 0;
        if (!ViewModel.IsColorPickingActive && !canPan) return;

        _panCandidate = true;
        _panning = false;
        _panStart = e.GetPosition(ImageScrollViewer);
        _startX = ImageScrollViewer.HorizontalOffset;
        _startY = ImageScrollViewer.VerticalOffset;
        ImageScrollViewer.CaptureMouse();
        e.Handled = true;
    }

    private void ImageScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_panCandidate || e.LeftButton != MouseButtonState.Pressed) return;
        var point = e.GetPosition(ImageScrollViewer);
        var canPan = ImageScrollViewer.ScrollableWidth > 0 || ImageScrollViewer.ScrollableHeight > 0;
        if (!_panning && canPan && (Math.Abs(point.X - _panStart.X) > 3 || Math.Abs(point.Y - _panStart.Y) > 3))
            _panning = true;
        if (!_panning) return;

        ImageScrollViewer.Cursor = Cursors.Hand;
        ImageScrollViewer.ScrollToHorizontalOffset(_startX - (point.X - _panStart.X));
        ImageScrollViewer.ScrollToVerticalOffset(_startY - (point.Y - _panStart.Y));
        e.Handled = true;
    }

    private void ImageScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_panCandidate) return;
        var shouldPick = !_panning && ViewModel.IsColorPickingActive && ViewModel.Document is not null;
        var imagePoint = shouldPick ? e.GetPosition(ImageView) : default;

        _panCandidate = false;
        _panning = false;
        ImageScrollViewer.ReleaseMouseCapture();
        ImageScrollViewer.Cursor = ViewModel.IsColorPickingActive ? Cursors.Cross : Cursors.Arrow;
        e.Handled = true;

        if (!shouldPick || ViewModel.Document is null) return;
        var mapped = ImageCoordinateMapper.MapToPixel(imagePoint.X, imagePoint.Y,
            ImageView.ActualWidth, ImageView.ActualHeight,
            ViewModel.Document.PixelWidth, ViewModel.Document.PixelHeight);
        if (mapped is { } pixel) ViewModel.SelectImagePixel(pixel.X, pixel.Y);
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => Close();

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e) =>
        new AboutDialog { Owner = this }.ShowDialog();

    private void HowToUseMenuItem_Click(object sender, RoutedEventArgs e) =>
        new HowToUseDialog { Owner = this }.ShowDialog();

    private void GitHubProjectMenuItem_Click(object sender, RoutedEventArgs e) => OpenGitHubProject();

    internal void OpenGitHubProject()
    {
        try { ExternalLinks.OpenGitHubProject(); }
        catch
        {
            MessageBox.Show(this, "CanvasLab could not open the web browser.", "CanvasLab", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
