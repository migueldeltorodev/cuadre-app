import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { SettingsApi } from '../api/configuration-api';
import { BusinessSettingsResponse, UpdateBusinessSettingsRequest } from '../api/models';

/**
 * Single source of truth for the business settings on the client.
 * The side menu (business name) and the "Negocio" / "Precios y moneda" sections read the same signal,
 * so saving in one place updates every screen without reloading.
 */
@Injectable({ providedIn: 'root' })
export class BusinessSettingsStore {
  private readonly api = inject(SettingsApi);

  private readonly _settings = signal<BusinessSettingsResponse | null>(null);
  readonly settings = this._settings.asReadonly();

  private readonly _loadFailed = signal(false);
  readonly loadFailed = this._loadFailed.asReadonly();

  load(): void {
    this.api.get().subscribe({
      next: (settings) => {
        this._settings.set(settings);
        this._loadFailed.set(false);
      },
      error: () => this._loadFailed.set(true),
    });
  }

  /**
   * The API replaces the whole settings row, so each section sends only what it edits
   * and the rest comes from the last known state.
   */
  save(changes: Partial<UpdateBusinessSettingsRequest>): Observable<BusinessSettingsResponse> {
    const current = this._settings();
    if (current === null) {
      throw new Error('Settings must be loaded before saving');
    }

    const base: UpdateBusinessSettingsRequest = {
      name: current.name,
      address: current.address,
      closedDays: current.closedDays,
      cashDifferenceThresholdCents: current.cashDifferenceThresholdCents,
      exchangeBufferCents: current.exchangeBufferCents,
      defaultMarkupBps: current.defaultMarkupBps,
      priceRoundingStepCents: current.priceRoundingStepCents,
    };
    return this.api.update({ ...base, ...changes }).pipe(tap((saved) => this._settings.set(saved)));
  }
}
