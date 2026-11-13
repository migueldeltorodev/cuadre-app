using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

public interface IEnvelopeOperationService
{
    /// <summary>Opening, transfer, withdrawal or tax payment: validates, resolves the lines, confirms, warns.</summary>
    Task<Result<EnvelopeOperationResult>> CreateAsync(EnvelopeOperation operation,
        CancellationToken cancellationToken = default);

    Task<Result<EnvelopeOperation>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<EnvelopeOperationSummary>> GetAllAsync(GetAllEnvelopeOperationsOptions options,
        CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(GetAllEnvelopeOperationsOptions options, CancellationToken cancellationToken = default);

    /// <summary>Always allowed: writes the reverse of every line, dated today.</summary>
    Task<Result<EnvelopeOperation>> VoidAsync(VoidEnvelopeOperation request,
        CancellationToken cancellationToken = default);
}

public class EnvelopeOperationService : IEnvelopeOperationService
{
    private readonly IEnvelopeOperationRepository _repository;
    private readonly IEnvelopeRepository _envelopeRepository;
    private readonly ICashBoxRepository _cashBoxRepository;
    private readonly IValidator<EnvelopeOperation> _validator;
    private readonly IValidator<VoidEnvelopeOperation> _voidValidator;
    private readonly IValidator<GetAllEnvelopeOperationsOptions> _optionsValidator;
    private readonly IClosedDayGuard _closedDayGuard;
    private readonly TimeProvider _clock;

    public EnvelopeOperationService(IEnvelopeOperationRepository repository, IEnvelopeRepository envelopeRepository,
        ICashBoxRepository cashBoxRepository, IValidator<EnvelopeOperation> validator,
        IValidator<VoidEnvelopeOperation> voidValidator, IValidator<GetAllEnvelopeOperationsOptions> optionsValidator,
        IClosedDayGuard closedDayGuard, TimeProvider clock)
    {
        _repository = repository;
        _envelopeRepository = envelopeRepository;
        _cashBoxRepository = cashBoxRepository;
        _validator = validator;
        _voidValidator = voidValidator;
        _optionsValidator = optionsValidator;
        _closedDayGuard = closedDayGuard;
        _clock = clock;
    }

    public async Task<Result<EnvelopeOperationResult>> CreateAsync(EnvelopeOperation operation,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateOrThrowAsync(operation, cancellationToken);

        if (operation.Date > _clock.Today())
        {
            return Result.Fail(new BusinessRuleError("La fecha no puede ser futura."));
        }

        var open = await _closedDayGuard.EnsureOpenAsync(operation.Date, cancellationToken);
        if (open.IsFailed)
        {
            return open;
        }

        if (operation.Kind == EnvelopeOperationKind.TaxPayment)
        {
            operation.EnvelopeId = (await _envelopeRepository.GetByRoleAsync(EnvelopeRole.Tax, cancellationToken)).Id;
        }

        var envelopes = (await _envelopeRepository.GetAllAsync(cancellationToken)).ToDictionary(e => e.Id);
        var boxes = (await _cashBoxRepository.GetAllAsync(cancellationToken)).ToDictionary(b => b.Id);

        var linesCheck = operation.Kind == EnvelopeOperationKind.Opening
            ? await ResolveOpeningLinesAsync(operation, envelopes, boxes, cancellationToken)
            : ResolveSingleBoxLines(operation, envelopes, boxes);
        if (linesCheck.IsFailed)
        {
            return linesCheck;
        }

        // Before saving: what each touched (envelope, box) holds now, to warn about the ones left negative.
        var before = await _envelopeRepository.GetBalancesAsync(cancellationToken: cancellationToken);

        var now = _clock.UtcNow();
        operation.Status = EnvelopeOperationStatus.Confirmed;
        operation.CreatedAt = now;
        operation.UpdatedAt = now;
        operation.Number = await _repository.GetNextNumberAsync(cancellationToken);

        var movements = operation.Lines
            .Where(line => line.AmountCents != 0)
            .Select(line => NewMovement(operation, line, line.AmountCents, operation.Date, Kind(operation.Kind),
                Description(operation, envelopes), now))
            .ToList();

        await _repository.CreateAsync(operation, movements, cancellationToken);

        var saved = await _repository.GetByIdAsync(operation.Id, cancellationToken)
                    ?? throw new InvalidOperationException("The envelope operation was not found right after saving it.");

        var warnings = saved.Lines
            .Where(line => line.AmountCents < 0)
            .Select(line => new NegativeEnvelopeWarning(line.EnvelopeId, line.EnvelopeName, line.CashBoxId,
                line.CashBoxName, line.Currency,
                Balance(before, line.EnvelopeId, line.CashBoxId) + line.AmountCents))
            .Where(warning => warning.BalanceCents < 0)
            .ToList();

        return Result.Ok(new EnvelopeOperationResult(saved, warnings));
    }

