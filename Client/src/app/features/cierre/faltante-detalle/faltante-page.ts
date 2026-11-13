import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { CashDiscrepanciesApi } from '../../../core/api/closings-api';
import { CashBoxesApi } from '../../../core/api/configuration-api';
import { CashBoxResponse, CashDiscrepancyResponse, ShortageRecoveryResponse } from '../../../core/api/models';
import { Breadcrumbs } from '../../../core/layout/breadcrumbs';
import { ClosingAlertsStore } from '../../../core/state/closing-alerts-store';
import { formatShortDate, formatTimestampDay } from '../../../shared/format/date';
import { formatMoney, parseHundredths, toInputText } from '../../../shared/format/money';
import { amountValidator } from '../../../shared/format/money-validators';
import { discrepancyStateLabel } from '../closing-format';

type Action = 'recover' | 'writeOff' | null;

/**
 * One shortage or surplus. A pending shortage is claimed here: each recovery comes in on its day and is split like
 * an A3 (D-35); what never comes back is written off with a note (D-36). A recovery is never edited: it is voided.
 */
@Component({
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './faltante-page.html',
  styleUrls: ['../../../shared/ui/list-page.css', './faltante-page.css'],
})
export default class FaltantePage implements OnInit {
  /** Route param (withComponentInputBinding). */
  readonly id = input.required<string>();

  private readonly api = inject(CashDiscrepanciesApi);
  private readonly cashBoxesApi = inject(CashBoxesApi);
  private readonly alerts = inject(ClosingAlertsStore);
  private readonly crumbs = inject(Breadcrumbs);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly formatShortDate = formatShortDate;
  protected readonly formatTimestampDay = formatTimestampDay;
  protected readonly formatMoney = formatMoney;

  protected readonly discrepancy = signal<CashDiscrepancyResponse | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly cashBoxes = signal<CashBoxResponse[]>([]);

  protected readonly action = signal<Action>(null);
  protected readonly saving = signal(false);
  protected readonly actionError = signal<ApiError | null>(null);

  /** The recovery being voided (inline, in its row). */
  protected readonly voiding = signal<string | null>(null);
  protected readonly voidError = signal<ApiError | null>(null);

  protected readonly recoverForm = this.fb.group({
    date: [''],
    cashBoxId: ['', Validators.required],
    amount: ['', amountValidator({ min: 1 })],
    note: ['', Validators.maxLength(300)],
  });
  protected readonly writeOffNote = this.fb.control('', [Validators.required, Validators.maxLength(300)]);
  protected readonly voidReason = this.fb.control('', [Validators.required, Validators.maxLength(300)]);

  protected readonly state = computed(() => {
    const d = this.discrepancy();
    return d ? discrepancyStateLabel(d.status) : null;
  });

  protected readonly title = computed(() => {
    const d = this.discrepancy();
    return d ? `${d.kind === 'Shortage' ? 'Faltante' : 'Sobrante'} del ${formatShortDate(d.date)}` : '';
  });

  protected readonly canClaim = computed(() => {
    const d = this.discrepancy();
    return !!d && d.kind === 'Shortage' && d.status === 'Pending';
  });

  /** Recoveries can be voided while the shortage is still being claimed or was recovered. */
  protected readonly canVoid = computed(() => {
    const status = this.discrepancy()?.status;
    return status === 'Pending' || status === 'Recovered';
  });

  /** Typed amount above what is left: told before sending. */
  protected readonly tooMuch = signal(false);

  protected readonly nameInTrail = effect(() => {
    const title = this.title();
    if (title) {
      this.crumbs.set(title);
    }
  });

  ngOnInit(): void {
    this.api.get(this.id()).subscribe({
      next: (d) => this.discrepancy.set(d),
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
    // Money comes back in pesos, to an active CUP box (cash or bank).
    this.cashBoxesApi.getAll().subscribe({
      next: (boxes) => {
        const cup = boxes.items.filter((box) => box.isActive && box.currency === 'CUP');
        this.cashBoxes.set(cup);
        const cash = cup.find((box) => box.kind === 'Cash') ?? cup[0];
        if (cash && !this.recoverForm.controls.cashBoxId.value) {
          this.recoverForm.controls.cashBoxId.setValue(cash.id);
        }
      },
    });
  }

  protected start(action: Exclude<Action, null>): void {
    this.actionError.set(null);
    this.tooMuch.set(false);
    if (action === 'recover') {
      const pending = this.discrepancy()?.pendingCents ?? 0;
      this.recoverForm.patchValue({ date: '', amount: pending > 0 ? toInputText(pending) : '', note: '' });
      this.recoverForm.markAsUntouched();
    } else {
      this.writeOffNote.reset('');
    }
    this.action.set(action);
  }

  protected cancel(): void {
    this.action.set(null);
  }

  protected recover(): void {
    const d = this.discrepancy();
    this.recoverForm.markAllAsTouched();
    if (!d || this.recoverForm.invalid) {
      return;
    }

    const amountCents = parseHundredths(this.recoverForm.controls.amount.value)!;
    if (amountCents > d.pendingCents) {
      this.tooMuch.set(true);
      return;
    }
    this.tooMuch.set(false);

    const { date, cashBoxId, note } = this.recoverForm.getRawValue();
    this.run(this.api.recover(d.id, { date: date || null, cashBoxId, amountCents, note: note.trim() || null }));
  }

  protected writeOff(): void {
    const d = this.discrepancy();
    this.writeOffNote.markAsTouched();
    if (!d || this.writeOffNote.invalid || !this.writeOffNote.value.trim()) {
      return;
    }
    this.run(this.api.writeOff(d.id, this.writeOffNote.value.trim()));
  }

  protected startVoid(recovery: ShortageRecoveryResponse): void {
    this.voidReason.reset('');
    this.voidError.set(null);
    this.voiding.set(recovery.id);
  }

  protected confirmVoid(): void {
    const id = this.voiding();
    this.voidReason.markAsTouched();
    if (!id || this.voidReason.invalid || !this.voidReason.value.trim()) {
      return;
    }
    this.saving.set(true);
    this.api.voidRecovery(id, this.voidReason.value.trim()).subscribe({
      next: (d) => {
        this.discrepancy.set(d);
        this.voiding.set(null);
        this.saving.set(false);
        this.alerts.refresh();
      },
      error: (err: unknown) => {
        this.voidError.set(toApiError(err));
        this.saving.set(false);
      },
    });
  }

  private run(request: ReturnType<CashDiscrepanciesApi['get']>): void {
    this.saving.set(true);
    this.actionError.set(null);
    request.subscribe({
      next: (d) => {
        this.discrepancy.set(d);
        this.action.set(null);
        this.saving.set(false);
        this.alerts.refresh();
      },
      error: (err: unknown) => {
        this.actionError.set(toApiError(err));
        this.saving.set(false);
      },
    });
  }
}
