using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

public interface IPaymentMethodRepository
{
    Task<bool> CreateAsync(PaymentMethod paymentMethod, CancellationToken cancellationToken = default);
    Task<PaymentMethod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaymentMethod?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IEnumerable<PaymentMethod>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(PaymentMethod paymentMethod, CancellationToken cancellationToken = default);
}

/// <summary>No delete on purpose: payments will reference these rows, so they are deactivated instead.</summary>
public class PaymentMethodRepository : IPaymentMethodRepository
{
    private const string SelectColumns = "id, name, cash_box_id, is_active, created_at, updated_at";

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public PaymentMethodRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<bool> CreateAsync(PaymentMethod paymentMethod, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO payment_methods (id, name, cash_box_id, is_active, created_at, updated_at)
            VALUES (@Id, @Name, @CashBoxId, @IsActive, @CreatedAt, @UpdatedAt)
            """, paymentMethod, cancellationToken: cancellationToken));
        return result > 0;
    }

    public async Task<PaymentMethod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<PaymentMethod>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM payment_methods WHERE id = @id",
            new { id }, cancellationToken: cancellationToken));
    }

    public async Task<PaymentMethod?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<PaymentMethod>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM payment_methods WHERE name = @name",
            new { name }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<PaymentMethod>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<PaymentMethod>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM payment_methods ORDER BY is_active DESC, created_at, name",
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(PaymentMethod paymentMethod, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE payment_methods SET
                name = @Name,
                cash_box_id = @CashBoxId,
                is_active = @IsActive,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """, paymentMethod, cancellationToken: cancellationToken));
        return result > 0;
    }
}
