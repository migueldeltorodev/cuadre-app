using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

public interface IPriceListRepository
{
    /// <summary>Active products grouped by category → subcategory → name.</summary>
    Task<IEnumerable<PriceListEntry>> GetEntriesAsync(CancellationToken cancellationToken = default);

    Task<PriceListCopy?> GetLastCopyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// "I copied the list to the sign": snapshots every active product's price into listed_price_cents
    /// and records the copy. One transaction, so the counts always match the snapshot.
    /// </summary>
    Task<PriceListCopy> MarkCopiedAsync(Guid id, DateTime copiedAt, CancellationToken cancellationToken = default);
}

public class PriceListRepository : IPriceListRepository
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public PriceListRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<IEnumerable<PriceListEntry>> GetEntriesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<PriceListEntry>(new CommandDefinition("""
            SELECT
                p.id AS product_id, p.code, p.name, p.brand, p.presentation, p.stock_unit,
                p.category_id, c.name AS category_name,
                p.subcategory_id, s.name AS subcategory_name,
                p.sale_price_cents, p.listed_price_cents
            FROM products p
                JOIN categories c ON c.id = p.category_id
                LEFT JOIN categories s ON s.id = p.subcategory_id
            WHERE p.is_active = 1
            ORDER BY c.name COLLATE NOCASE, s.name IS NOT NULL, s.name COLLATE NOCASE, p.name COLLATE NOCASE
            """, cancellationToken: cancellationToken));
    }

    public async Task<PriceListCopy?> GetLastCopyAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<PriceListCopy>(new CommandDefinition("""
            SELECT id, copied_at, product_count, changed_count
            FROM price_list_copies
            ORDER BY copied_at DESC
            LIMIT 1
            """, cancellationToken: cancellationToken));
    }

    public async Task<PriceListCopy> MarkCopiedAsync(Guid id, DateTime copiedAt,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // IS NOT compares NULLs as values: a never-listed product with a price counts as changed.
        var counts = await connection.QuerySingleAsync<(long Products, long Changed)>(new CommandDefinition("""
            SELECT COUNT(*), COALESCE(SUM(sale_price_cents IS NOT listed_price_cents), 0)
            FROM products
            WHERE is_active = 1
            """, transaction: transaction, cancellationToken: cancellationToken));

        // updated_at is NOT touched: copying the sign is not an edit of the product.
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE products SET listed_price_cents = sale_price_cents WHERE is_active = 1",
            transaction: transaction, cancellationToken: cancellationToken));

        var copy = new PriceListCopy
        {
            Id = id,
            CopiedAt = copiedAt,
            ProductCount = (int)counts.Products,
            ChangedCount = (int)counts.Changed
        };

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO price_list_copies (id, copied_at, product_count, changed_count)
            VALUES (@Id, @CopiedAt, @ProductCount, @ChangedCount)
            """, copy, transaction, cancellationToken: cancellationToken));

        transaction.Commit();
        return copy;
    }
}
