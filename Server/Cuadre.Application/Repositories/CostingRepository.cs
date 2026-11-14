using Cuadre.Application.Database;
using Cuadre.Application.Models;

namespace Cuadre.Application.Repositories;

public interface ICostingRepository
{
    /// <summary>
    /// The FIFO batches (D-60) of the products, replaying their ledger up to that date included (null = everything).
    /// Every product asked for gets a book, empty when it has no movements.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, FifoBook>> GetBooksAsync(IReadOnlyCollection<Guid> productIds,
        DateOnly? upToDate, CancellationToken cancellationToken = default);
}

public class CostingRepository : ICostingRepository
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public CostingRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<IReadOnlyDictionary<Guid, FifoBook>> GetBooksAsync(IReadOnlyCollection<Guid> productIds,
        DateOnly? upToDate, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await FifoLedger.LoadAsync(connection, null, productIds, upToDate, cancellationToken);
    }
}
