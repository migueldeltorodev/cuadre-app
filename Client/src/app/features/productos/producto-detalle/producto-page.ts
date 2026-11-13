import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { CategoriesApi, ProductsApi, SuppliersApi } from '../../../core/api/catalog-api';
import {
  PriceChangeResponse,
  ProductPurchaseResponse,
  ProductResponse,
  SupplierResponse,
} from '../../../core/api/models';
import { BusinessSettingsStore } from '../../../core/state/business-settings-store';
import { formatShortDate } from '../../../shared/format/date';
import { formatHundredths, formatMicros, formatMoney, formatPercent } from '../../../shared/format/money';
import {
  ONE_UNIT_MILLI,
  UNIT_LABELS,
  formatQuantityWithUnit,
  perUnit,
  unitName,
} from '../../../shared/format/quantity';
import { CategoryIndex, isLowStock, productState } from '../catalog-labels';

/** What the colchón says about today's rate (RN-04), ready to print. */
interface BufferStatus {
  tone: 'ok' | 'warn' | 'muted';
  title: string;
  text: string;
}

/**
 * One product (design "Producto", RF-PRD-03/05). Cost, suggested price and margin come from the server
 * (never recomputed here); stock by location and the cost chart arrive with inventory (slice 4).
 */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './producto-page.html',
  styleUrl: './producto-page.css',
})
export default class ProductoPage implements OnInit {
  /** Route param (withComponentInputBinding). */
  readonly id = input.required<string>();

  private readonly productsApi = inject(ProductsApi);
  private readonly categoriesApi = inject(CategoriesApi);
  private readonly suppliersApi = inject(SuppliersApi);
  protected readonly settings = inject(BusinessSettingsStore).settings;

  protected readonly formatHundredths = formatHundredths;
  protected readonly formatMicros = formatMicros;
  protected readonly formatMoney = formatMoney;
  protected readonly formatPercent = formatPercent;
  protected readonly formatShortDate = formatShortDate;
  protected readonly formatQuantityWithUnit = formatQuantityWithUnit;
  protected readonly perUnit = perUnit;

