import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { CashBoxesApi } from '../../../core/api/configuration-api';
import { CashBoxResponse, LocationResponse, PurchaseResponse } from '../../../core/api/models';
import { LocationsApi, PurchasesApi } from '../../../core/api/purchases-api';
import { formatShortDate, formatTimestampDay } from '../../../shared/format/date';
import { formatHundredths, formatMicros, formatMoney } from '../../../shared/format/money';
import { formatQuantityWithUnit, perUnit } from '../../../shared/format/quantity';
import { purchaseStateLabel } from '../purchase-format';
import { Breadcrumbs } from '../../../core/layout/breadcrumbs';

/**
 * One purchase (RF-CMP-06/07): what came in, at what LANDED cost per stock unit, and how it was paid.
 * Never edited. Voiding asks for a reason and reverses stock and cash (dated today); the costs are replayed.
 */
@Component({
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './compra-page.html',
  styleUrl: './compra-page.css',
})
export default class CompraPage implements OnInit {
  /** Route param (withComponentInputBinding). */
  readonly id = input.required<string>();

  private readonly purchasesApi = inject(PurchasesApi);
  private readonly cashBoxesApi = inject(CashBoxesApi);
  private readonly locationsApi = inject(LocationsApi);

  protected readonly formatShortDate = formatShortDate;
  protected readonly formatTimestampDay = formatTimestampDay;
  protected readonly formatHundredths = formatHundredths;
  protected readonly formatMicros = formatMicros;
  protected readonly formatMoney = formatMoney;
  protected readonly formatQuantityWithUnit = formatQuantityWithUnit;
  protected readonly perUnit = perUnit;

  protected readonly purchase = signal<PurchaseResponse | null>(null);

  /** The trail names this page once it is loaded (the shell draws it). */
  private readonly crumbs = inject(Breadcrumbs);
  protected readonly nameInTrail = effect(() => {
    const purchase = this.purchase();
    if (purchase) {
      this.crumbs.set(`Compra #${purchase.number}`);
    }
  });
  private readonly cashBoxes = signal<CashBoxResponse[]>([]);
  private readonly locations = signal<LocationResponse[]>([]);
  protected readonly loadError = signal<ApiError | null>(null);

  protected readonly voiding = signal(false);
  protected readonly savingVoid = signal(false);
  protected readonly voidError = signal<ApiError | null>(null);
  protected readonly reason = inject(NonNullableFormBuilder).control('', [
    Validators.required,
    Validators.maxLength(300),
  ]);

  protected readonly state = computed(() => {
    const purchase = this.purchase();
    return purchase ? purchaseStateLabel(purchase.status) : null;
  });

  protected readonly locationName = computed(() => {
    const purchase = this.purchase();
    return this.locations().find((l) => l.id === purchase?.locationId)?.name ?? '—';
  });

  protected readonly chargesTotalUsdMicros = computed(
    () => this.purchase()?.lines.reduce((sum, line) => sum + line.allocatedChargesUsdMicros, 0) ?? 0,
  );

  ngOnInit(): void {
    forkJoin({
      purchase: this.purchasesApi.get(this.id()),
      cashBoxes: this.cashBoxesApi.getAll(),
      locations: this.locationsApi.getAll(),
    }).subscribe({
      next: ({ purchase, cashBoxes, locations }) => {
        this.purchase.set(purchase);
        this.cashBoxes.set(cashBoxes.items);
        this.locations.set(locations.items);
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }

  protected boxName(id: string): string {
    return this.cashBoxes().find((box) => box.id === id)?.name ?? '—';
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
    const purchase = this.purchase();
    this.reason.markAsTouched();
    if (!purchase || this.reason.invalid || !this.reason.value.trim()) {
      return;
    }
    this.savingVoid.set(true);
    this.voidError.set(null);
    this.purchasesApi.void(purchase.id, this.reason.value.trim()).subscribe({
      next: (voided) => {
        this.purchase.set(voided);
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
