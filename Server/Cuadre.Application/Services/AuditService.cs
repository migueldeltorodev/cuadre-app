using Cuadre.Application.Common;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentValidation;

namespace Cuadre.Application.Services;

/// <summary>RF-DAT-08: writes the Configuración changes and reads the whole log, the newest first.</summary>
public interface IAuditService
{
    /// <summary>Nothing is written when nothing really changed.</summary>
    Task RecordAsync(ConfigSubject subject, Guid? subjectId, string subjectName, ChangeList changes,
        CancellationToken cancellationToken = default);

    Task<AuditEventsPage> GetEventsAsync(GetAuditEventsOptions options, CancellationToken cancellationToken = default);
}

public class AuditService : IAuditService
{
    /// <summary>"Since always": the log has no entries before the app existed.</summary>
    private static readonly DateOnly Beginning = new(2000, 1, 1);

    private readonly IAuditRepository _repository;
    private readonly IValidator<GetAuditEventsOptions> _optionsValidator;
    private readonly TimeProvider _clock;

    public AuditService(IAuditRepository repository, IValidator<GetAuditEventsOptions> optionsValidator,
        TimeProvider clock)
    {
        _repository = repository;
        _optionsValidator = optionsValidator;
        _clock = clock;
    }

    public async Task RecordAsync(ConfigSubject subject, Guid? subjectId, string subjectName, ChangeList changes,
        CancellationToken cancellationToken = default)
    {
        if (changes.IsEmpty)
        {
            return;
        }

        await _repository.RecordAsync(new ConfigChange(Guid.NewGuid(),_clock.UtcNow(), subject, subjectId,
            subjectName, changes.Items), cancellationToken);
    }

    public async Task<AuditEventsPage> GetEventsAsync(GetAuditEventsOptions options,
        CancellationToken cancellationToken = default)
    {
        await _optionsValidator.ValidateOrThrowAsync(options, cancellationToken);

        // The shop's calendar days, turned into instants: [from 00:00, the day after "to" 00:00).
        var zone = _clock.LocalTimeZone;
        var from = options.From ?? Beginning;
        var to = options.To ?? _clock.Today();
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), zone);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);

        var kinds = options.Kind is { } kind
            ? new HashSet<AuditEventKind> { kind }
            : Enum.GetValues<AuditEventKind>().ToHashSet();

        var all = (await _repository.GetEventsAsync(fromUtc, toUtc, kinds, cancellationToken))
            .OrderByDescending(entry => entry.At)
            .ThenBy(entry => entry.Title, StringComparer.Ordinal)
            .ToList();

        var page = all.Skip((options.Page - 1) * options.PageSize).Take(options.PageSize).ToList();
        return new AuditEventsPage(page, all.Count);
    }
}
