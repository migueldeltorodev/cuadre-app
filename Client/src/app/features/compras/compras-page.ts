import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../core/api/api-error';
import { SuppliersApi } from '../../core/api/catalog-api';
import { PagedResponse, PurchaseSummaryResponse, SupplierResponse } from '../../core/api/models';
import { PurchasesApi } from '../../core/api/purchases-api';
import { formatShortDate } from '../../shared/format/date';
import { formatMoney } from '../../shared/format/money';
import { purchaseStateLabel } from './purchase-format';

const PAGE_SIZE = 25;

/** Purchase history (RF-CMP-06): newest first, filtered on the server. Voided ones only when asked. */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './compras-page.html',
  styleUrl: './compras-page.css',
})
export default class ComprasPage implements OnInit {
  private readonly purchasesApi = inject(PurchasesApi);
  private readonly suppliersApi = inject(SuppliersApi);

  protected readonly formatShortDate = formatShortDate;
  protected readonly formatMoney = formatMoney;
  protected readonly purchaseStateLabel = purchaseStateLabel;

  protected readonly suppliers = signal<SupplierResponse[]>([]);
  protected readonly result = signal<PagedResponse<PurchaseSummaryResponse> | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);

  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly supplierId = signal('');
  protected readonly includeVoided = signal(false);
  protected readonly page = signal(1);

  protected readonly hasFilters = computed(() => !!(this.from() || this.to() || this.supplierId()));

  ngOnInit(): void {
    this.suppliersApi.getAll().subscribe({
      next: (response) => this.suppliers.set(response.items),
      error: () => this.suppliers.set([]),
    });
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

  protected setSupplier(value: string): void {
    this.supplierId.set(value);
    this.reload();
  }

  protected toggleVoided(checked: boolean): void {
    this.includeVoided.set(checked);
    this.reload();
  }

  protected clearFilters(): void {
    this.from.set('');
    this.to.set('');
    this.supplierId.set('');
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
    this.purchasesApi
      .getAll({
        from: this.from() || undefined,
        to: this.to() || undefined,
        supplierId: this.supplierId() || undefined,
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
