import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { EnvelopeOperationsApi } from '../../../core/api/envelopes-api';
import {
  EnvelopeOperationSummaryResponse,
  EnvelopeOperationType,
  PagedResponse,
} from '../../../core/api/models';
import { formatShortDate } from '../../../shared/format/date';
import { formatMoney } from '../../../shared/format/money';
import { envelopeOperationKindLabel, envelopeOperationStateLabel } from '../envelope-format';

const PAGE_SIZE = 25;

const KINDS: EnvelopeOperationType[] = ['Withdrawal', 'Transfer', 'TaxPayment', 'Opening'];

/** Every opening, move, withdrawal and tax payment, newest first. Voided ones only when asked. */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './envelope-operations-page.html',
  styleUrls: ['../../../shared/ui/list-page.css'],
})
export default class EnvelopeOperationsPage implements OnInit {
  private readonly api = inject(EnvelopeOperationsApi);

  protected readonly kinds = KINDS;
  protected readonly formatShortDate = formatShortDate;
  protected readonly formatMoney = formatMoney;
  protected readonly kindLabel = envelopeOperationKindLabel;
  protected readonly stateLabel = envelopeOperationStateLabel;

  protected readonly result = signal<PagedResponse<EnvelopeOperationSummaryResponse> | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly kind = signal<EnvelopeOperationType | ''>('');
  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly includeVoided = signal(false);
  protected readonly page = signal(1);

  protected readonly hasFilters = computed(() => !!(this.kind() || this.from() || this.to()));

  ngOnInit(): void {
    this.load();
  }

  protected setKind(value: string): void {
    this.kind.set(value as EnvelopeOperationType | '');
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

  protected toggleVoided(checked: boolean): void {
    this.includeVoided.set(checked);
    this.reload();
  }

  protected clearFilters(): void {
    this.kind.set('');
    this.from.set('');
    this.to.set('');
    this.reload();
  }

  protected goTo(page: number): void {
    this.page.set(page);
    this.load();
  }

  /** "Ganancia → Compensación del dueño", "Impuestos · ONAT", "5 sobres". */
  protected what(operation: EnvelopeOperationSummaryResponse): string {
    switch (operation.kind) {
      case 'Transfer':
        return `${operation.envelopeName} → ${operation.toEnvelopeName}`;
      case 'TaxPayment':
        return operation.concept ? `${operation.envelopeName} · ${operation.concept}` : (operation.envelopeName ?? '');
      case 'Opening':
        return operation.lineCount === 1 ? '1 monto' : `${operation.lineCount} montos`;
      default:
        return operation.envelopeName ?? '';
    }
  }

  private reload(): void {
    this.page.set(1);
    this.load();
  }

  private load(): void {
    this.api
      .getAll({
        kind: this.kind() || undefined,
        from: this.from() || undefined,
        to: this.to() || undefined,
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
