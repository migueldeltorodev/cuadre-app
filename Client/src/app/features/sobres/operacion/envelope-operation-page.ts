import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { EnvelopeOperationsApi } from '../../../core/api/envelopes-api';
import { EnvelopeOperationResponse } from '../../../core/api/models';
import { formatShortDate, formatTimestampDay } from '../../../shared/format/date';
import { formatMoney } from '../../../shared/format/money';
import {
  envelopeOperationKindLabel,
  envelopeOperationStateLabel,
  formatBalance,
  formatSignedMoney,
} from '../envelope-format';

/**
 * One opening, move, withdrawal or tax payment. Never edited. Voiding asks for a reason and writes the
 * reverse cash movements dated today; the original stays, marked as voided.
 */
@Component({
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './envelope-operation-page.html',
  styleUrls: ['../../../shared/ui/list-page.css', './envelope-operation-page.css'],
})
export default class EnvelopeOperationPage implements OnInit {
  /** Route param (withComponentInputBinding). */
  readonly id = input.required<string>();

  private readonly api = inject(EnvelopeOperationsApi);

  protected readonly formatShortDate = formatShortDate;
  protected readonly formatTimestampDay = formatTimestampDay;
  protected readonly formatMoney = formatMoney;
  protected readonly formatBalance = formatBalance;
  protected readonly formatSignedMoney = formatSignedMoney;
  protected readonly kindLabel = envelopeOperationKindLabel;

  protected readonly operation = signal<EnvelopeOperationResponse | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);

  protected readonly voiding = signal(false);
  protected readonly savingVoid = signal(false);
  protected readonly voidError = signal<ApiError | null>(null);
  protected readonly reason = inject(NonNullableFormBuilder).control('', [
    Validators.required,
    Validators.maxLength(300),
  ]);

  protected readonly state = computed(() => {
    const operation = this.operation();
    return operation ? envelopeOperationStateLabel(operation.status) : null;
  });

  ngOnInit(): void {
    this.api.get(this.id()).subscribe({
      next: (operation) => this.operation.set(operation),
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
    const operation = this.operation();
    this.reason.markAsTouched();
    if (!operation || this.reason.invalid || !this.reason.value.trim()) {
      return;
    }
    this.savingVoid.set(true);
    this.voidError.set(null);
    this.api.void(operation.id, this.reason.value.trim()).subscribe({
      next: (voided) => {
        this.operation.set(voided);
        this.voiding.set(false);
        this.savingVoid.set(false);
      },
      error: (err: unknown) => {
        this.voidError.set(toApiError(err));
        this.savingVoid.set(false);
      },
    });
  }
}
