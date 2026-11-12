import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AppInfoService } from '../../core/api/app-info.service';

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
  `,
})
export default class InicioPage {
  private readonly appInfo = inject(AppInfoService);

  protected readonly status = toSignal(
    this.appInfo.getInfo().pipe(
      map((info): ApiStatus => ({ state: 'ok', version: info.version, environment: info.environment })),
      catchError(() => of<ApiStatus>({ state: 'down' })),
    ),
    { initialValue: { state: 'loading' } as ApiStatus },
  );
}
