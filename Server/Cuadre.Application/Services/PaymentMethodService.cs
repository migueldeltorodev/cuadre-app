using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

public interface IPaymentMethodService
{
    Task<Result<PaymentMethod>> CreateAsync(PaymentMethod paymentMethod, CancellationToken cancellationToken = default);
    Task<Result<PaymentMethod>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<PaymentMethod>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<PaymentMethod>> UpdateAsync(PaymentMethod paymentMethod, CancellationToken cancellationToken = default);
}

public class PaymentMethodService : IPaymentMethodService
{
    private readonly IPaymentMethodRepository _repository;
    private readonly ICashBoxRepository _cashBoxRepository;
    private readonly IValidator<PaymentMethod> _validator;
    private readonly TimeProvider _clock;

    public PaymentMethodService(IPaymentMethodRepository repository, ICashBoxRepository cashBoxRepository,
        IValidator<PaymentMethod> validator, TimeProvider clock)
    {
        _repository = repository;
        _cashBoxRepository = cashBoxRepository;
        _validator = validator;
        _clock = clock;
    }

    public async Task<Result<PaymentMethod>> CreateAsync(PaymentMethod paymentMethod,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateOrThrowAsync(paymentMethod, cancellationToken);

        var ruleCheck = await CheckDataRulesAsync(paymentMethod, cancellationToken);
        if (ruleCheck.IsFailed)
        {
            return ruleCheck;
        }

        var now = _clock.UtcNow();
        paymentMethod.CreatedAt = now;
        paymentMethod.UpdatedAt = now;

        await _repository.CreateAsync(paymentMethod, cancellationToken);
        return Result.Ok(paymentMethod);
    }

    public async Task<Result<PaymentMethod>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var paymentMethod = await _repository.GetByIdAsync(id, cancellationToken);
        return paymentMethod is null
            ? Result.Fail(NotFoundError.For<PaymentMethod>(id))
            : Result.Ok(paymentMethod);
    }

    public Task<IEnumerable<PaymentMethod>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public async Task<Result<PaymentMethod>> UpdateAsync(PaymentMethod paymentMethod,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(paymentMethod.Id, cancellationToken);
        if (existing is null)
        {
            return Result.Fail(NotFoundError.For<PaymentMethod>(paymentMethod.Id));
        }

        await _validator.ValidateOrThrowAsync(paymentMethod, cancellationToken);

        var ruleCheck = await CheckDataRulesAsync(paymentMethod, cancellationToken);
        if (ruleCheck.IsFailed)
        {
            return ruleCheck;
        }

        paymentMethod.CreatedAt = existing.CreatedAt;
        paymentMethod.UpdatedAt = _clock.UtcNow();
        await _repository.UpdateAsync(paymentMethod, cancellationToken);
        return Result.Ok(paymentMethod);
    }

    /// <summary>Unique name (409); the target cash box must exist (422) and be active if the method is (422).</summary>
    private async Task<Result<PaymentMethod>> CheckDataRulesAsync(PaymentMethod paymentMethod,
        CancellationToken cancellationToken)
    {
        var sameName = await _repository.GetByNameAsync(paymentMethod.Name, cancellationToken);
        if (sameName is not null && sameName.Id != paymentMethod.Id)
        {
            return Result.Fail(new ConflictError($"Ya existe una forma de cobro llamada '{paymentMethod.Name}'."));
        }

        // The box comes from the BODY, not the URL: a missing one is an invalid request (422), not a 404.
        var cashBox = await _cashBoxRepository.GetByIdAsync(paymentMethod.CashBoxId, cancellationToken);
        if (cashBox is null)
        {
            return Result.Fail(new BusinessRuleError("La caja elegida no existe."));
        }

        if (paymentMethod.IsActive && !cashBox.IsActive)
        {
            return Result.Fail(new BusinessRuleError(
                $"La caja '{cashBox.Name}' está desactivada: una forma de cobro activa no puede entrar a ella."));
        }

        return Result.Ok(paymentMethod);
    }
}
