using System.Reflection;
using System.Windows;
using CanvasLab.Utilities;

namespace CanvasLab.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        VersionText = ReadApplicationVersion();
        DataContext = this;
    }

    public string VersionText { get; }

    private static string ReadApplicationVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AboutDialog).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var actualVersion = informational?.Split('+', 2)[0] ?? assembly.GetName().Version?.ToString() ?? "unknown";
        return $"Version {actualVersion}";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void GitHubLink_Click(object sender, RoutedEventArgs e)
    {
        try { ExternalLinks.OpenGitHubProject(); }
        catch
        {
            MessageBox.Show(this, "CanvasLab could not open the web browser.", "CanvasLab", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
