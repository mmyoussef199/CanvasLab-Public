using System.IO;
using System.Windows.Media.Imaging;
using CanvasLab.Models;
using CanvasLab.Services;
using CanvasLab.Utilities;

namespace CanvasLab.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private const double MinZoom = 10, MaxZoom = 800, MarkerRadius = 7;
    private readonly IImageLoaderService _imageLoader;
    private readonly IFileDialogService _fileDialog;
    private readonly IColorAnalysisService _colorAnalysis;
    private readonly IClipboardService _clipboard;
    private readonly IImageOptimizationService _optimization;
    private CancellationTokenSource? _loadCancellation, _optimizationCancellation, _debounceCancellation;
    private ImageDocument? _document;
    private ImageOptimizationResult? _optimizationResult;
    private PixelColor? _selectedColor;
    private IReadOnlyList<RepresentativeColor> _mainColors = [];
    private bool _isBusy, _isAnalyzingColors, _isOptimizing, _isColorPickingActive, _fitMode = true;
    private bool _keepProportions = true, _showingPreview, _changingDimensions;
    private double _zoomPercentage = 100, _viewportWidth = 800, _viewportHeight = 600, _dpiScaleX = 1, _dpiScaleY = 1;
    private string _statusMessage = "Open a JPEG, PNG, or WebP image to begin.", _targetWidth = "", _targetHeight = "";
    private string? _errorMessage;
    private int _imageQuality = 80;
    private ImageFileFormat _outputFormat = ImageFileFormat.WebP;

    public MainWindowViewModel(IImageLoaderService imageLoader, IFileDialogService fileDialog,
        IColorAnalysisService colorAnalysis, IClipboardService clipboard, IImageOptimizationService? optimization = null)
    {
        _imageLoader = imageLoader; _fileDialog = fileDialog; _colorAnalysis = colorAnalysis; _clipboard = clipboard;
        _optimization = optimization ?? new ImageOptimizationService();
        OpenImageCommand = new AsyncRelayCommand(OpenImageAsync, () => !IsBusy);
        FitToWindowCommand = new RelayCommand(FitToWindow, () => HasImage);
        ActualSizeCommand = new RelayCommand(ShowActualSize, () => HasImage);
        ZoomInCommand = new RelayCommand(ZoomIn, () => HasImage && ZoomPercentage < MaxZoom);
        ZoomOutCommand = new RelayCommand(ZoomOut, () => HasImage && ZoomPercentage > MinZoom);
        PickColorsCommand = new RelayCommand(ToggleColorPicking, () => HasImage && !ShowingPreview && !IsBusy);
        CopySelectedColorCommand = new RelayCommand(CopySelectedColor, () => HasSelectedColor);
        CopyPaletteColorCommand = new RelayCommand<RepresentativeColor>(CopyPaletteColor);
        MakeImageSmallerCommand = new AsyncRelayCommand(OptimizeNowAsync, () => CanOptimize);
        PreviewCommand = new RelayCommand(TogglePreview, () => HasOptimizationResult);
        SaveImageCommand = new AsyncRelayCommand(SaveImageAsync, () => HasOptimizationResult && !IsOptimizing);
        ResetOptimizationCommand = new RelayCommand(ResetOptimization, () => HasImage);
        DismissErrorCommand = new RelayCommand(() => ErrorMessage = null, () => HasError);
    }

    public AsyncRelayCommand OpenImageCommand { get; }
    public RelayCommand FitToWindowCommand { get; }
    public RelayCommand ActualSizeCommand { get; }
    public RelayCommand ZoomInCommand { get; }
    public RelayCommand ZoomOutCommand { get; }
    public RelayCommand PickColorsCommand { get; }
    public RelayCommand CopySelectedColorCommand { get; }
    public RelayCommand<RepresentativeColor> CopyPaletteColorCommand { get; }
    public AsyncRelayCommand MakeImageSmallerCommand { get; }
    public RelayCommand PreviewCommand { get; }
    public AsyncRelayCommand SaveImageCommand { get; }
    public RelayCommand ResetOptimizationCommand { get; }
    public RelayCommand DismissErrorCommand { get; }
    public IReadOnlyList<ImageFileFormat> OutputFormats { get; } = [ImageFileFormat.Jpeg, ImageFileFormat.Png, ImageFileFormat.WebP];

    public ImageDocument? Document
    {
        get => _document;
        private set
        {
            if (!SetProperty(ref _document, value)) return;
            NotifyDocumentAndGeometry(); RaiseCommandStates();
        }
    }
    public bool HasImage => Document is not null;
    public BitmapSource? ImageSource => ShowingPreview ? OptimizationResult?.PreviewImage : Document?.ImageSource;
    public int CurrentPixelWidth => ShowingPreview ? OptimizationResult?.PixelWidth ?? 0 : Document?.PixelWidth ?? 0;
    public int CurrentPixelHeight => ShowingPreview ? OptimizationResult?.PixelHeight ?? 0 : Document?.PixelHeight ?? 0;
    public string FileName => Document?.FileName ?? "No image open";
    public string FilePath => Document?.FilePath ?? "Choose Open Image to select a JPEG, PNG, or WebP file.";
    public string DimensionsText => !HasImage ? "—" : $"{CurrentPixelWidth:N0} × {CurrentPixelHeight:N0} px";
    public string FileSizeText => Document is null ? "—" : FileSizeFormatter.Format(Document.FileSizeBytes);
    public string SourceFormatText => Document?.SourceFormat.ToString() ?? "—";
    public string ZoomText => HasImage ? $"{ZoomPercentage:0}%" : "—";
    public double DisplayImageWidth => CurrentPixelWidth * ZoomPercentage / 100d / _dpiScaleX;
    public double DisplayImageHeight => CurrentPixelHeight * ZoomPercentage / 100d / _dpiScaleY;

    public PixelColor? SelectedColor { get => _selectedColor; private set { if (SetProperty(ref _selectedColor, value)) { OnPropertyChanged(nameof(HasSelectedColor)); NotifyMarker(); CopySelectedColorCommand.RaiseCanExecuteChanged(); } } }
    public bool HasSelectedColor => SelectedColor is not null && !ShowingPreview;
    public double MarkerLeft => !HasSelectedColor || Document is null ? 0 : (SelectedColor!.X + .5) * DisplayImageWidth / Document.PixelWidth - MarkerRadius;
    public double MarkerTop => !HasSelectedColor || Document is null ? 0 : (SelectedColor!.Y + .5) * DisplayImageHeight / Document.PixelHeight - MarkerRadius;
    public IReadOnlyList<RepresentativeColor> MainColors { get => _mainColors; private set { if (SetProperty(ref _mainColors, value)) { OnPropertyChanged(nameof(HasMainColors)); OnPropertyChanged(nameof(ColorsFoundText)); } } }
    public bool HasMainColors => MainColors.Count > 0;
    public string ColorsFoundText => $"{MainColors.Count} Colors Found";
    public bool IsColorPickingActive { get => _isColorPickingActive; private set => SetProperty(ref _isColorPickingActive, value); }
    public bool IsAnalyzingColors { get => _isAnalyzingColors; private set => SetProperty(ref _isAnalyzingColors, value); }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) RaiseCommandStates(); } }
    public bool IsOptimizing { get => _isOptimizing; private set { if (SetProperty(ref _isOptimizing, value)) { RaiseCommandStates(); OnPropertyChanged(nameof(OptimizationStatus)); } } }
    public string OptimizationStatus => IsOptimizing ? "Creating preview…" : HasOptimizationResult ? "Preview ready" : "Choose settings, then make a smaller image.";
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string? ErrorMessage { get => _errorMessage; private set { if (SetProperty(ref _errorMessage, value)) { OnPropertyChanged(nameof(HasError)); DismissErrorCommand.RaiseCanExecuteChanged(); } } }
    public bool HasError => ErrorMessage is not null;

    public ImageFileFormat OutputFormat { get => _outputFormat; set { if (SetProperty(ref _outputFormat, value)) { OnPropertyChanged(nameof(IsQualityEnabled)); InvalidateOptimization(); } } }
    public bool IsQualityEnabled => OutputFormat != ImageFileFormat.Png;
    public int ImageQuality { get => _imageQuality; set { if (SetProperty(ref _imageQuality, Math.Clamp(value, 1, 100))) InvalidateOptimization(); } }
    public bool KeepProportions { get => _keepProportions; set => SetProperty(ref _keepProportions, value); }
    public string TargetWidth { get => _targetWidth; set { if (SetProperty(ref _targetWidth, value)) DimensionChanged(true); } }
    public string TargetHeight { get => _targetHeight; set { if (SetProperty(ref _targetHeight, value)) DimensionChanged(false); } }
    public bool HasValidDimensions => int.TryParse(TargetWidth, out var w) && int.TryParse(TargetHeight, out var h) && w is > 0 and <= 50_000 && h is > 0 and <= 50_000 && (long)w * h <= 100_000_000;
    public bool CanOptimize => HasImage && HasValidDimensions && !IsOptimizing;

    public ImageOptimizationResult? OptimizationResult { get => _optimizationResult; private set { if (SetProperty(ref _optimizationResult, value)) { OnPropertyChanged(nameof(HasOptimizationResult)); OnPropertyChanged(nameof(NewSizeText)); OnPropertyChanged(nameof(SpaceSavedText)); OnPropertyChanged(nameof(OptimizationStatus)); RaiseCommandStates(); } } }
    public bool HasOptimizationResult => OptimizationResult is not null;
    public string OriginalSizeText => Document is null ? "—" : FileSizeFormatter.Format(Document.FileSizeBytes);
    public string NewSizeText => OptimizationResult is null ? "—" : FileSizeFormatter.Format(OptimizationResult.OptimizedSizeBytes);
    public string SpaceSavedText
    {
        get
        {
            if (Document is null || OptimizationResult is null || Document.FileSizeBytes <= 0) return "—";
            var difference = Document.FileSizeBytes - OptimizationResult.OptimizedSizeBytes;
            var percentage = Math.Abs(difference) * 100d / Document.FileSizeBytes;
            return difference >= 0 ? $"{FileSizeFormatter.Format(difference)} ({percentage:0.#}%)" : $"Size increased by {FileSizeFormatter.Format(-difference)} ({percentage:0.#}%)";
        }
    }
    public bool ShowingPreview { get => _showingPreview; private set { if (!SetProperty(ref _showingPreview, value)) return; IsColorPickingActive = false; OnPropertyChanged(nameof(ImageSource)); OnPropertyChanged(nameof(CurrentPixelWidth)); OnPropertyChanged(nameof(CurrentPixelHeight)); OnPropertyChanged(nameof(DimensionsText)); OnPropertyChanged(nameof(PreviewButtonText)); NotifyImageGeometryChanged(); RaiseCommandStates(); } }
    public string PreviewButtonText => ShowingPreview ? "Show Original" : "Preview";

    public double ZoomPercentage { get => _zoomPercentage; private set { if (SetProperty(ref _zoomPercentage, Math.Clamp(value, MinZoom, MaxZoom))) { OnPropertyChanged(nameof(ZoomText)); NotifyImageGeometryChanged(); RaiseCommandStates(); } } }
    public void UpdateViewport(double width, double height, double dpiScaleX, double dpiScaleY) { if (width <= 0 || height <= 0) return; _viewportWidth = width; _viewportHeight = height; _dpiScaleX = Math.Max(dpiScaleX, .1); _dpiScaleY = Math.Max(dpiScaleY, .1); if (_fitMode && HasImage) CalculateFitZoom(); else NotifyImageGeometryChanged(); }
    public void RequestZoomIn() => ZoomIn(); public void RequestZoomOut() => ZoomOut();
    public void SelectImagePixel(int x, int y) { if (!IsColorPickingActive || Document is null || ShowingPreview) return; SelectedColor = _colorAnalysis.SamplePixel(Document, x, y); StatusMessage = $"Selected {SelectedColor.HexCode} at pixel {x}, {y}."; }

    private async Task OpenImageAsync() { var path = _fileDialog.ChooseImageToOpen(); if (!string.IsNullOrWhiteSpace(path)) await LoadImageAsync(path); }
    public async Task LoadImageAsync(string filePath)
    {
        _loadCancellation?.Cancel(); _loadCancellation?.Dispose(); _loadCancellation = new();
        var token = _loadCancellation.Token; ErrorMessage = null; IsBusy = true; StatusMessage = "Opening image…"; ImageDocument? loaded = null;
        try
        {
            loaded = await _imageLoader.LoadAsync(filePath, token); Document = loaded; SelectedColor = null; MainColors = [];
            IsColorPickingActive = false; OptimizationResult = null; ShowingPreview = false; ResetOptimizationFields();
            _fitMode = true; CalculateFitZoom(); StatusMessage = $"Opened {loaded.FileName}";
        }
        catch (OperationCanceledException) { StatusMessage = HasImage ? $"Showing {FileName}" : "Open a JPEG, PNG, or WebP image to begin."; }
        catch (ImageLoadException ex) { ErrorMessage = ex.Message; StatusMessage = "The image could not be opened."; }
        finally { IsBusy = false; }
        if (loaded is not null) _ = AnalyzeColorsAsync(loaded, token);
    }

    private async Task AnalyzeColorsAsync(ImageDocument document, CancellationToken token)
    {
        IsAnalyzingColors = true;
        try { var result = await _colorAnalysis.AnalyzeAsync(document, token); if (ReferenceEquals(Document, document)) MainColors = result.MainColors; }
        catch (OperationCanceledException) { }
        catch { if (ReferenceEquals(Document, document)) ErrorMessage = "CanvasLab could not find the main colors in this image."; }
        finally { if (ReferenceEquals(Document, document)) IsAnalyzingColors = false; }
    }

    private async Task OptimizeNowAsync()
    {
        _debounceCancellation?.Cancel();
        if (Document is null || !int.TryParse(TargetWidth, out var width) || !int.TryParse(TargetHeight, out var height)) return;
        _optimizationCancellation?.Cancel(); _optimizationCancellation?.Dispose(); _optimizationCancellation = new();
        IsOptimizing = true; ErrorMessage = null; StatusMessage = "Creating smaller image…";
        try
        {
            var request = new ImageOptimizationRequest(Document, width, height, ImageQuality, OutputFormat);
            OptimizationResult = await _optimization.OptimizeAsync(request, _optimizationCancellation.Token);
            StatusMessage = "Smaller image is ready to preview or save.";
        }
        catch (OperationCanceledException) { }
        catch (ImageOptimizationException ex) { ErrorMessage = ex.Message; StatusMessage = "The smaller image could not be created."; }
        finally { IsOptimizing = false; }
    }

    private async Task SaveImageAsync()
    {
        if (Document is null || OptimizationResult is null) return;
        var selection = _fileDialog.ChooseImageToSave(Document.FileName, OptimizationResult.OutputFormat);
        if (selection is null) return;
        if (string.Equals(Path.GetFullPath(selection.FilePath), Path.GetFullPath(Document.FilePath), StringComparison.OrdinalIgnoreCase))
        { ErrorMessage = "Choose a different file name. CanvasLab will not overwrite the original image."; return; }
        try { await _optimization.SaveAsync(OptimizationResult, selection.FilePath, selection.OverwriteApproved); StatusMessage = $"Saved {Path.GetFileName(selection.FilePath)}"; }
        catch (ImageOptimizationException ex) { ErrorMessage = ex.Message; StatusMessage = "The image could not be saved."; }
    }

    private void DimensionChanged(bool widthChanged)
    {
        if (_changingDimensions || Document is null) { RaiseCommandStates(); return; }
        if (KeepProportions && int.TryParse(widthChanged ? TargetWidth : TargetHeight, out var value) && value > 0)
        {
            _changingDimensions = true;
            if (widthChanged) { _targetHeight = Math.Max(1, (int)Math.Round(value * Document.PixelHeight / (double)Document.PixelWidth)).ToString(); OnPropertyChanged(nameof(TargetHeight)); }
            else { _targetWidth = Math.Max(1, (int)Math.Round(value * Document.PixelWidth / (double)Document.PixelHeight)).ToString(); OnPropertyChanged(nameof(TargetWidth)); }
            _changingDimensions = false;
        }
        InvalidateOptimization();
    }

    private void InvalidateOptimization()
    {
        _optimizationCancellation?.Cancel(); OptimizationResult = null; ShowingPreview = false; RaiseCommandStates();
        _debounceCancellation?.Cancel(); _debounceCancellation?.Dispose(); _debounceCancellation = new();
        var token = _debounceCancellation.Token;
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null)
            _ = Task.Run(async () => { try { await Task.Delay(450, token); if (!token.IsCancellationRequested) await dispatcher.InvokeAsync(OptimizeNowAsync); } catch (OperationCanceledException) { } });
    }

    private void TogglePreview() { if (!HasOptimizationResult) return; ShowingPreview = !ShowingPreview; CalculateFitZoom(); StatusMessage = ShowingPreview ? "Showing the saved result preview." : "Showing the original image."; }
    private void ResetOptimization() { ResetOptimizationFields(); OptimizationResult = null; ShowingPreview = false; StatusMessage = "Optimization settings reset."; }
    private void ResetOptimizationFields() { if (Document is null) return; _changingDimensions = true; _targetWidth = Document.PixelWidth.ToString(); _targetHeight = Document.PixelHeight.ToString(); _outputFormat = Document.SourceFormat; _imageQuality = 80; _keepProportions = true; _changingDimensions = false; OnPropertyChanged(nameof(TargetWidth)); OnPropertyChanged(nameof(TargetHeight)); OnPropertyChanged(nameof(OutputFormat)); OnPropertyChanged(nameof(ImageQuality)); OnPropertyChanged(nameof(KeepProportions)); OnPropertyChanged(nameof(IsQualityEnabled)); OnPropertyChanged(nameof(OriginalSizeText)); }
    private void ToggleColorPicking() { IsColorPickingActive = !IsColorPickingActive; StatusMessage = IsColorPickingActive ? "Click the image to pick a color." : $"Showing {FileName}"; }
    private void CopySelectedColor() { if (SelectedColor is not null) CopyColorCode(SelectedColor.HexCode); }
    private void CopyPaletteColor(RepresentativeColor color) => CopyColorCode(color.HexCode);
    private void CopyColorCode(string code) { try { _clipboard.SetText(code); StatusMessage = $"Copied {code}."; } catch { ErrorMessage = "CanvasLab could not copy the color. Try again."; } }
    private void FitToWindow() { _fitMode = true; CalculateFitZoom(); StatusMessage = "Image fitted to the window."; }
    private void ShowActualSize() { _fitMode = false; ZoomPercentage = 100; StatusMessage = "Showing image at 100%."; }
    private void ZoomIn() { if (!HasImage) return; _fitMode = false; ZoomPercentage = NextZoom(ZoomPercentage, true); }
    private void ZoomOut() { if (!HasImage) return; _fitMode = false; ZoomPercentage = NextZoom(ZoomPercentage, false); }
    private void CalculateFitZoom() { if (!HasImage || CurrentPixelWidth == 0 || CurrentPixelHeight == 0) return; var scale = Math.Min(Math.Max(1, _viewportWidth - 48) / (CurrentPixelWidth / _dpiScaleX), Math.Max(1, _viewportHeight - 48) / (CurrentPixelHeight / _dpiScaleY)); ZoomPercentage = Math.Clamp(Math.Min(1, scale) * 100, MinZoom, MaxZoom); }
    private static double NextZoom(double current, bool up) { double[] stops = [10,25,50,75,100,125,150,200,300,400,600,800]; return up ? stops.FirstOrDefault(x => x > current, MaxZoom) : stops.Reverse().FirstOrDefault(x => x < current, MinZoom); }
    private void NotifyDocumentAndGeometry() { OnPropertyChanged(nameof(HasImage)); OnPropertyChanged(nameof(ImageSource)); OnPropertyChanged(nameof(FileName)); OnPropertyChanged(nameof(FilePath)); OnPropertyChanged(nameof(DimensionsText)); OnPropertyChanged(nameof(FileSizeText)); OnPropertyChanged(nameof(SourceFormatText)); OnPropertyChanged(nameof(OriginalSizeText)); OnPropertyChanged(nameof(ZoomText)); NotifyImageGeometryChanged(); }
    private void NotifyImageGeometryChanged() { OnPropertyChanged(nameof(DisplayImageWidth)); OnPropertyChanged(nameof(DisplayImageHeight)); NotifyMarker(); }
    private void NotifyMarker() { OnPropertyChanged(nameof(HasSelectedColor)); OnPropertyChanged(nameof(MarkerLeft)); OnPropertyChanged(nameof(MarkerTop)); }
    private void RaiseCommandStates() { OpenImageCommand.RaiseCanExecuteChanged(); FitToWindowCommand.RaiseCanExecuteChanged(); ActualSizeCommand.RaiseCanExecuteChanged(); ZoomInCommand.RaiseCanExecuteChanged(); ZoomOutCommand.RaiseCanExecuteChanged(); PickColorsCommand.RaiseCanExecuteChanged(); MakeImageSmallerCommand.RaiseCanExecuteChanged(); PreviewCommand.RaiseCanExecuteChanged(); SaveImageCommand.RaiseCanExecuteChanged(); ResetOptimizationCommand.RaiseCanExecuteChanged(); }
}
