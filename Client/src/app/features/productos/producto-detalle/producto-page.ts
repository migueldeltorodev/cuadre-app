import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { CategoriesApi, ProductsApi, SuppliersApi } from '../../../core/api/catalog-api';
import { PriceChangeResponse, ProductResponse, SupplierResponse } from '../../../core/api/models';
import { BusinessSettingsStore } from '../../../core/state/business-settings-store';
import { formatShortDate } from '../../../shared/format/date';
import { formatHundredths, formatMoney, formatPercent } from '../../../shared/format/money';
import {
  ONE_UNIT_MILLI,
  UNIT_LABELS,
  formatQuantityWithUnit,
  perUnit,
  unitName,
} from '../../../shared/format/quantity';
import { CategoryIndex, productState } from '../catalog-labels';

/**
 * One product (design "Producto", RF-PRD-03/05). What needs purchases — cost, suggested price, margin,
 * the cost chart and the purchases table — arrives with slice 3; stock by location with slice 4.
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
  protected readonly formatMoney = formatMoney;
  protected readonly formatPercent = formatPercent;
  protected readonly formatShortDate = formatShortDate;
  protected readonly formatQuantityWithUnit = formatQuantityWithUnit;
  protected readonly perUnit = perUnit;

  protected readonly product = signal<ProductResponse | null>(null);
  protected readonly history = signal<PriceChangeResponse[]>([]);
  protected readonly categories = signal(new CategoryIndex([]));
  protected readonly supplier = signal<SupplierResponse | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);

  protected readonly state = computed(() => {
    const product = this.product();
    return product ? productState(product) : null;
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

  ngOnInit(): void {
    forkJoin({
      product: this.productsApi.get(this.id()),
      history: this.productsApi.priceHistory(this.id()),
      categories: this.categoriesApi.getAll(),
      suppliers: this.suppliersApi.getAll(),
    }).subscribe({
      next: ({ product, history, categories, suppliers }) => {
        this.product.set(product);
        this.history.set(history.items);
        this.categories.set(new CategoryIndex(categories.items));
        this.supplier.set(suppliers.items.find((s) => s.id === product.usualSupplierId) ?? null);
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }
}
