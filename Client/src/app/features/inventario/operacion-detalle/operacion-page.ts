import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { StockOperationsApi } from '../../../core/api/inventory-api';
import { StockOperationLineResponse, StockOperationResponse } from '../../../core/api/models';
import { formatShortDate, formatTimestampDay } from '../../../shared/format/date';
import { formatMicros, formatMoney } from '../../../shared/format/money';
import { formatQuantityWithUnit, perUnit } from '../../../shared/format/quantity';
import { exitReasonLabel, formatSignedQuantity, operationKindLabel, operationStateLabel } from '../inventory-format';

/**
 * One transfer, exit or count. Never edited. Voiding asks for a reason and writes the reverse movements
 * (dated today); the cost of its products is replayed as if it never happened.
 */
@Component({
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './operacion-page.html',
  styleUrls: ['../../../shared/ui/list-page.css', './operacion-page.css'],
})
export default class OperacionPage implements OnInit {
  /** Route param (withComponentInputBinding). */
  readonly id = input.required<string>();

  private readonly api = inject(StockOperationsApi);

  protected readonly formatShortDate = formatShortDate;
  protected readonly formatTimestampDay = formatTimestampDay;
  protected readonly formatMicros = formatMicros;
  protected readonly formatMoney = formatMoney;
  protected readonly formatQuantityWithUnit = formatQuantityWithUnit;
  protected readonly formatSignedQuantity = formatSignedQuantity;
  protected readonly perUnit = perUnit;
  protected readonly exitReasonLabel = exitReasonLabel;
  protected readonly operationKindLabel = operationKindLabel;

  protected readonly operation = signal<StockOperationResponse | null>(null);
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
    return operation ? operationStateLabel(operation.status) : null;
  });

  protected readonly backLink = computed(() => {
    switch (this.operation()?.kind) {
      case 'Exit':
        return { link: '/inventario/salidas', label: 'Salidas' };
      case 'Count':
        return { link: '/inventario/conteos', label: 'Conteos' };
      default:
        return { link: '/inventario/traslados', label: 'Traslados' };
    }
  });

  /** Exit: value of what left. Count: shortage minus surplus (positive = loss). */
  protected readonly valueCents = computed(() => {
    const operation = this.operation();
    if (!operation || operation.kind === 'Transfer') {
      return null;
    }
    const sum = operation.lines.reduce((total, line) => total + (line.valueCupCents ?? 0), 0);
    return operation.kind === 'Count' ? -sum : sum;
  });

  ngOnInit(): void {
    this.api.get(this.id()).subscribe({
      next: (operation) => this.operation.set(operation),
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }

  /** "2 sacos" or "50 lb": as it was typed. */
  protected entered(line: StockOperationLineResponse): string {
    return formatQuantityWithUnit(
      line.enteredQuantityMilli,
      line.enteredInPurchaseUnits ? line.purchaseUnit : line.stockUnit,
    );
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
