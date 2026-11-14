using Cuadre.Application.Models;
using Cuadre.Application.Services;
using Microsoft.Extensions.Options;

namespace Cuadre.Api.Desktop;

public sealed class BackupOptions
{
    public const string SectionName = "Backup";

    /// <summary>RF-DAT-01: back up when the app closes. Off in the tests (they would leave a copy per run).</summary>
    public bool OnExit { get; init; } = true;
}

/// <summary>
/// Makes the automatic backups one after another: the ones the closing asks for while the app runs, and one last
/// copy when the window is closed (RF-DAT-01). A backup in progress is never cut: stopping first lets the queue
/// drain, then makes the exit copy, within the host's shutdown timeout.
/// </summary>
public sealed class BackupWorker : BackgroundService
{
    private readonly BackupQueue _queue;
    private readonly IBackupService _backups;
    private readonly BackupOptions _options;
    private readonly ILogger<BackupWorker> _logger;

    public BackupWorker(BackupQueue queue, IBackupService backups, IOptions<BackupOptions> options,
        ILogger<BackupWorker> logger)
    {
        _queue = queue;
        _backups = backups;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Not stoppingToken: the loop ends when the queue is completed (StopAsync), after the last request.
        await foreach (var reason in _queue.Reader.ReadAllAsync(CancellationToken.None))
        {
            await BackUpAsync(reason);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Complete();
        if (ExecuteTask is { } running)
        {
            await running.WaitAsync(cancellationToken);
        }

        if (_options.OnExit)
        {
            await BackUpAsync(BackupReason.AppExit);
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task BackUpAsync(BackupReason reason)
    {
        try
        {
            var result = await _backups.CreateAsync(reason);
            if (result.IsFailed)
            {
                _logger.LogError("Backup ({Reason}) failed: {Message}", reason, result.Errors[0].Message);
                return;
            }

            _logger.LogInformation("Backup ({Reason}) written: {File}; pendrive: {External}",
                reason, result.Value.Backup.FileName, result.Value.External);
        }
        catch (Exception ex)
        {
            // The worker must survive anything: the next closing will try again.
            _logger.LogError(ex, "Backup ({Reason}) crashed", reason);
        }
    }
}
