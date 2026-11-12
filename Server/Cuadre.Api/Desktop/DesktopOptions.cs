namespace Cuadre.Api.Desktop;

public sealed class DesktopOptions
{
    public const string SectionName = "Desktop";

    /// <summary>Open the app window (Edge in --app mode) when the server starts. Off in Development/Testing.</summary>
    public bool LaunchWindow { get; init; }

    /// <summary>Optional explicit path to msedge.exe. Empty = look in the standard install folders.</summary>
    public string? EdgePath { get; init; }

    public int WindowWidth { get; init; } = 1440;
    public int WindowHeight { get; init; } = 900;
}
