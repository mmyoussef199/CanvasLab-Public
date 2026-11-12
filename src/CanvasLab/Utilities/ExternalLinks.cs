using System.Diagnostics;

namespace CanvasLab.Utilities;

internal static class ExternalLinks
{
    public const string GitHubProjectUrl = "https://github.com/mmyoussef199/CanvasLab-Public";

    public static void OpenGitHubProject() => Process.Start(new ProcessStartInfo
    {
        FileName = GitHubProjectUrl,
        UseShellExecute = true
    });
}
