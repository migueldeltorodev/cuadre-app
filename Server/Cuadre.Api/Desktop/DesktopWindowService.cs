using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;

namespace Cuadre.Api.Desktop;

/// <summary>
/// Beta desktop shell: once the server is listening, opens Microsoft Edge in "app" mode
/// (a window without browser UI) pointing at the local API, and shuts the server down when
/// that window is closed. Replaced later by a WebView2 host without touching API or frontend.
/// </summary>
public sealed class DesktopWindowService : BackgroundService
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IServer _server;
    private readonly DesktopOptions _options;
    private readonly AppPaths _paths;
    private readonly ILogger<DesktopWindowService> _logger;

    public DesktopWindowService(
        IHostApplicationLifetime lifetime,
        IServer server,
        IOptions<DesktopOptions> options,
        AppPaths paths,
        ILogger<DesktopWindowService> logger)
    {
        _lifetime = lifetime;
        _server = server;
        _options = options.Value;
        _paths = paths;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.LaunchWindow)
        {
            return;
        }

        if (!await WaitForApplicationStartedAsync(stoppingToken))
        {
            return;
        }

        var url = _server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
        if (url is null)
        {
            _logger.LogError("The server has no listening address; the window cannot be opened");
            return;
        }

        var edge = FindEdge();
        if (edge is null)
        {
            // Fallback: default browser. The server keeps running until the process is closed.
            _logger.LogWarning("Microsoft Edge not found; opening {Url} in the default browser", url);
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return;
        }

        // A dedicated profile makes this process the real browser process for the window,
        // so waiting for it to exit means "the user closed Cuadre".
        Directory.CreateDirectory(_paths.EdgeProfile);
        var startInfo = new ProcessStartInfo(edge)
        {
            ArgumentList =
            {
                $"--app={url}",
                $"--user-data-dir={_paths.EdgeProfile}",
                $"--window-size={_options.WindowWidth},{_options.WindowHeight}",
                "--no-first-run",
                "--no-default-browser-check"
            }
        };

        _logger.LogInformation("Opening Cuadre window at {Url}", url);
        using var process = Process.Start(startInfo);
        if (process is null)
        {
            _logger.LogError("Microsoft Edge could not be started");
            return;
        }

        try
        {
            await process.WaitForExitAsync(stoppingToken);
            _logger.LogInformation("Cuadre window closed; stopping the server");
            _lifetime.StopApplication();
        }
        catch (OperationCanceledException)
        {
            // The server is stopping on its own (e.g. Ctrl+C); nothing else to do.
        }
    }

    private async Task<bool> WaitForApplicationStartedAsync(CancellationToken stoppingToken)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var onStarted = _lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        await using var onStopping = stoppingToken.Register(() => started.TrySetCanceled(stoppingToken));

        try
        {
            await started.Task;
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private string? FindEdge()
    {
        if (!string.IsNullOrWhiteSpace(_options.EdgePath) && File.Exists(_options.EdgePath))
        {
            return _options.EdgePath;
        }

        string[] roots =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        ];

        return roots
            .Where(root => !string.IsNullOrEmpty(root))
            .Select(root => Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe"))
            .FirstOrDefault(File.Exists);
    }
}
