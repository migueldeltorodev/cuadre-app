import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { CategoriesApi, SuppliersApi } from '../../../core/api/catalog-api';
import { SupplierResponse } from '../../../core/api/models';
import { CostChangesReportResponse } from '../../../core/api/report-models';
import { ReportsApi } from '../../../core/api/reports-api';
import { formatShortDate } from '../../../shared/format/date';
import { formatMicros, formatMoney, formatPercent } from '../../../shared/format/money';
import { perUnit } from '../../../shared/format/quantity';
import { Period, PeriodFilter } from '../period-filter';
import { categoryOptions } from '../report-format';

/**
 * RF-REP-02 for a category or a supplier (D-50): each product's first and last landed cost of the period and how
 * much it moved. Products with different units are never averaged together. Click one for its full history.
 */
@Component({
  imports: [RouterLink, PeriodFilter],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cost-changes-page.html',
  styleUrls: ['../../../shared/ui/list-page.css', '../report-page.css'],
})
export default class CostChangesPage implements OnInit {
  private readonly api = inject(ReportsApi);
  private readonly categoriesApi = inject(CategoriesApi);
  private readonly suppliersApi = inject(SuppliersApi);

  protected readonly formatMoney = formatMoney;
  protected readonly formatMicros = formatMicros;
  protected readonly formatShortDate = formatShortDate;
  protected readonly perUnit = perUnit;

  protected readonly report = signal<CostChangesReportResponse | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly loading = signal(false);
  protected readonly today = signal<string | null>(null);
  protected readonly categoryId = signal('');
  protected readonly supplierId = signal('');
  protected readonly categories = signal<{ id: string; label: string }[]>([]);
  protected readonly suppliers = signal<SupplierResponse[]>([]);

  ngOnInit(): void {
    this.load();
    this.categoriesApi.getAll().subscribe({
      next: (response) => this.categories.set(categoryOptions(response.items)),
      error: () => this.categories.set([]),
    });
    this.suppliersApi.getAll().subscribe({
      next: (response) => this.suppliers.set([...response.items].sort((a, b) => a.name.localeCompare(b.name, 'es'))),
      error: () => this.suppliers.set([]),
    });
  }

  protected setCategory(id: string): void {
    this.categoryId.set(id);
    this.reload();
  }

  protected setSupplier(id: string): void {
    this.supplierId.set(id);
    this.reload();
  }

  /** "+10%" / "−3.5%" / "igual". */
  protected change(bps: number | null): { text: string; tone: 'up' | 'down' | 'flat' } {
    if (bps === null || bps === 0) {
      return { text: bps === null ? '—' : 'igual', tone: 'flat' };
    }
    return bps > 0
      ? { text: `+${formatPercent(bps)}`, tone: 'up' }
      : { text: `−${formatPercent(-bps)}`, tone: 'down' };
  }

  protected load(period?: Period): void {
    this.loading.set(true);
    this.api
      .costChanges({
        ...period,
        categoryId: this.categoryId() || undefined,
        supplierId: this.supplierId() || undefined,
      })
      .subscribe({
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

  private reload(): void {
    const r = this.report();
    this.load(r ? { from: r.from, to: r.to } : undefined);
  }
}
