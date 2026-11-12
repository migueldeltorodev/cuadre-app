import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { CategoriesApi } from '../../../core/api/catalog-api';
import { InventoryApi } from '../../../core/api/inventory-api';
import {
  CategoryResponse,
  ProductStockResponse,
  ShoppingListItemResponse,
  StockLocationResponse,
  StockMovementResponse,
  StockResponse,
} from '../../../core/api/models';
import { ExchangeRateStore } from '../../../core/state/exchange-rate-store';
import { formatShortDate } from '../../../shared/format/date';
import { formatQuantityWithUnit } from '../../../shared/format/quantity';
import { localToday } from '../../compras/purchase-format';
import {
  COUNT_REMINDER_DAYS,
  daysBetween,
  formatSignedQuantity,
  inPurchaseUnits,
  movementLabel,
  movementLink,
} from '../inventory-format';

interface CountReminder {
  location: StockLocationResponse;
  /** Null = never counted. */
  days: number | null;
  due: boolean;
}

/**
 * Stock tab (design: Inventario). Products × locations with the total, the minimum and the low flag (RF-INV-05);
 * on the side, today's movements and the reminder to count the storage (RF-INV-08).
 */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './stock-tab.html',
  styleUrls: ['../inventory-shared.css', './stock-tab.css'],
})
export default class StockTab implements OnInit {
  private readonly inventoryApi = inject(InventoryApi);
  private readonly categoriesApi = inject(CategoriesApi);
  private readonly rateStore = inject(ExchangeRateStore);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly formatQuantityWithUnit = formatQuantityWithUnit;
  protected readonly formatSignedQuantity = formatSignedQuantity;
  protected readonly formatShortDate = formatShortDate;
  protected readonly movementLabel = movementLabel;
  protected readonly movementLink = movementLink;

  protected readonly stock = signal<StockResponse | null>(null);
  protected readonly movements = signal<StockMovementResponse[]>([]);
  protected readonly shoppingList = signal<ShoppingListItemResponse[]>([]);
  protected readonly categories = signal<CategoryResponse[]>([]);
  protected readonly loadError = signal<ApiError | null>(null);

  protected readonly search = signal('');
  protected readonly categoryId = signal('');
  protected readonly lowOnly = signal(false);
  protected readonly includeInactive = signal(false);
  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  protected readonly hasFilters = computed(
    () => !!(this.search() || this.categoryId() || this.lowOnly() || this.includeInactive()),
  );

  protected readonly topCategories = computed(() => this.categories().filter((c) => c.parentId === null));

  private readonly today = computed(() => this.rateStore.current()?.today ?? localToday());

  /** Storage locations first: those are the ones counted by hand (the counter is counted at the close). */
  protected readonly reminders = computed<CountReminder[]>(() => {
    const locations = this.stock()?.locations ?? [];
    return locations
      .filter((location) => location.kind === 'Storage')
      .map((location) => {
        const days = location.lastCountDate ? daysBetween(location.lastCountDate, this.today()) : null;
        return { location, days, due: days === null || days >= COUNT_REMINDER_DAYS };
      });
  });

  ngOnInit(): void {
    this.categoriesApi.getAll().subscribe({
      next: (response) => this.categories.set(response.items),
      error: () => this.categories.set([]),
    });
    this.inventoryApi.movements().subscribe({
      next: (response) => this.movements.set([...response.items].reverse()),   // newest first
      error: () => this.movements.set([]),
    });
    this.inventoryApi.shoppingList().subscribe({
      next: (response) => this.shoppingList.set(response.items),
      error: () => this.shoppingList.set([]),
    });
    this.destroyRef.onDestroy(() => clearTimeout(this.searchTimer));
    this.load();
  }

  protected setSearch(value: string): void {
    this.search.set(value);
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.load(), 250);
  }

  protected setCategory(value: string): void {
    this.categoryId.set(value);
    this.load();
  }

  protected toggleLowOnly(checked: boolean): void {
    this.lowOnly.set(checked);
    this.load();
  }

  protected toggleInactive(checked: boolean): void {
    this.includeInactive.set(checked);
    this.load();
  }

  protected clearFilters(): void {
    this.search.set('');
    this.categoryId.set('');
    this.lowOnly.set(false);
    this.includeInactive.set(false);
    this.load();
  }

  protected quantityAt(product: ProductStockResponse, locationId: string): number {
    return product.locations.find((l) => l.locationId === locationId)?.quantityMilli ?? 0;
  }

  /** "2 sacos" under "200 lb" when exact. */
  protected alt(product: ProductStockResponse, milli: number): string | null {
    return inPurchaseUnits(milli, product.purchaseUnit, product.stockUnit, product.conversionFactorMilli);
  }

  protected hasNegative(product: ProductStockResponse): boolean {
    return product.locations.some((l) => l.quantityMilli < 0);
  }

  private load(): void {
    this.inventoryApi
      .stock({
        search: this.search().trim() || undefined,
        categoryId: this.categoryId() || undefined,
        lowOnly: this.lowOnly() || undefined,
        includeInactive: this.includeInactive() || undefined,
      })
      .subscribe({
        next: (stock) => {
          this.stock.set(stock);
          this.loadError.set(null);
        },
        error: (err: unknown) => this.loadError.set(toApiError(err)),
      });
  }
}
