using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

/// <summary>Employees (who worked) and the pay schemes of the post. Few rows in a lifetime: no paging.</summary>
public interface IStaffRepository
{
    Task<bool> CreateEmployeeAsync(Employee employee, CancellationToken cancellationToken = default);
    Task<Employee?> GetEmployeeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Employee?> GetEmployeeByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Active first, then by name.</summary>
    Task<IEnumerable<Employee>> GetEmployeesAsync(CancellationToken cancellationToken = default);

    Task<bool> UpdateEmployeeAsync(Employee employee, CancellationToken cancellationToken = default);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<StaffPayScheme>> GetPaySchemesAsync(CancellationToken cancellationToken = default);

    /// <summary>The scheme in force on that date: the latest one starting on or before it.</summary>
    Task<StaffPayScheme?> GetPaySchemeForDateAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>Inserts it, or replaces the amounts of the scheme with the same start date.</summary>
    Task SavePaySchemeAsync(StaffPayScheme scheme, bool replaceExisting, CancellationToken cancellationToken = default);
}

public class StaffRepository : IStaffRepository
{
    private const string EmployeeColumns = "id, name, is_active, created_at, updated_at";

    private const string SchemeColumns =
        "id, effective_from, daily_fixed_cents, percent_of_sales_bps, notes, created_at, updated_at";

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public StaffRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<bool> CreateEmployeeAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO employees (id, name, is_active, created_at, updated_at)
            VALUES (@Id, @Name, @IsActive, @CreatedAt, @UpdatedAt)
            """, employee, cancellationToken: cancellationToken));
        return result > 0;
    }

    public async Task<Employee?> GetEmployeeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Employee>(new CommandDefinition(
            $"SELECT {EmployeeColumns} FROM employees WHERE id = @id", new { id },
            cancellationToken: cancellationToken));
    }

    public async Task<Employee?> GetEmployeeByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Employee>(new CommandDefinition(
            $"SELECT {EmployeeColumns} FROM employees WHERE name = @name", new { name },
            cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<Employee>> GetEmployeesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<Employee>(new CommandDefinition(
            $"SELECT {EmployeeColumns} FROM employees ORDER BY is_active DESC, name",
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateEmployeeAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE employees SET name = @Name, is_active = @IsActive, updated_at = @UpdatedAt WHERE id = @Id
            """, new { employee.Id, employee.Name, employee.IsActive, employee.UpdatedAt },
            cancellationToken: cancellationToken));
        return result > 0;
    }

    public async Task<IReadOnlyList<StaffPayScheme>> GetPaySchemesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var schemes = await connection.QueryAsync<StaffPayScheme>(new CommandDefinition(
            $"SELECT {SchemeColumns} FROM staff_pay_schemes ORDER BY effective_from DESC",
            cancellationToken: cancellationToken));
        return schemes.ToList();
    }

    public async Task<StaffPayScheme?> GetPaySchemeForDateAsync(DateOnly date,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<StaffPayScheme>(new CommandDefinition($"""
            SELECT {SchemeColumns} FROM staff_pay_schemes
            WHERE effective_from <= @date
            ORDER BY effective_from DESC
            LIMIT 1
            """, new { date }, cancellationToken: cancellationToken));
    }

    public async Task SavePaySchemeAsync(StaffPayScheme scheme, bool replaceExisting,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var sql = replaceExisting
            ? """
              UPDATE staff_pay_schemes SET
                  daily_fixed_cents = @DailyFixedCents,
                  percent_of_sales_bps = @PercentOfSalesBps,
                  notes = @Notes,
                  updated_at = @UpdatedAt
              WHERE effective_from = @EffectiveFrom
              """
            : """
              INSERT INTO staff_pay_schemes (id, effective_from, daily_fixed_cents, percent_of_sales_bps, notes,
                                             created_at, updated_at)
              VALUES (@Id, @EffectiveFrom, @DailyFixedCents, @PercentOfSalesBps, @Notes, @CreatedAt, @UpdatedAt)
              """;
        await connection.ExecuteAsync(new CommandDefinition(sql, scheme, cancellationToken: cancellationToken));
    }
}
