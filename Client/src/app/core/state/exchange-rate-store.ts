import { Injectable, computed, inject, signal } from '@angular/core';
import { ExchangeRatesApi } from '../api/configuration-api';
import { CurrentExchangeRateResponse } from '../api/models';

/**
 * "Today's rate" shared by the side menu and the "Precios y moneda" section.
 * "Today" comes from the SERVER (the PC clock), never from the browser, so both always agree.
 */
@Injectable({ providedIn: 'root' })
export class ExchangeRateStore {
  private readonly api = inject(ExchangeRatesApi);

  private readonly _current = signal<CurrentExchangeRateResponse | null>(null);
  readonly current = this._current.asReadonly();

  private readonly _loadFailed = signal(false);
  readonly loadFailed = this._loadFailed.asReadonly();

  /** True once loaded and no rate was typed for today (RF-MON-03). */
  readonly isMissingToday = computed(() => this._current()?.isMissing ?? false);

  load(): void {
    this.api.getCurrent().subscribe({
      next: (current) => {
        this._current.set(current);
        this._loadFailed.set(false);
      },
      error: () => this._loadFailed.set(true),
    });
  }
}
