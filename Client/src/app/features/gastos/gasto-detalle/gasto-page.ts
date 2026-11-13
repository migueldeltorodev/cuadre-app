import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { ExpensesApi } from '../../../core/api/expenses-api';
import { ExpenseResponse } from '../../../core/api/models';
import { Breadcrumbs } from '../../../core/layout/breadcrumbs';
import { ClosingAlertsStore } from '../../../core/state/closing-alerts-store';
import { formatShortDate, formatTimestampDay } from '../../../shared/format/date';
import { formatMoney } from '../../../shared/format/money';
import { expenseStateLabel } from '../expense-format';

/**
 * One gasto. Never edited (RF-GAS-04): voiding asks for a reason and gives the money back to its envelope with a
 * movement dated today; the expense stays, marked as voided. A voided recurring payment is proposed again.
 */
@Component({
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './gasto-page.html',
  styleUrls: ['../../../shared/ui/list-page.css', '../../sobres/operacion/envelope-operation-page.css'],
})
export default class GastoPage implements OnInit {
  /** Route param (withComponentInputBinding). */
  readonly id = input.required<string>();

  private readonly api = inject(ExpensesApi);
  private readonly alerts = inject(ClosingAlertsStore);

  protected readonly formatShortDate = formatShortDate;
  protected readonly formatTimestampDay = formatTimestampDay;
  protected readonly formatMoney = formatMoney;

  protected readonly expense = signal<ExpenseResponse | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);

  /** The trail names this page once it is loaded (the shell draws it). */
  private readonly crumbs = inject(Breadcrumbs);
  protected readonly nameInTrail = effect(() => {
    const expense = this.expense();
    if (expense) {
      this.crumbs.set(`#${expense.number}`);
    }
  });

  protected readonly voiding = signal(false);
  protected readonly savingVoid = signal(false);
  protected readonly voidError = signal<ApiError | null>(null);
  protected readonly reason = inject(NonNullableFormBuilder).control('', [
    Validators.required,
    Validators.maxLength(300),
  ]);

  protected readonly state = computed(() => {
    const expense = this.expense();
    return expense ? expenseStateLabel(expense.status) : null;
  });

  ngOnInit(): void {
    this.api.get(this.id()).subscribe({
      next: (expense) => this.expense.set(expense),
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }

  protected startVoid(): void {
    this.reason.reset('');
    this.voidError.set(null);
    this.voiding.set(true);
  }

  protected cancelVoid(): void {
    this.voiding.set(false);
  }

  protected confirmVoid(): void {
    const expense = this.expense();
    this.reason.markAsTouched();
    if (!expense || this.reason.invalid || !this.reason.value.trim()) {
      return;
    }
    this.savingVoid.set(true);
    this.voidError.set(null);
    this.api.void(expense.id, this.reason.value.trim()).subscribe({
      next: (voided) => {
        this.expense.set(voided);
        this.voiding.set(false);
        this.savingVoid.set(false);
        this.alerts.refresh();
      },
      error: (err: unknown) => {
        this.voidError.set(toApiError(err));
        this.savingVoid.set(false);
      },
    });
  }
}
