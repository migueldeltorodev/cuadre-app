using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

/// <summary>Configuración › Personal: who works the counter and how the post is paid (RF-PER-01/02).</summary>
public interface IStaffService
{
    Task<Result<Employee>> CreateEmployeeAsync(Employee employee, CancellationToken cancellationToken = default);
    Task<IEnumerable<Employee>> GetEmployeesAsync(CancellationToken cancellationToken = default);
    Task<Result<Employee>> UpdateEmployeeAsync(EmployeeChanges changes, CancellationToken cancellationToken = default);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<StaffPayScheme>> GetPaySchemesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// A new scheme from a date on. Same start as the latest one replaces it; an earlier date, or a date already
    /// closed, is refused: what was paid on a closed day never changes (RF-PER-02).
    /// </summary>
    Task<Result<StaffPayScheme>> SavePaySchemeAsync(StaffPayScheme scheme,
        CancellationToken cancellationToken = default);
}

public class StaffService : IStaffService
{
    private readonly IStaffRepository _repository;
    private readonly IClosedDayGuard _closedDayGuard;
    private readonly IValidator<Employee> _employeeValidator;
    private readonly IValidator<StaffPayScheme> _schemeValidator;
    private readonly TimeProvider _clock;

    public StaffService(IStaffRepository repository, IClosedDayGuard closedDayGuard,
        IValidator<Employee> employeeValidator, IValidator<StaffPayScheme> schemeValidator, TimeProvider clock)
    {
        _repository = repository;
        _closedDayGuard = closedDayGuard;
        _employeeValidator = employeeValidator;
        _schemeValidator = schemeValidator;
        _clock = clock;
    }

    public async Task<Result<Employee>> CreateEmployeeAsync(Employee employee,
        CancellationToken cancellationToken = default)
    {
        await _employeeValidator.ValidateOrThrowAsync(employee, cancellationToken);

        if (await _repository.GetEmployeeByNameAsync(employee.Name, cancellationToken) is not null)
        {
            return Result.Fail(new ConflictError($"Ya hay alguien llamado '{employee.Name}'."));
        }

        var now = _clock.UtcNow();
        employee.CreatedAt = now;
        employee.UpdatedAt = now;
        await _repository.CreateEmployeeAsync(employee, cancellationToken);
        return Result.Ok(employee);
    }

    public Task<IEnumerable<Employee>> GetEmployeesAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetEmployeesAsync(cancellationToken);
    }

    public async Task<Result<Employee>> UpdateEmployeeAsync(EmployeeChanges changes,
        CancellationToken cancellationToken = default)
    {
        var employee = await _repository.GetEmployeeByIdAsync(changes.Id, cancellationToken);
        if (employee is null)
        {
            return Result.Fail(NotFoundError.For<Employee>(changes.Id));
        }

        employee.Name = changes.Name;
        employee.IsActive = changes.IsActive;
        await _employeeValidator.ValidateOrThrowAsync(employee, cancellationToken);

        var sameName = await _repository.GetEmployeeByNameAsync(employee.Name, cancellationToken);
        if (sameName is not null && sameName.Id != employee.Id)
        {
            return Result.Fail(new ConflictError($"Ya hay alguien llamado '{employee.Name}'."));
        }

        employee.UpdatedAt = _clock.UtcNow();
        await _repository.UpdateEmployeeAsync(employee, cancellationToken);
        return Result.Ok(employee);
    }

    public Task<IReadOnlyList<StaffPayScheme>> GetPaySchemesAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetPaySchemesAsync(cancellationToken);
    }

    public async Task<Result<StaffPayScheme>> SavePaySchemeAsync(StaffPayScheme scheme,
        CancellationToken cancellationToken = default)
    {
        await _schemeValidator.ValidateOrThrowAsync(scheme, cancellationToken);

        var open = await _closedDayGuard.EnsureOpenAsync(scheme.EffectiveFrom, cancellationToken);
        if (open.IsFailed)
        {
            return open;
        }

        var latest = (await _repository.GetPaySchemesAsync(cancellationToken)).FirstOrDefault();
        if (latest is not null && scheme.EffectiveFrom < latest.EffectiveFrom)
        {
            return Result.Fail(new BusinessRuleError(
                $"Ya hay un esquema que empieza el {ClosedDayGuard.Format(latest.EffectiveFrom)}: " +
                "el nuevo tiene que empezar ese día o después."));
        }

        var replaces = latest is not null && scheme.EffectiveFrom == latest.EffectiveFrom;
        var now = _clock.UtcNow();
        scheme.CreatedAt = replaces ? latest!.CreatedAt : now;
        scheme.UpdatedAt = now;
        await _repository.SavePaySchemeAsync(scheme, replaces, cancellationToken);

        return Result.Ok(await _repository.GetPaySchemeForDateAsync(scheme.EffectiveFrom, cancellationToken)
                         ?? scheme);
    }
}
