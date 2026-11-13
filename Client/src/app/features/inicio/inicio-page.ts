import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AppInfoService } from '../../core/api/app-info.service';
import { ClosingAlertsStore } from '../../core/state/closing-alerts-store';
import { formatShortDate } from '../../shared/format/date';
import { formatMoney } from '../../shared/format/money';

type ApiStatus =
  | { state: 'loading' }
  | { state: 'ok'; version: string; environment: string }
  | { state: 'down' };

/**
 * Skeleton of the "Inicio" screen. For now it only proves the full chain
 * Angular → /api → ASP.NET Core → SQLite is alive. The real dashboard arrives in slice 8.
 */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="head">
      <div>
        <h1 class="page-title">Buenas noches</h1>
        <p class="page-subtitle">Cuadre está en construcción. Esta es la beta del esqueleto.</p>
      </div>
      <a class="btn btn--primary" routerLink="/cierre">Hacer el cierre de hoy</a>
    </header>

    @let days = alerts.pendingDays();
    @let shortages = alerts.pendingShortages();
    @if ((days?.count ?? 0) > 0 || (shortages?.count ?? 0) > 0) {
      <section class="todo" aria-label="Pendientes del cierre">
        @if (days && days.count > 0) {
          <a class="todo__item" [routerLink]="['/cierre']" [queryParams]="{ fecha: days.days[0] }">
            <span class="todo__count num">{{ days.count }}</span>
            <span>
              <strong>{{ days.count === 1 ? 'Día sin cerrar' : 'Días sin cerrar' }}</strong>
              <span class="todo__sub">
                Desde el {{ formatShortDate(days.days[0]) }}. Cerralos en orden: empezá por ese.
              </span>
            </span>
          </a>
        }
        @if (shortages && shortages.count > 0) {
          <a class="todo__item" routerLink="/cierre/faltantes">
            <span class="todo__count num">{{ shortages.count }}</span>
            <span>
              <strong>{{ shortages.count === 1 ? 'Faltante por reclamar' : 'Faltantes por reclamar' }}</strong>
              <span class="todo__sub">Suman {{ formatMoney(shortages.pendingCents) }}.</span>
            </span>
          </a>
        }
      </section>
    }

    <section class="card status" aria-live="polite">
      <h2 class="status__title">Conexión con el servidor</h2>
      @let s = status();
      @if (s.state === 'ok') {
        <span class="pill pill--ok">Conectado · v{{ s.version }} · {{ s.environment }}</span>
      } @else if (s.state === 'down') {
        <span class="pill pill--bad">Sin conexión con la API</span>
      } @else {
        <span class="pill">Conectando…</span>
      }
    </section>
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 32px; }
    .head { display: flex; flex-wrap: wrap; gap: 20px; align-items: flex-end; justify-content: space-between; }
    .status { display: flex; flex-wrap: wrap; gap: 16px; align-items: center; justify-content: space-between; }
    .status__title { margin: 0; font-size: 15px; font-weight: 600; }
    .todo { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 12px; }
    .todo__item {
      display: flex; align-items: flex-start; gap: 14px; padding: 18px 20px;
      border: 1px solid #efd9a8; border-radius: var(--r-lg); background: var(--c-warn-soft);
      color: var(--c-ink); text-decoration: none; transition: border-color 0.15s;
    }
    .todo__item:hover { border-color: var(--c-warn-ink); }
    .todo__item:focus-visible { outline: 2px solid var(--c-accent); outline-offset: 2px; }
    .todo__count {
      flex: none; display: grid; place-items: center; min-width: 36px; height: 36px; padding: 0 8px;
      border-radius: var(--r-pill); background: var(--c-surface); color: var(--c-warn-ink); font-weight: 600;
    }
    .todo__sub { display: block; margin-top: 3px; font-size: 13.5px; color: var(--c-ink-2); }
  `,
})
export default class InicioPage {
  private readonly appInfo = inject(AppInfoService);
  protected readonly alerts = inject(ClosingAlertsStore);
  protected readonly formatShortDate = formatShortDate;
  protected readonly formatMoney = formatMoney;

  protected readonly status = toSignal(
    this.appInfo.getInfo().pipe(
      map((info): ApiStatus => ({ state: 'ok', version: info.version, environment: info.environment })),
      catchError(() => of<ApiStatus>({ state: 'down' })),
    ),
    { initialValue: { state: 'loading' } as ApiStatus },
  );
}
