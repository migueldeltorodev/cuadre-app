import { Injectable, computed, inject, signal } from '@angular/core';
import { CashDiscrepanciesApi, DailyClosingsApi } from '../api/closings-api';
import { PendingClosingDaysResponse, PendingShortagesResponse } from '../api/models';

/**
 * What the closing still asks for: business days not closed (RF-CIE-09) and shortages to claim (RF-CIE-13).
 * Shared by the side menu (badges), Inicio and the closing wizard. Refreshed by the menu after every navigation,
 * so an action on one page shows up everywhere on the next one.
 */
@Injectable({ providedIn: 'root' })
export class ClosingAlertsStore {
  private readonly closingsApi = inject(DailyClosingsApi);
  private readonly discrepanciesApi = inject(CashDiscrepanciesApi);

  private readonly _pendingDays = signal<PendingClosingDaysResponse | null>(null);
  readonly pendingDays = this._pendingDays.asReadonly();

  private readonly _pendingShortages = signal<PendingShortagesResponse | null>(null);
  readonly pendingShortages = this._pendingShortages.asReadonly();

  readonly pendingDaysCount = computed(() => this._pendingDays()?.count ?? 0);
  readonly pendingShortagesCount = computed(() => this._pendingShortages()?.count ?? 0);

  refresh(): void {
    // Failures are silent: these are hints, the pages show the real errors.
    this.closingsApi.pendingDays().subscribe({ next: (days) => this._pendingDays.set(days), error: () => {} });
    this.discrepanciesApi
      .pendingSummary()
      .subscribe({ next: (summary) => this._pendingShortages.set(summary), error: () => {} });
  }
}
