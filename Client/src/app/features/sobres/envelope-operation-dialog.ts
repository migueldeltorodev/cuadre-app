import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { ApiError, toApiError } from '../../core/api/api-error';
import { EnvelopeOperationsApi } from '../../core/api/envelopes-api';
import { EnvelopeOperationResponse, EnvelopeOverviewResponse } from '../../core/api/models';
import { amountValidator } from '../../shared/format/money-validators';
import { parseHundredths } from '../../shared/format/money';
import { formatBalance } from './envelope-format';

export type EnvelopeDialogKind = 'Withdrawal' | 'Transfer' | 'TaxPayment';

export interface EnvelopeOperationDialogData {
  kind: EnvelopeDialogKind;
  overview: EnvelopeOverviewResponse;
  /** Preselected envelope (the one money leaves from). */
  envelopeId?: string;
}

interface FormValue {
  date: string;
  cashBoxId: string;
  envelopeId: string;
  toEnvelopeId: string;
  amount: string;
  concept: string;
  receipt: string;
  notes: string;
}

const COPY: Record<EnvelopeDialogKind, { title: string; lead: string; confirm: string }> = {
  Withdrawal: {
    title: 'Sacar de un sobre',
    lead: 'Plata que sale del negocio: tu sueldo, la ganancia que te llevás. Sale de la caja elegida.',
    confirm: 'Sacar',
  },
  Transfer: {
    title: 'Mover entre sobres',
    lead: 'La plata no se mueve de lugar: cambia para qué es. Por eso es dentro de una misma caja.',
    confirm: 'Mover',
  },
  TaxPayment: {
    title: 'Pagar impuestos',
    lead: 'Sale siempre del sobre Impuestos.',
    confirm: 'Registrar el pago',
  },
};

/**
 * Withdrawal, move between envelopes or tax payment: one small form, saved = confirmed.
 * Leaving an envelope below zero is allowed (RF-PF-08); the page shows the server's warning afterwards.
 */
