import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, input, linkedSignal, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { InventoryApi } from '../../../core/api/inventory-api';
import { KardexResponse, LocationQuantityResponse, ProductStockResponse, StockLocationResponse } from '../../../core/api/models';
import { formatShortDate } from '../../../shared/format/date';
import { formatQuantityWithUnit } from '../../../shared/format/quantity';
import { formatSignedQuantity, movementLabel, movementLink } from '../inventory-format';

/**
 * Historial por producto (RF-INV-06): every movement of one product, oldest first, with the running balance
 * of its location and of the whole business. Voided documents stay in the list (muted), next to their reversal.
 */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './kardex-tab.html',
  styleUrls: ['../inventory-shared.css'],
})
export default class KardexTab implements OnInit {
  /** ?producto=… (withComponentInputBinding): links from the stock and the operations land here. */
  readonly producto = input<string>();

  private readonly api = inject(InventoryApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly formatShortDate = formatShortDate;
  protected readonly formatQuantityWithUnit = formatQuantityWithUnit;
  protected readonly formatSignedQuantity = formatSignedQuantity;
  protected readonly movementLabel = movementLabel;
  protected readonly movementLink = movementLink;

  protected readonly products = signal<ProductStockResponse[]>([]);
  protected readonly locations = signal<StockLocationResponse[]>([]);
  protected readonly kardex = signal<KardexResponse | null>(null);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<ApiError | null>(null);

  protected readonly productId = linkedSignal(() => this.producto() ?? '');
  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly locationId = signal('');

  protected readonly hasFilters = computed(() => !!(this.from() || this.to() || this.locationId()));

  protected readonly opening = computed(() => this.sum(this.kardex()?.openingBalances ?? []));
  protected readonly closing = computed(() => this.sum(this.kardex()?.closingBalances ?? []));

  /** Newest first is easier to read; the balances were computed oldest first on the server. */
  protected readonly entries = computed(() => [...(this.kardex()?.entries ?? [])].reverse());

  constructor() {
    effect((onCleanup) => {
      const productId = this.productId();
      if (!productId) {
        this.kardex.set(null);
        return;
      }
      this.loading.set(true);
      const subscription = this.api
        .kardex(productId, {
          from: this.from() || undefined,
          to: this.to() || undefined,
          locationId: this.locationId() || undefined,
        })
        .subscribe({
          next: (kardex) => {
            this.kardex.set(kardex);
            this.loadError.set(null);
            this.loading.set(false);
          },
          error: (err: unknown) => {
            this.loadError.set(toApiError(err));
            this.loading.set(false);
          },
        });
      onCleanup(() => subscription.unsubscribe());
    });
  }

  ngOnInit(): void {
    this.api.stock({ includeInactive: true }).subscribe({
      next: (stock) => {
        this.products.set([...stock.items].sort((a, b) => a.name.localeCompare(b.name, 'es')));
        this.locations.set(stock.locations);
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }

  /** The product goes in the URL, so the page can be shared, reloaded or reached with "back". */
  protected selectProduct(id: string): void {
    this.productId.set(id);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { producto: id || null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  protected clearFilters(): void {
    this.from.set('');
    this.to.set('');
    this.locationId.set('');
  }

  /** "Saldo" of the business, or of the chosen location only. */
  private sum(balances: LocationQuantityResponse[]): number {
    const location = this.locationId();
    return balances
      .filter((balance) => !location || balance.locationId === location)
      .reduce((total, balance) => total + balance.quantityMilli, 0);
  }

  protected locationName(id: string): string {
    return this.locations().find((location) => location.id === id)?.name ?? '';
  }
}
