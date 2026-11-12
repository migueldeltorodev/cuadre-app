using Cuadre.Application.Common;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;

namespace Cuadre.Application.Services;

public interface IPriceListService
{
    Task<PriceList> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>"Ya lo copié al cartel": from now on, only prices changed after this moment are flagged.</summary>
    Task<PriceListCopy> MarkCopiedAsync(CancellationToken cancellationToken = default);
}

public class PriceListService : IPriceListService
{
    private readonly IPriceListRepository _repository;
    private readonly TimeProvider _clock;

    public PriceListService(IPriceListRepository repository, TimeProvider clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<PriceList> GetAsync(CancellationToken cancellationToken = default)
    {
        var entries = await _repository.GetEntriesAsync(cancellationToken);
        var lastCopy = await _repository.GetLastCopyAsync(cancellationToken);
        return new PriceList(lastCopy, entries.ToList());
    }

    public Task<PriceListCopy> MarkCopiedAsync(CancellationToken cancellationToken = default)
    {
        return _repository.MarkCopiedAsync(Guid.NewGuid(), _clock.UtcNow(), cancellationToken);
    }
}
