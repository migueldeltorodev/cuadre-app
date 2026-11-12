import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

/**
 * "Inventario" (design: Inventario): title, the two everyday actions, and the tabs.
 * Every number shown below comes from the stock ledger: nothing here is typed by hand (RF-INV-02).
 */
@Component({
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="head">
      <div>
        <h1 class="page-title">Inventario</h1>
        <p class="page-subtitle">El stock sale de los movimientos: compras, traslados, salidas y conteos.</p>
      </div>
      <div class="head__actions">
        <a class="btn" routerLink="salida">Registrar salida</a>
        <a class="btn btn--primary" routerLink="traslado">Llevar al mostrador</a>
      </div>
    </header>

    <nav class="tabs" aria-label="Secciones del inventario">
      @for (tab of tabs; track tab.path) {
        <a class="tabs__link" [routerLink]="tab.path" routerLinkActive="tabs__link--active"
          ariaCurrentWhenActive="page">{{ tab.label }}</a>
      }
    </nav>

    <router-outlet />
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 20px; }
    .head { display: flex; flex-wrap: wrap; gap: 16px; justify-content: space-between; align-items: flex-end; }
    .head__actions { display: flex; flex-wrap: wrap; gap: 10px; }
    .tabs {
      display: flex; flex-wrap: wrap; gap: 4px;
      border-bottom: 1px solid var(--c-line);
    }
    .tabs__link {
      display: flex; align-items: center; min-height: 40px; padding: 0 14px;
      margin-bottom: -1px; border-bottom: 2px solid transparent;
      color: var(--c-ink-2); text-decoration: none; font-size: 14.5px;
    }
    .tabs__link:hover { color: var(--c-ink); }
    .tabs__link--active { color: var(--c-ink); border-bottom-color: var(--c-accent); font-weight: 500; }
  `,
})
export default class InventarioPage {
  protected readonly tabs = [
    { path: 'stock', label: 'Stock' },
    { path: 'traslados', label: 'Traslados' },
    { path: 'salidas', label: 'Salidas' },
    { path: 'conteos', label: 'Conteos' },
    { path: 'historial', label: 'Historial por producto' },
    { path: 'lista-de-compras', label: 'Lista de compras' },
    { path: 'ubicaciones', label: 'Ubicaciones' },
  ];
}
