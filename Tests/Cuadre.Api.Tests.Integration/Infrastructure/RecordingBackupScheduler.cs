using System.Collections.Concurrent;
using Cuadre.Application.Models;
using Cuadre.Application.Services;

namespace Cuadre.Api.Tests.Integration.Infrastructure;

/// <summary>
/// Stands in for the background queue: tests see WHAT was asked (a closing asks for a backup) without a worker
/// copying the database behind their back while the next test wipes it.
/// </summary>
public sealed class RecordingBackupScheduler : IBackupScheduler
{
    private readonly ConcurrentQueue<BackupReason> _requests = new();

    public IReadOnlyList<BackupReason> Requests => _requests.ToList();

    public void Request(BackupReason reason) => _requests.Enqueue(reason);

    public void Clear() => _requests.Clear();
}
