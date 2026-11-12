import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { CashBoxesApi, PaymentMethodsApi } from '../../../core/api/configuration-api';
import { CashBoxResponse, PaymentMethodResponse } from '../../../core/api/models';
import { DIALOG_DEFAULTS } from '../../../shared/ui/dialog-config';
import { CashBoxDialog, CashBoxDialogData } from './cash-box-dialog';
import { CASH_BOX_TYPE_LABELS } from './labels';
import { PaymentMethodDialog, PaymentMethodDialogData } from './payment-method-dialog';

/** Cash boxes (RF-CAJ-01) and payment methods (RF-CFG-03). Nothing is deleted: only deactivated. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cobros-y-cajas-section.html',
  styles: `
    :host { display: flex; flex-direction: column; gap: 20px; }
    .card { display: flex; flex-direction: column; gap: 8px; }
  `,
})
export default class CobrosYCajasSection implements OnInit {
  private readonly dialog = inject(Dialog);
  private readonly cashBoxesApi = inject(CashBoxesApi);
  private readonly paymentMethodsApi = inject(PaymentMethodsApi);

  protected readonly typeLabels = CASH_BOX_TYPE_LABELS;
  protected readonly cashBoxes = signal<CashBoxResponse[] | null>(null);
  protected readonly paymentMethods = signal<PaymentMethodResponse[]>([]);
  protected readonly loadError = signal<ApiError | null>(null);

  private readonly boxNames = computed(
    () => new Map((this.cashBoxes() ?? []).map((box) => [box.id, box.name] as const)),
  );

  ngOnInit(): void {
    this.reload();
  }

  protected boxName(id: string): string {
    return this.boxNames().get(id) ?? '—';
  }

  /** How many ACTIVE payment methods land in a box (an active box with 0 is fine; info only). */
  protected methodsInto(boxId: string): number {
    return this.paymentMethods().filter((m) => m.isActive && m.cashBoxId === boxId).length;
  }

  protected openCashBox(cashBox?: CashBoxResponse): void {
    this.dialog
      .open<CashBoxResponse, CashBoxDialogData>(CashBoxDialog, { ...DIALOG_DEFAULTS, data: { cashBox } })
      .closed.subscribe((saved) => saved && this.reload());
  }

  protected openPaymentMethod(paymentMethod?: PaymentMethodResponse): void {
    this.dialog
      .open<PaymentMethodResponse, PaymentMethodDialogData>(PaymentMethodDialog, {
        ...DIALOG_DEFAULTS,
        data: { paymentMethod, cashBoxes: this.cashBoxes() ?? [] },
      })
      .closed.subscribe((saved) => saved && this.reload());
  }

  private reload(): void {
    forkJoin({ boxes: this.cashBoxesApi.getAll(), methods: this.paymentMethodsApi.getAll() }).subscribe({
      next: ({ boxes, methods }) => {
        this.cashBoxes.set(boxes.items);
        this.paymentMethods.set(methods.items);
        this.loadError.set(null);
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }
}