    public async Task<Result<EnvelopeOperation>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var operation = await _repository.GetByIdAsync(id, cancellationToken);
        return operation is null
            ? Result.Fail(NotFoundError.For<EnvelopeOperation>(id))
            : Result.Ok(operation);
    }

    public async Task<IEnumerable<EnvelopeOperationSummary>> GetAllAsync(GetAllEnvelopeOperationsOptions options,
        CancellationToken cancellationToken = default)
    {
        await _optionsValidator.ValidateOrThrowAsync(options, cancellationToken);
        return await _repository.GetAllAsync(options, cancellationToken);
    }

    public Task<int> GetCountAsync(GetAllEnvelopeOperationsOptions options,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetCountAsync(options, cancellationToken);
    }

    public async Task<Result<EnvelopeOperation>> VoidAsync(VoidEnvelopeOperation request,
        CancellationToken cancellationToken = default)
    {
        var operation = await _repository.GetByIdAsync(request.OperationId, cancellationToken);
        if (operation is null)
        {
            return Result.Fail(NotFoundError.For<EnvelopeOperation>(request.OperationId));
        }

        await _voidValidator.ValidateOrThrowAsync(request, cancellationToken);

        if (operation.Status == EnvelopeOperationStatus.Voided)
        {
            return Result.Fail(new BusinessRuleError($"La operación #{operation.Number} ya está anulada."));
        }

        // The reversal is dated today: today must still be open.
        var open = await _closedDayGuard.EnsureOpenAsync(_clock.Today(), cancellationToken);
        if (open.IsFailed)
        {
            return open;
        }

        var now = _clock.UtcNow();
        var today = _clock.Today();
        operation.Status = EnvelopeOperationStatus.Voided;
        operation.VoidReason = request.Reason.Trim();
        operation.VoidedAt = now;
        operation.UpdatedAt = now;

        var reversals = operation.Lines
            .Where(line => line.AmountCents != 0)
            .Select(line => NewMovement(operation, line, -line.AmountCents, today,
                CashMovementKind.EnvelopeOperationVoid, $"Anulación de la operación #{operation.Number}", now))
            .ToList();

        await _repository.VoidAsync(operation, reversals, cancellationToken);
        return Result.Ok(await _repository.GetByIdAsync(operation.Id, cancellationToken) ?? operation);
    }

    /// <summary>
    /// Opening (RF-PF-06 start): each target says what is physically in an envelope of a box on that date. The line
    /// moves the difference with what the ledger already says (purchases recorded before, an earlier opening...),
    /// so the balance ends exactly at the target. 0 = it already matched (kept, without a movement).
    /// </summary>
    private async Task<Result> ResolveOpeningLinesAsync(EnvelopeOperation operation,
        IReadOnlyDictionary<Guid, Envelope> envelopes, IReadOnlyDictionary<Guid, CashBox> boxes,
        CancellationToken cancellationToken)
    {
        var balances = await _envelopeRepository.GetBalancesAsync(operation.Date, cancellationToken: cancellationToken);

        var lineNo = 1;
        foreach (var target in operation.Targets)
        {
            if (!boxes.TryGetValue(target.CashBoxId, out var box) || !box.IsActive)
            {
                return Result.Fail(new BusinessRuleError("Una de las cajas no existe o está desactivada."));
            }

            if (!envelopes.TryGetValue(target.EnvelopeId, out var envelope) || !envelope.IsActive)
            {
                return Result.Fail(new BusinessRuleError("Uno de los sobres no existe o está desactivado."));
            }

            operation.Lines.Add(new EnvelopeOperationLine
            {
                Id = Guid.NewGuid(),
                OperationId = operation.Id,
                LineNo = lineNo++,
                CashBoxId = box.Id,
                EnvelopeId = envelope.Id,
                AmountCents = target.TargetCents - Balance(balances, envelope.Id, box.Id),
                TargetCents = target.TargetCents
            });
        }

        return operation.Lines.All(line => line.AmountCents == 0)
            ? Result.Fail(new BusinessRuleError("Los saldos escritos ya son los que tiene la app: no hay nada que cambiar."))
            : Result.Ok();
    }

