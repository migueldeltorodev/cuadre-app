import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { ProductCostHistoryResponse } from '../../../core/api/report-models';
import { ReportsApi } from '../../../core/api/reports-api';
import { Breadcrumbs } from '../../../core/layout/breadcrumbs';
import { formatShortDate } from '../../../shared/format/date';
import { formatHundredths, formatMicros, formatMoney } from '../../../shared/format/money';
import { formatQuantityWithUnit, perUnit } from '../../../shared/format/quantity';
import { CostPoint, CostPriceChart, PricePoint } from '../../../shared/ui/charts/cost-price-chart';

/**
 * RF-REP-02 and RF-REP-03 for one product: the landed cost of every purchase against the sale price over time,
 * and what each supplier charged. The supplier filter narrows the chart and the purchases, never the comparison.
 */
@Component({
  imports: [RouterLink, CostPriceChart],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './product-costs-page.html',
  styleUrls: ['../../../shared/ui/list-page.css', '../report-page.css', './product-costs-page.css'],
})
export default class ProductCostsPage {
  private readonly api = inject(ReportsApi);
  private readonly crumbs = inject(Breadcrumbs);

  /** Route param (withComponentInputBinding). */
  readonly id = input.required<string>();

  protected readonly formatMoney = formatMoney;
  protected readonly formatMicros = formatMicros;
  protected readonly formatHundredths = formatHundredths;
  protected readonly formatShortDate = formatShortDate;
  protected readonly formatQuantityWithUnit = formatQuantityWithUnit;
  protected readonly perUnit = perUnit;

  protected readonly history = signal<ProductCostHistoryResponse | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly loading = signal(false);
  protected readonly supplierId = signal('');

  protected readonly nameInTrail = effect(() => {
    const history = this.history();
    if (history) {
      this.crumbs.set(history.name);
    }
  });

  /** Loads again whenever the product (route) or the supplier filter changes. */
  protected readonly loader = effect((onCleanup) => {
    const id = this.id();
    const supplierId = this.supplierId();
    this.loading.set(true);
    const subscription = this.api.productCosts(id, { supplierId: supplierId || undefined }).subscribe({
      next: (history) => {
        this.history.set(history);
        this.loadError.set(null);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loadError.set(toApiError(err));
        this.loading.set(false);
      },
    });
    // A newer product or filter wins: the older answer is dropped.
    onCleanup(() => subscription.unsubscribe());
  });

  protected readonly costs = computed<CostPoint[]>(() =>
    (this.history()?.purchases ?? []).map((p) => ({
      key: p.purchaseId,
      date: p.date,
      cupMicros: p.unitCostCupMicros,
      usdMicros: p.unitCostUsdMicros,
      supplierName: p.supplierName,
      label: `Compra #${p.purchaseNumber}`,
    })),
  );

  protected readonly prices = computed<PricePoint[]>(() =>
    (this.history()?.priceChanges ?? []).map((c) => ({ date: c.effectiveDate, cents: c.priceCents })),
  );

  /** The cheapest average, to mark it in the comparison. */
  protected readonly cheapestId = computed(() => {
    const suppliers = this.history()?.suppliers ?? [];
    return suppliers.length > 1 ? suppliers[0].supplierId : null;
  });

  /** Newest first for the tables (the chart wants them oldest first). */
  protected readonly purchasesNewestFirst = computed(() => [...(this.history()?.purchases ?? [])].reverse());
  protected readonly priceChangesNewestFirst = computed(() => [...(this.history()?.priceChanges ?? [])].reverse());
}
