import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { CategoriesApi } from '../../../core/api/catalog-api';
import { ProductRankingReportResponse, ProductRankingResponse } from '../../../core/api/report-models';
import { ReportsApi } from '../../../core/api/reports-api';
import { formatShortDate } from '../../../shared/format/date';
import { formatMoney, formatPercent } from '../../../shared/format/money';
import { formatQuantityWithUnit } from '../../../shared/format/quantity';
import { Period, PeriodFilter } from '../period-filter';
import { categoryOptions } from '../report-format';

type SortKey = 'sales' | 'margin' | 'marginPercent' | 'loss';

const SORTS: Record<SortKey, (a: ProductRankingResponse, b: ProductRankingResponse) => number> = {
  sales: (a, b) => b.salesCents - a.salesCents,
  margin: (a, b) => b.marginCents - a.marginCents,
  marginPercent: (a, b) => (b.marginBps ?? -Infinity) - (a.marginBps ?? -Infinity),
  loss: (a, b) => b.lossCents - a.lossCents,
};

/**
 * RF-REP-04: every product of the period with what it sold, what it left and what was lost. Quantities are in
 * different units, so the order is by money; the server sends the rows, this page only sorts them.
 */
@Component({
  imports: [RouterLink, PeriodFilter],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './product-ranking-page.html',
  styleUrls: ['../../../shared/ui/list-page.css', '../report-page.css'],
})
export default class ProductRankingPage implements OnInit {
  private readonly api = inject(ReportsApi);
  private readonly categoriesApi = inject(CategoriesApi);

  protected readonly formatMoney = formatMoney;
  protected readonly formatPercent = formatPercent;
  protected readonly formatShortDate = formatShortDate;
  protected readonly formatQuantityWithUnit = formatQuantityWithUnit;

  protected readonly report = signal<ProductRankingReportResponse | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly loading = signal(false);
  protected readonly today = signal<string | null>(null);
  protected readonly categoryId = signal('');
  protected readonly sort = signal<SortKey>('sales');
  protected readonly categories = signal<{ id: string; label: string }[]>([]);

  protected readonly rows = computed(() => [...(this.report()?.items ?? [])].sort(SORTS[this.sort()]));

  protected readonly totals = computed(() => {
    const items = this.report()?.items ?? [];
    const sales = items.reduce((sum, x) => sum + x.salesCents, 0);
    const margin = items.reduce((sum, x) => sum + x.marginCents, 0);
    return {
      sales,
      margin,
      marginBps: sales > 0 ? Math.round((margin * 10_000) / sales) : null,
      loss: items.reduce((sum, x) => sum + x.lossCents, 0),
    };
  });

  ngOnInit(): void {
    this.load();
    this.categoriesApi.getAll().subscribe({
      next: (response) => this.categories.set(categoryOptions(response.items)),
      error: () => this.categories.set([]),
    });
  }

  protected setPeriod(period: Period): void {
    this.load(period);
  }

  protected setCategory(id: string): void {
    this.categoryId.set(id);
    const r = this.report();
    this.load(r ? { from: r.from, to: r.to } : undefined);
  }

  private load(period?: Period): void {
    this.loading.set(true);
    this.api.productRanking({ ...period, categoryId: this.categoryId() || undefined }).subscribe({
      next: (report) => {
        if (!this.today() && !period) {
          this.today.set(report.to);
        }
        this.report.set(report);
        this.loadError.set(null);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loadError.set(toApiError(err));
        this.loading.set(false);
      },
    });
  }
}
