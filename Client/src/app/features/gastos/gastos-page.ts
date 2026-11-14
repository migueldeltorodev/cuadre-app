import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../core/api/api-error';
import { EnvelopesApi } from '../../core/api/envelopes-api';
import { ExpenseCategoriesApi, ExpensesApi, RecurringExpensesApi } from '../../core/api/expenses-api';
import {
  DueExpenseResponse,
  EnvelopeOverviewResponse,
  ExpenseCategoryResponse,
  ExpenseResponse,
  ExpensesResponse,
} from '../../core/api/models';
import { ClosingAlertsStore } from '../../core/state/closing-alerts-store';
import { formatShortDate } from '../../shared/format/date';
import { formatMoney } from '../../shared/format/money';
import { DIALOG_DEFAULTS } from '../../shared/ui/dialog-config';
import { firstOfMonth, formatBalance } from '../sobres/envelope-format';
import { ExpenseDialog, ExpenseDialogData } from './expense-dialog';
import { duePeriodLabel, expenseStateLabel } from './expense-format';

const PAGE_SIZE = 25;

/**
 * Gastos (RF-GAS-01/02): the recurring ones waiting to be paid, then every expense, newest first.
 * Starts on this month; the totals add up the whole filter, per currency.
 */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './gastos-page.html',
  styleUrls: ['../../shared/ui/list-page.css', './gastos-page.css'],
})
export default class GastosPage implements OnInit {
  private readonly dialog = inject(Dialog);
  private readonly api = inject(ExpensesApi);
  private readonly recurringApi = inject(RecurringExpensesApi);
  private readonly categoriesApi = inject(ExpenseCategoriesApi);
  private readonly envelopesApi = inject(EnvelopesApi);
  protected readonly alerts = inject(ClosingAlertsStore);

  protected readonly formatShortDate = formatShortDate;
  protected readonly formatMoney = formatMoney;
  protected readonly stateLabel = expenseStateLabel;
  protected readonly periodLabel = duePeriodLabel;

  protected readonly result = signal<ExpensesResponse | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly overview = signal<EnvelopeOverviewResponse | null>(null);
  protected readonly categories = signal<ExpenseCategoryResponse[]>([]);

  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly categoryId = signal('');
  protected readonly cashBoxId = signal('');
  protected readonly includeVoided = signal(false);
  protected readonly page = signal(1);

  /** The last expense saved here, with its negative-envelope warning (allowed, RF-PF-08). */
  protected readonly lastSaved = signal<ExpenseResponse | null>(null);

  /** The occurrence asking "¿saltar?" (inline confirmation), and the result of skipping. */
  protected readonly skipping = signal<string | null>(null);
  protected readonly skipError = signal<ApiError | null>(null);

  protected readonly hasFilters = computed(
    () => !!(this.from() || this.to() || this.categoryId() || this.cashBoxId()),
  );

  protected readonly boxes = computed(() => this.overview()?.cashBoxes ?? []);

  ngOnInit(): void {
    this.envelopesApi.balances().subscribe({
      next: (overview) => {
        this.overview.set(overview);
        // This month by default: what the owner usually asks ("¿cuánto gasté este mes?").
        this.from.set(firstOfMonth(overview.date));
        this.load();
      },
      error: (err: unknown) => {
        this.loadError.set(toApiError(err));
        this.load();
      },
    });
    this.categoriesApi.getAll().subscribe({ next: (r) => this.categories.set(r.items) });
    this.alerts.refresh();
  }

  protected setFrom(value: string): void {
    this.from.set(value);
    this.reload();
  }

  protected setTo(value: string): void {
    this.to.set(value);
    this.reload();
  }

  protected setCategory(value: string): void {
    this.categoryId.set(value);
    this.reload();
  }

  protected setCashBox(value: string): void {
    this.cashBoxId.set(value);
    this.reload();
  }

  protected toggleVoided(checked: boolean): void {
    this.includeVoided.set(checked);
    this.reload();
  }

  protected clearFilters(): void {
    this.from.set('');
    this.to.set('');
    this.categoryId.set('');
    this.cashBoxId.set('');
    this.reload();
  }

  protected goTo(page: number): void {
    this.page.set(page);
    this.load();
  }

  protected newExpense(): void {
    this.openDialog(undefined);
  }

  protected pay(due: DueExpenseResponse): void {
    this.openDialog(due);
  }

  protected dueKey(due: DueExpenseResponse): string {
    return `${due.recurring.id}|${due.periodStart}`;
  }

  protected askSkip(due: DueExpenseResponse): void {
    this.skipError.set(null);
    this.skipping.set(this.dueKey(due));
  }

  protected confirmSkip(due: DueExpenseResponse): void {
    this.recurringApi.skip(due.recurring.id, due.periodStart).subscribe({
      next: () => {
        this.skipping.set(null);
        this.alerts.refresh();
      },
      error: (err: unknown) => this.skipError.set(toApiError(err)),
    });
  }

  protected warningText(saved: ExpenseResponse): string | null {
    const w = saved.warning;
    return w ? `${w.envelopeName} en ${w.cashBoxName} quedó en ${formatBalance(w.balanceCents, w.currency)}.` : null;
  }

  private openDialog(due: DueExpenseResponse | undefined): void {
    const overview = this.overview();
    if (!overview) {
      return;
    }
    this.dialog
      .open<ExpenseResponse, ExpenseDialogData>(ExpenseDialog, {
        ...DIALOG_DEFAULTS,
        width: '560px',
        data: { overview, categories: this.categories(), due },
      })
      .closed.subscribe((saved) => {
        if (saved) {
          this.lastSaved.set(saved);
          this.alerts.refresh();
          this.envelopesApi.balances().subscribe({ next: (fresh) => this.overview.set(fresh) });
          this.load();
        }
      });
  }

  private reload(): void {
    this.page.set(1);
    this.load();
  }

  private load(): void {
    this.api
      .getAll({
        from: this.from() || undefined,
        to: this.to() || undefined,
        categoryId: this.categoryId() || undefined,
        cashBoxId: this.cashBoxId() || undefined,
        includeVoided: this.includeVoided() || undefined,
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