    /// <summary>Transfer: out of one envelope, into the other. Withdrawal and tax payment: out of one envelope.</summary>
    private static Result ResolveSingleBoxLines(EnvelopeOperation operation,
        IReadOnlyDictionary<Guid, Envelope> envelopes, IReadOnlyDictionary<Guid, CashBox> boxes)
    {
        if (!boxes.TryGetValue(operation.CashBoxId!.Value, out var box) || !box.IsActive)
        {
            return Result.Fail(new BusinessRuleError("La caja elegida no existe o está desactivada."));
        }

        if (!envelopes.TryGetValue(operation.EnvelopeId!.Value, out var from) || !from.IsActive)
        {
            return Result.Fail(new BusinessRuleError("El sobre elegido no existe o está desactivado."));
        }

        var amount = operation.AmountCents!.Value;
        operation.Lines.Add(NewLine(operation, 1, box.Id, from.Id, -amount));

        if (operation.ToEnvelopeId is { } toId)
        {
            if (!envelopes.TryGetValue(toId, out var to) || !to.IsActive)
            {
                return Result.Fail(new BusinessRuleError("El sobre de destino no existe o está desactivado."));
            }

            operation.Lines.Add(NewLine(operation, 2, box.Id, to.Id, amount));
        }

        return Result.Ok();
    }

    private static EnvelopeOperationLine NewLine(EnvelopeOperation operation, int lineNo, Guid cashBoxId,
        Guid envelopeId, long amountCents) => new()
    {
        Id = Guid.NewGuid(),
        OperationId = operation.Id,
        LineNo = lineNo,
        CashBoxId = cashBoxId,
        EnvelopeId = envelopeId,
        AmountCents = amountCents
    };

    private static long Balance(IEnumerable<EnvelopeBoxBalance> balances, Guid envelopeId, Guid cashBoxId) =>
        balances.FirstOrDefault(b => b.EnvelopeId == envelopeId && b.CashBoxId == cashBoxId)?.AmountCents ?? 0;

    private static CashMovementKind Kind(EnvelopeOperationKind kind) => kind switch
    {
        EnvelopeOperationKind.Opening => CashMovementKind.Opening,
        EnvelopeOperationKind.Transfer => CashMovementKind.EnvelopeTransfer,
        EnvelopeOperationKind.Withdrawal => CashMovementKind.Withdrawal,
        EnvelopeOperationKind.TaxPayment => CashMovementKind.TaxPayment,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static string Description(EnvelopeOperation operation, IReadOnlyDictionary<Guid, Envelope> envelopes)
    {
        string Name(Guid? id) => id is { } value && envelopes.TryGetValue(value, out var e) ? e.Name : "";

        var text = operation.Kind switch
        {
            EnvelopeOperationKind.Opening => $"Saldo inicial #{operation.Number}",
            EnvelopeOperationKind.Transfer =>
                $"Movimiento #{operation.Number}: {Name(operation.EnvelopeId)} → {Name(operation.ToEnvelopeId)}",
            EnvelopeOperationKind.Withdrawal => $"Retiro #{operation.Number}",
            EnvelopeOperationKind.TaxPayment => $"Impuestos #{operation.Number}: {operation.Concept?.Trim()}",
            _ => $"Operación #{operation.Number}"
        };

        return operation.Notes is { Length: > 0 } notes && operation.Kind == EnvelopeOperationKind.Withdrawal
            ? $"{text}: {notes.Trim()}"
            : text;
    }

    private static CashMovement NewMovement(EnvelopeOperation operation, EnvelopeOperationLine line, long amountCents,
        DateOnly date, CashMovementKind kind, string description, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        CashBoxId = line.CashBoxId,
        EnvelopeId = line.EnvelopeId,
        Date = date,
        Kind = kind,
        AmountCents = amountCents,
        Description = description,
        EnvelopeOperationId = operation.Id,
        CreatedAt = now
    };
}
