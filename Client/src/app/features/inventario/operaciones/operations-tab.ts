import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { StockOperationsApi } from '../../../core/api/inventory-api';
import {
  PagedResponse,
  StockOperationSummaryResponse,
  StockOperationType,
} from '../../../core/api/models';
import { formatShortDate } from '../../../shared/format/date';
import { formatMoney } from '../../../shared/format/money';
import { exitReasonLabel, operationStateLabel } from '../inventory-format';

const PAGE_SIZE = 25;

interface KindCopy {
  title: string;
  emptyTitle: string;
  emptyLead: string;
  action: string;
  actionLink: string;
}

const COPY: Record<StockOperationType, KindCopy> = {
  Transfer: {
    title: 'Traslados',
    emptyTitle: 'Todavía no hay traslados',
    emptyLead: 'Cuando abrís un saco en el mostrador, registralo acá: baja en el almacén y sube en el mostrador.',
    action: 'Llevar al mostrador',
    actionLink: '../traslado',
  },
  Exit: {
    title: 'Salidas',
    emptyTitle: 'Todavía no hay salidas',
    emptyLead: 'Lo que sale sin venderse: merma, vencido, roto, consumo de la casa o un ajuste.',
    action: 'Registrar salida',
    actionLink: '../salida',
  },
  Count: {
    title: 'Conteos',
    emptyTitle: 'Todavía no hay conteos',
    emptyLead: 'Contá lo que hay de verdad y la app ajusta la diferencia. Un faltante queda como pérdida.',
    action: 'Nuevo conteo',
    actionLink: '../conteo',
  },
};

/** History of one kind of stock operation (route data "kind"), newest first. Voided ones only when asked. */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './operations-tab.html',
  styleUrls: ['../../../shared/ui/list-page.css'],
})
export default class OperationsTab implements OnInit {
  /** Route data (withComponentInputBinding). */
  readonly kind = input.required<StockOperationType>();

  private readonly api = inject(StockOperationsApi);

  protected readonly formatShortDate = formatShortDate;
  protected readonly formatMoney = formatMoney;
  protected readonly exitReasonLabel = exitReasonLabel;
  protected readonly operationStateLabel = operationStateLabel;

  protected readonly copy = computed(() => COPY[this.kind()]);

  protected readonly result = signal<PagedResponse<StockOperationSummaryResponse> | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly includeVoided = signal(false);
  protected readonly page = signal(1);

  protected readonly hasFilters = computed(() => !!(this.from() || this.to()));

  ngOnInit(): void {
    this.load();
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
    this.api
      .getAll({
        kind: this.kind(),
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
