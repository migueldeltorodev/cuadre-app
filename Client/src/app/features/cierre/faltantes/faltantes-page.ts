import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { CashDiscrepanciesApi } from '../../../core/api/closings-api';
import { CashDiscrepancyQuery, CashDiscrepancyResponse, PagedResponse } from '../../../core/api/models';
import { ClosingAlertsStore } from '../../../core/state/closing-alerts-store';
import { formatShortDate } from '../../../shared/format/date';
import { formatMoney } from '../../../shared/format/money';
import { discrepancyStateLabel } from '../closing-format';

const PAGE_SIZE = 25;

type View = 'pending' | 'shortages' | 'surpluses' | 'all';

const VIEWS: { value: View; label: string; query: CashDiscrepancyQuery }[] = [
  { value: 'pending', label: 'Por reclamar', query: { kind: 'Shortage', status: 'Pending' } },
  { value: 'shortages', label: 'Todos los faltantes', query: { kind: 'Shortage' } },
  { value: 'surpluses', label: 'Sobrantes', query: { kind: 'Surplus' } },
  { value: 'all', label: 'Todo', query: {} },
];

/** RF-CIE-13: the shortages to claim first, and the history of every shortage and surplus. */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './faltantes-page.html',
  styleUrls: ['../../../shared/ui/list-page.css', './faltantes-page.css'],
})
export default class FaltantesPage implements OnInit {
  private readonly api = inject(CashDiscrepanciesApi);
  protected readonly alerts = inject(ClosingAlertsStore);

  protected readonly formatShortDate = formatShortDate;
  protected readonly formatMoney = formatMoney;
  protected readonly stateLabel = discrepancyStateLabel;
  protected readonly views = VIEWS;

  protected readonly view = signal<View>('pending');
  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly page = signal(1);
  protected readonly result = signal<PagedResponse<CashDiscrepancyResponse> | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);

  protected readonly hasDates = computed(() => !!(this.from() || this.to()));

  ngOnInit(): void {
    this.load();
  }

  protected setView(view: View): void {
    this.view.set(view);
    this.reload();
  }

  protected setFrom(value: string): void {
    this.from.set(value);
    this.reload();
  }

  protected setTo(value: string): void {
    this.to.set(value);
    this.reload();
  }

  protected clearDates(): void {
    this.from.set('');
    this.to.set('');
    this.reload();
  }

  protected goTo(page: number): void {
    this.page.set(page);
    this.load();
  }

  private reload(): void {
    this.page.set(1);
    this.load();
  }

  private load(): void {
    const view = VIEWS.find((v) => v.value === this.view())!;
    this.api
      .getAll({
        ...view.query,
        from: this.from() || undefined,
        to: this.to() || undefined,
        page: this.page(),
        pageSize: PAGE_SIZE,
      })
      .subscribe({
        next: (result) => {
          this.result.set(result);
          this.loadError.set(null);
        },
        error: (err: unknown) => this.loadError.set(toApiError(err)),
      });
  }
}
