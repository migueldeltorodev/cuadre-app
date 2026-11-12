using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

public interface ICashBoxRepository
{
    Task<bool> CreateAsync(CashBox cashBox, CancellationToken cancellationToken = default);
    Task<CashBox?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CashBox?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IEnumerable<CashBox>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(CashBox cashBox, CancellationToken cancellationToken = default);

    /// <summary>True if an ACTIVE payment method still sends money to this box.</summary>
    Task<bool> HasActivePaymentMethodsAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// No delete on purpose: cash boxes will hold movements, so they are deactivated instead.
/// Enums are bound as text explicitly: Dapper sends them as integers by default.
/// </summary>
public class CashBoxRepository : ICashBoxRepository
{
    private const string SelectColumns = "id, name, currency, kind, is_active, created_at, updated_at";

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public CashBoxRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<bool> CreateAsync(CashBox cashBox, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO cash_boxes (id, name, currency, kind, is_active, created_at, updated_at)
            VALUES (@Id, @Name, @Currency, @Kind, @IsActive, @CreatedAt, @UpdatedAt)
            """, new
        {
            cashBox.Id,
            cashBox.Name,
            Currency = cashBox.Currency.ToString(),
            Kind = cashBox.Kind.ToString(),
            cashBox.IsActive,
            cashBox.CreatedAt,
            cashBox.UpdatedAt
        }, cancellationToken: cancellationToken));
        return result > 0;
    }

    public async Task<CashBox?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<CashBox>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM cash_boxes WHERE id = @id",
            new { id }, cancellationToken: cancellationToken));
    }

    public async Task<CashBox?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        // The column is COLLATE NOCASE, so "efectivo cup" finds "Efectivo CUP".
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<CashBox>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM cash_boxes WHERE name = @name",
            new { name }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<CashBox>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<CashBox>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM cash_boxes ORDER BY is_active DESC, created_at, name",
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(CashBox cashBox, CancellationToken cancellationToken = default)
    {
        // currency and kind are deliberately NOT updatable.
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE cash_boxes SET
                name = @Name,
                is_active = @IsActive,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """, new { cashBox.Id, cashBox.Name, cashBox.IsActive, cashBox.UpdatedAt },
            cancellationToken: cancellationToken));
        return result > 0;
    }

    public async Task<bool> HasActivePaymentMethodsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM payment_methods WHERE cash_box_id = @id AND is_active = 1)",
            new { id }, cancellationToken: cancellationToken));
    }
}
