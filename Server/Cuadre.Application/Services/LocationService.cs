using Cuadre.Application.Models;
using Cuadre.Application.Repositories;

namespace Cuadre.Application.Services;

/// <summary>Read-only in slice 3: the purchase form needs to list where the goods can land.</summary>
public interface ILocationService
{
    Task<IEnumerable<Location>> GetAllAsync(CancellationToken cancellationToken = default);
}

public class LocationService : ILocationService
{
    private readonly ILocationRepository _repository;

    public LocationService(ILocationRepository repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<Location>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }
}