  protected readonly product = signal<ProductResponse | null>(null);
  protected readonly history = signal<PriceChangeResponse[]>([]);
  protected readonly purchases = signal<ProductPurchaseResponse[]>([]);
  protected readonly categories = signal(new CategoryIndex([]));
  protected readonly supplier = signal<SupplierResponse | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);

  /** "Mantener": hides the suggestion for this visit. It comes back while the price stays under it. */
  protected readonly suggestionDismissed = signal(false);
  protected readonly savingPrice = signal(false);
  protected readonly priceError = signal<ApiError | null>(null);

  protected readonly state = computed(() => {
    const product = this.product();
    return product ? productState(product) : null;
  });

  protected readonly lowStock = computed(() => {
    const product = this.product();
    return product ? isLowStock(product) : false;
  });

  /** The suggestion worth showing: there is a cost and it differs from the price. */
  protected readonly suggestion = computed(() => {
    const product = this.product();
    const cost = product?.cost;
    if (!product || !cost || this.suggestionDismissed() || cost.suggestedPriceCents === product.salePriceCents) {
      return null;
    }
    return cost.suggestedPriceCents;
  });

  /** The purchase the current cost comes from: accepting its suggestion is recorded as "Por una compra". */
  private readonly lastPurchaseId = computed(
    () => this.purchases().find((purchase) => purchase.status === 'Confirmed')?.purchaseId ?? null,
  );

  /**
   * The actual recargo of the CURRENT price over the CUP cost. Display only: the server owns the
   * suggested price; this just tells you what you are charging today.
   */
  protected readonly actualMarkupBps = computed(() => {
    const product = this.product();
    const cost = product?.cost;
    if (!product?.salePriceCents || !cost) {
      return null;
    }
    const costCents = cost.avgCostCupMicros / 10_000;
    return Math.round(((product.salePriceCents - costCents) / costCents) * 10_000);
  });

  protected readonly bufferStatus = computed<BufferStatus | null>(() => {
    const cost = this.product()?.cost;
    if (!cost) {
      return null;
    }
    const limit = cost.averageRateCents + cost.effectiveBufferCents;
    const limitText = `${formatHundredths(cost.averageRateCents)} + ${formatHundredths(cost.effectiveBufferCents)} = ${formatHundredths(limit)}`;
    if (cost.todayRateCents === null) {
      return {
        tone: 'muted',
        title: 'Falta la tasa.',
        text: `Cargá la tasa de hoy para saber si el colchón te cubre (límite ${limitText}).`,
      };
    }
    const today = formatHundredths(cost.todayRateCents);
    if (cost.isBufferExceeded) {
      return {
        tone: 'warn',
        title: 'Colchón superado.',
        text: `La tasa de hoy (${today}) pasó el límite (${limitText}). Revisá el precio: estás reponiendo más caro.`,
      };
    }
    return {
      tone: 'ok',
      title: 'El colchón te cubre.',
      text: `La tasa de hoy (${today}) está ${formatHundredths(limit - cost.todayRateCents)} CUP por debajo del límite (${limitText}).`,
    };
  });

  /** "Granel · se vende por libra" / "Se vende por unidad". */
  protected readonly saleUnitText = computed(() => {
    const product = this.product();
    if (!product) {
      return '';
    }
    const unit = UNIT_LABELS[product.stockUnit].one;
    return product.allowsDecimals ? `Granel · se vende por ${unit}` : `Se vende por ${unit}`;
  });

  /** "Saco = 100 lb", or "Unidad" when it is bought and sold the same way. */
  protected readonly purchaseText = computed(() => {
    const product = this.product();
    if (!product) {
      return '';
    }
    if (product.purchaseUnit === product.stockUnit && product.conversionFactorMilli === ONE_UNIT_MILLI) {
      return unitName(product.purchaseUnit);
    }
    return `${unitName(product.purchaseUnit)} = ${formatQuantityWithUnit(product.conversionFactorMilli, product.stockUnit)}`;
  });

  /** The product's own value, or the business default, saying which one it is. */
  protected readonly markupText = computed(() => {
    const product = this.product();
    if (product?.markupBps != null) {
      return `${formatPercent(product.markupBps)} (propio)`;
    }
    const settings = this.settings();
    return settings ? `${formatPercent(settings.defaultMarkupBps)} (el del negocio)` : '—';
  });

  protected readonly bufferText = computed(() => {
    const product = this.product();
    if (product?.exchangeBufferCents != null) {
      return `${formatMoney(product.exchangeBufferCents)} (propio)`;
    }
    const settings = this.settings();
    return settings ? `${formatMoney(settings.exchangeBufferCents)} (el del negocio)` : '—';
  });

  /** "2 proveedores" over the confirmed purchases. */
  protected readonly supplierCount = computed(() => {
    const names = new Set(this.purchases().filter((p) => p.status === 'Confirmed').map((p) => p.supplierName));
    return names.size === 1 ? '1 proveedor' : `${names.size} proveedores`;
  });

  ngOnInit(): void {
    forkJoin({
      product: this.productsApi.get(this.id()),
      history: this.productsApi.priceHistory(this.id()),
      purchases: this.productsApi.purchases(this.id()),
      categories: this.categoriesApi.getAll(),
      suppliers: this.suppliersApi.getAll(),
    }).subscribe({
      next: ({ product, history, purchases, categories, suppliers }) => {
        this.product.set(product);
        this.history.set(history.items);
        this.purchases.set(purchases.items);
        this.categories.set(new CategoryIndex(categories.items));
        this.supplier.set(suppliers.items.find((s) => s.id === product.usualSupplierId) ?? null);
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }

  protected useSuggestion(priceCents: number): void {
    const product = this.product();
    if (!product) {
      return;
    }
    this.savingPrice.set(true);
    this.priceError.set(null);
    this.productsApi
      .setSalePrice(product.id, { salePriceCents: priceCents, purchaseId: this.lastPurchaseId() })
      .subscribe({
        next: (updated) => {
          this.product.set(updated);
          this.savingPrice.set(false);
          this.productsApi.priceHistory(updated.id).subscribe((history) => this.history.set(history.items));
        },
        error: (err: unknown) => {
          this.priceError.set(toApiError(err));
          this.savingPrice.set(false);
        },
      });
  }
}
