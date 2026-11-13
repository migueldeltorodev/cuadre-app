import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { EnvelopesApi } from '../../../core/api/envelopes-api';
import { ExpenseCategoriesApi, RecurringExpensesApi } from '../../../core/api/expenses-api';
import {
  EnvelopeOverviewResponse,
  ExpenseCategoryResponse,
  RecurringExpenseResponse,
} from '../../../core/api/models';
import { ClosingAlertsStore } from '../../../core/state/closing-alerts-store';
import { formatMoney } from '../../../shared/format/money';
import { DIALOG_DEFAULTS } from '../../../shared/ui/dialog-config';
import { planStartLabel } from '../../sobres/envelope-format';
import { scheduleLabel } from '../expense-format';
import { RecurringExpenseDialog, RecurringExpenseDialogData } from './recurring-expense-dialog';

/** Gastos fijos (RF-GAS-01): the templates. Paying or skipping what is due happens on the Gastos page. */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="head">
      <div>
        <h1 class="page-title">Gastos fijos</h1>
        <p class="page-subtitle">Lo que se paga siempre. Cuando vence, aparece en Gastos para pagarlo o saltarlo.</p>
      </div>
      <button class="btn btn--primary" type="button" [disabled]="!overview()" (click)="open()">+ Nuevo gasto fijo</button>
    </header>

    @if (alerts.dueExpensesCount() > 0) {
      <p class="alert alert--warn" role="status">
        {{ alerts.dueExpensesCount() === 1 ? 'Hay 1 vencimiento por pagar.' : 'Hay ' + alerts.dueExpensesCount() + ' vencimientos por pagar.' }}
        <a routerLink="/gastos">Ir a pagarlos</a>
      </p>
    }

    @if (loadError(); as e) {
      <p class="alert alert--bad" role="alert">{{ e.message }}</p>
    }

    @if (recurring(); as list) {
      <section class="card">
        <div class="rows">
          @for (item of list; track item.id) {
            <div class="row" [class.row--inactive]="!item.isActive">
              <div class="row__main">
                <div class="row__title">{{ item.name }}</div>
                <div class="row__sub">
                  {{ scheduleLabel(item) }} · {{ planStartLabel(item.startsOn) }} · {{ item.categoryName }} ·
                  {{ item.cashBoxName }}, {{ item.envelopeName }}
                </div>
              </div>
              <span class="num strong">{{ formatMoney(item.amountCents, item.currency) }}</span>
              @if (item.isActive) {
                <span class="pill pill--ok">Activo</span>
              } @else {
                <span class="pill pill--muted">Ya no se paga</span>
              }
              <button class="btn btn--ghost btn--small" type="button" [disabled]="!overview()" (click)="open(item)"
                [attr.aria-label]="'Editar ' + item.name">Editar</button>
            </div>
          } @empty {
            <div class="empty">
              <p class="card__lead">
                Todavía no hay gastos fijos. Agregá el gestor, el alquiler o lo que pagues siempre, y Cuadre te avisa
                cuando venzan.
              </p>
            </div>
          }
        </div>
      </section>

      @if (inactiveCount() > 0) {
        <p class="muted">Los que ya no se pagan quedan para la historia: sus pagos siguen en Gastos.</p>
      }
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 20px; }
    .head { display: flex; flex-wrap: wrap; gap: 16px; justify-content: space-between; align-items: flex-end; }
    .page-subtitle { margin: 6px 0 0; }
    .strong { font-weight: 600; }
    .muted { margin: 0; color: var(--c-muted); font-size: 13.5px; }
    .alert a { color: inherit; font-weight: 600; }
  `,
})
export default class GastosFijosPage implements OnInit {
  private readonly dialog = inject(Dialog);
  private readonly api = inject(RecurringExpensesApi);
  private readonly categoriesApi = inject(ExpenseCategoriesApi);
  private readonly envelopesApi = inject(EnvelopesApi);
  protected readonly alerts = inject(ClosingAlertsStore);

  protected readonly formatMoney = formatMoney;
  protected readonly scheduleLabel = scheduleLabel;
  protected readonly planStartLabel = planStartLabel;

  protected readonly recurring = signal<RecurringExpenseResponse[] | null>(null);
  protected readonly overview = signal<EnvelopeOverviewResponse | null>(null);
  protected readonly categories = signal<ExpenseCategoryResponse[]>([]);
  protected readonly loadError = signal<ApiError | null>(null);

  protected readonly inactiveCount = computed(() => (this.recurring() ?? []).filter((r) => !r.isActive).length);

  ngOnInit(): void {
    forkJoin({
      overview: this.envelopesApi.balances(),
      categories: this.categoriesApi.getAll(),
    }).subscribe({
      next: ({ overview, categories }) => {
        this.overview.set(overview);
        this.categories.set(categories.items);
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
    this.load();
  }

  protected open(recurring?: RecurringExpenseResponse): void {
    const overview = this.overview();
    if (!overview) {
      return;
    }
    this.dialog
      .open<RecurringExpenseResponse, RecurringExpenseDialogData>(RecurringExpenseDialog, {
        ...DIALOG_DEFAULTS,
        width: '600px',
        data: { recurring, overview, categories: this.categories() },
      })
      .closed.subscribe((saved) => {
        if (saved) {
          this.load();
          this.alerts.refresh();
        }
      });
  }

  private load(): void {
    this.api.getAll().subscribe({
      next: (result) => {
        this.recurring.set(result.items);
        this.loadError.set(null);
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }
}
