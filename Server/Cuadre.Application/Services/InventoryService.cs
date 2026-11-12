using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

/// <summary>The read side of the Inventario screen: nothing here writes.</summary>
public interface IInventoryService
{
    /// <summary>Stock table: products × locations, totals and the low-stock flag (RF-INV-05).</summary>
    Task<StockOverview> GetStockAsync(GetStockOptions options, CancellationToken cancellationToken = default);

    /// <summary>Kardex of one product (RF-INV-07).</summary>
    Task<Result<Kardex>> GetKardexAsync(GetKardexOptions options, CancellationToken cancellationToken = default);

    /// <summary>"Movimientos de hoy" (or of any day).</summary>
    Task<IEnumerable<StockMovementEntry>> GetMovementsOfDayAsync(DateOnly? date,
        CancellationToken cancellationToken = default);

    /// <summary>Suggested purchases from the low-stock products (RF-INV-06).</summary>
    Task<IEnumerable<ShoppingListItem>> GetShoppingListAsync(CancellationToken cancellationToken = default);
}

public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly IValidator<GetKardexOptions> _kardexValidator;
    private readonly TimeProvider _clock;

    public InventoryService(IInventoryRepository repository, IProductRepository productRepository,
        IValidator<GetKardexOptions> kardexValidator, TimeProvider clock)
    {
        _repository = repository;
        _productRepository = productRepository;
        _kardexValidator = kardexValidator;
        _clock = clock;
    }

    public async Task<StockOverview> GetStockAsync(GetStockOptions options,
        CancellationToken cancellationToken = default)
    {
        var locations = (await _repository.GetStockLocationsAsync(cancellationToken)).ToList();
        var products = await _repository.GetProductStocksAsync(options, cancellationToken);

        var rows = options.LowOnly ? products.Where(product => product.IsLow).ToList() : products.ToList();
        return new StockOverview(locations, rows);
    }

    public async Task<Result<Kardex>> GetKardexAsync(GetKardexOptions options,
        CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(options.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Fail(NotFoundError.For<Product>(options.ProductId));
        }

        await _kardexValidator.ValidateOrThrowAsync(options, cancellationToken);

        var movements = (await _repository.GetProductMovementsAsync(options.ProductId, cancellationToken)).ToList();

        // Opening balance = everything before the period; the walk then continues from it.
        var balances = new Dictionary<Guid, long>();
        foreach (var movement in movements.Where(movement => options.From is { } start && movement.Date < start))
        {
            balances[movement.LocationId] = balances.GetValueOrDefault(movement.LocationId) + movement.QuantityMilli;
        }

        var opening = ToQuantities(balances);
        var inPeriod = movements.Where(movement =>
            (options.From is not { } start || movement.Date >= start) && (options.To is not { } end || movement.Date <= end));

        var entries = InventoryMath.RunningBalances(inPeriod, balances);
        var closing = ToQuantities(balances);

        // The location filter only hides rows: the balances shown are still the real ones.
        var visible = options.LocationId is { } locationId
            ? entries.Where(entry => entry.Movement.LocationId == locationId).ToList()
            : entries;

        return Result.Ok(new Kardex(product, opening, visible, closing));
    }

    public Task<IEnumerable<StockMovementEntry>> GetMovementsOfDayAsync(DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetMovementsOfDayAsync(date ?? _clock.Today(), cancellationToken);
    }

    public Task<IEnumerable<ShoppingListItem>> GetShoppingListAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetShoppingListAsync(cancellationToken);
    }

    private static List<LocationQuantity> ToQuantities(Dictionary<Guid, long> balances) =>
        balances.Where(pair => pair.Value != 0)
            .Select(pair => new LocationQuantity { LocationId = pair.Key, QuantityMilli = pair.Value })
            .ToList();
}
