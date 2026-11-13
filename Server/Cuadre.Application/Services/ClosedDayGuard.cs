using System.Globalization;
using Cuadre.Application.Errors;
using Cuadre.Application.Repositories;
using FluentResults;

namespace Cuadre.Application.Services;

/// <summary>
/// RF-CIE-10: a closed day is locked. Nothing may be dated on or before the latest closed day: purchases, stock
/// operations, envelope operations, voids (their reversals are dated today), new % or pay schemes. Otherwise a
/// closing already made would stop matching its ledger (its counter stock, its split). D-26.
/// To change a closed day the user reopens it (only the latest one can be reopened).
/// </summary>
public interface IClosedDayGuard
{
    Task<Result> EnsureOpenAsync(DateOnly date, CancellationToken cancellationToken = default);
}

public class ClosedDayGuard : IClosedDayGuard
{
    private readonly IDailyClosingRepository _repository;

    public ClosedDayGuard(IDailyClosingRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> EnsureOpenAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var lastClosed = await _repository.GetLastClosedDateAsync(cancellationToken);
        return lastClosed is { } closed && date <= closed
            ? Result.Fail(new BusinessRuleError(
                $"El {Format(closed)} ya está cerrado: no se puede registrar nada con fecha hasta ese día. " +
                "Si hay que corregir algo, reabrí ese cierre."))
            : Result.Ok();
    }

    internal static string Format(DateOnly date) => date.ToString("d/M/yyyy", CultureInfo.InvariantCulture);
}