@Component({
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()" novalidate>
      <div>
        <h2 class="dialog__title">{{ copy.title }}</h2>
        <p class="card__lead">{{ copy.lead }}</p>
      </div>

      <div class="form__row">
        <label class="field">
          <span class="field__label">Caja</span>
          <select class="input" formControlName="cashBoxId">
            @for (box of boxes; track box.id) {
              <option [value]="box.id">{{ box.name }}</option>
            }
          </select>
        </label>
        <label class="field">
          <span class="field__label">Fecha</span>
          <input class="input num" type="date" formControlName="date" [class.input--invalid]="invalid('date')" />
        </label>
      </div>

      @if (kind !== 'TaxPayment') {
        <div class="form__row">
          <label class="field">
            <span class="field__label">{{ kind === 'Transfer' ? 'Desde el sobre' : 'Del sobre' }}</span>
            <select class="input" formControlName="envelopeId" cdkFocusInitial>
              @for (envelope of envelopes; track envelope.id) {
                <option [value]="envelope.id">{{ envelope.name }}</option>
              }
            </select>
          </label>
          @if (kind === 'Transfer') {
            <label class="field">
              <span class="field__label">Al sobre</span>
              <select class="input" formControlName="toEnvelopeId" [class.input--invalid]="sameEnvelope()">
                @for (envelope of envelopes; track envelope.id) {
                  <option [value]="envelope.id">{{ envelope.name }}</option>
                }
              </select>
              @if (sameEnvelope()) {
                <span class="field__error">Elegí otro sobre.</span>
              }
            </label>
          }
        </div>
      }

      <p class="available" [class.available--low]="leftAfter() !== null && leftAfter()! < 0">
        Hay en {{ sourceName() }} · {{ boxName() }}: <strong class="num">{{ formatBalance(available(), currency()) }}</strong>
        @if (leftAfter() !== null && leftAfter()! < 0) {
          <span>· quedaría en {{ formatBalance(leftAfter()!, currency()) }}</span>
        }
      </p>

      <label class="field">
        <span class="field__label">Monto</span>
        <span class="input input--affix" [class.input--invalid]="invalid('amount')">
          <input formControlName="amount" inputmode="decimal" autocomplete="off" class="num"
            [attr.cdkFocusInitial]="kind === 'TaxPayment' ? '' : null" />
          <span>{{ currency() }}</span>
        </span>
        @if (invalid('amount')) {
          <span class="field__error">Escribí un monto mayor que cero.</span>
        }
      </label>

      @if (kind === 'TaxPayment') {
        <div class="form__row">
          <label class="field">
            <span class="field__label">Qué se pagó</span>
            <input class="input" formControlName="concept" autocomplete="off" placeholder="ONAT, noviembre"
              [class.input--invalid]="invalid('concept')" />
            @if (invalid('concept')) {
              <span class="field__error">Escribí el concepto.</span>
            }
          </label>
          <label class="field">
            <span class="field__label">Comprobante <span class="opt">(opcional)</span></span>
            <input class="input" formControlName="receipt" autocomplete="off" />
          </label>
        </div>
      }

      <label class="field">
        <span class="field__label">Nota <span class="opt">(opcional)</span></span>
        <input class="input" formControlName="notes" autocomplete="off" />
      </label>

      @if (error(); as e) {
        <div class="alert alert--bad" role="alert">
          {{ e.message }}
          @for (message of fieldMessages(e); track message) {
            <div>· {{ message }}</div>
          }
        </div>
      }

      <div class="form__actions">
        <button class="btn" type="button" (click)="ref.close()">Cancelar</button>
        <button class="btn btn--primary" type="submit" [disabled]="saving()">
          {{ saving() ? 'Guardando…' : copy.confirm }}
        </button>
      </div>
    </form>
  `,
  styles: `
    .available { margin: 0; font-size: 14px; color: var(--c-ink-2); }
    .available--low { color: var(--c-bad-ink); }
    .opt { color: var(--c-muted); font-weight: 400; }
  `,
})
export class EnvelopeOperationDialog {
  protected readonly data = inject<EnvelopeOperationDialogData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<EnvelopeOperationResponse>>(DialogRef);
  private readonly api = inject(EnvelopeOperationsApi);

  protected readonly kind = this.data.kind;
  protected readonly copy = COPY[this.kind];
  protected readonly formatBalance = formatBalance;

  protected readonly boxes = this.data.overview.cashBoxes.filter((box) => box.isActive);
  protected readonly envelopes = this.data.overview.envelopes
    .map((row) => row.envelope)
    .filter((envelope) => envelope.isActive);

  private readonly taxEnvelope = this.envelopes.find((envelope) => envelope.role === 'Tax');

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  private readonly submitted = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    date: [this.data.overview.date, Validators.required],
    cashBoxId: [this.defaultBoxId()],
    envelopeId: [this.defaultEnvelopeId()],
    toEnvelopeId: [this.defaultToEnvelopeId()],
    amount: ['', [Validators.required, amountValidator({ min: 1 })]],
    concept: ['', this.kind === 'TaxPayment' ? [Validators.required, Validators.maxLength(120)] : []],
    receipt: ['', Validators.maxLength(60)],
    notes: ['', Validators.maxLength(500)],
  });

  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  private readonly sourceId = computed(() =>
    this.kind === 'TaxPayment' ? (this.taxEnvelope?.id ?? '') : (this.value().envelopeId ?? ''),
  );

  protected readonly sourceName = computed(
    () => this.envelopes.find((envelope) => envelope.id === this.sourceId())?.name ?? '',
  );

  private readonly box = computed(() => this.boxes.find((box) => box.id === this.value().cashBoxId));
  protected readonly boxName = computed(() => this.box()?.name ?? '');
  protected readonly currency = computed(() => this.box()?.currency ?? 'CUP');

  /** Today's balance of the source envelope in the chosen box (display only: the server decides). */
  protected readonly available = computed(() => {
    const row = this.data.overview.envelopes.find((r) => r.envelope.id === this.sourceId());
    return row?.balances.find((b) => b.cashBoxId === this.value().cashBoxId)?.amountCents ?? 0;
  });

  protected readonly leftAfter = computed(() => {
    const amount = parseHundredths(this.value().amount);
    return amount === null ? null : this.available() - amount;
  });

  protected readonly sameEnvelope = computed(
    () => this.kind === 'Transfer' && this.value().envelopeId === this.value().toEnvelopeId,
  );

  protected invalid(name: 'date' | 'amount' | 'concept'): boolean {
    const control = this.form.controls[name];
    return control.invalid && (control.touched || this.submitted());
  }

  protected fieldMessages(error: ApiError): string[] {
    return Object.values(error.fields);
  }

  protected save(): void {
    this.submitted.set(true);
    if (this.form.invalid || this.sameEnvelope()) {
      return;
    }

    const request$ = this.request(this.form.getRawValue());
    this.saving.set(true);
    this.error.set(null);
    request$.subscribe({
      next: (saved) => this.ref.close(saved),
      error: (err: unknown) => {
        this.error.set(toApiError(err));
        this.saving.set(false);
      },
    });
  }

  private request(value: FormValue): Observable<EnvelopeOperationResponse> {
    const amountCents = parseHundredths(value.amount)!;
    const notes = value.notes.trim() || null;
    switch (this.kind) {
      case 'Withdrawal':
        return this.api.createWithdrawal({
          date: value.date,
          cashBoxId: value.cashBoxId,
          envelopeId: value.envelopeId,
          amountCents,
          notes,
        });
      case 'Transfer':
        return this.api.createTransfer({
          date: value.date,
          cashBoxId: value.cashBoxId,
          fromEnvelopeId: value.envelopeId,
          toEnvelopeId: value.toEnvelopeId,
          amountCents,
          notes,
        });
      case 'TaxPayment':
        return this.api.createTaxPayment({
          date: value.date,
          cashBoxId: value.cashBoxId,
          amountCents,
          concept: value.concept.trim(),
          receipt: value.receipt.trim() || null,
          notes,
        });
    }
  }

  /** Cash in CUP first: it is where most envelope money lives (D-11). Taxes may be paid by transfer. */
  private defaultBoxId(): string {
    const cashCup = this.boxes.find((box) => box.kind === 'Cash' && box.currency === 'CUP');
    return (cashCup ?? this.boxes[0])?.id ?? '';
  }

  private defaultEnvelopeId(): string {
    if (this.data.envelopeId) {
      return this.data.envelopeId;
    }
    const preferred = this.kind === 'Withdrawal' ? 'OwnerPay' : this.kind === 'Transfer' ? 'Profit' : 'Tax';
    return (this.envelopes.find((envelope) => envelope.role === preferred) ?? this.envelopes[0])?.id ?? '';
  }

  private defaultToEnvelopeId(): string {
    const from = this.defaultEnvelopeId();
    return this.envelopes.find((envelope) => envelope.id !== from)?.id ?? '';
  }
}
