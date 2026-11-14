import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

/** "Configuración": title + section menu on the left, the routed section on the right (design: Config). */
@Component({
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header>
      <h1 class="page-title">Configuración</h1>
    </header>

    <div class="layout">
      <nav class="sections" aria-label="Secciones de configuración">
        @for (section of sections; track section.path) {
          <a class="sections__link" [routerLink]="section.path" routerLinkActive="sections__link--active"
            ariaCurrentWhenActive="page">{{ section.label }}</a>
        }
      </nav>

      <div class="content">
        <router-outlet />
      </div>
    </div>
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 28px; }
    .layout { display: flex; flex-wrap: wrap; gap: 28px; align-items: flex-start; }
    .sections { flex: 0 1 220px; display: flex; flex-direction: column; gap: 2px; }
    .sections__link {
      display: flex; align-items: center; min-height: 40px; padding: 0 12px;
      border: 1px solid transparent; border-radius: var(--r-sm);
      color: var(--c-ink-2); text-decoration: none; font-size: 14.5px;
    }
    .sections__link:hover { background: rgba(255, 255, 255, 0.6); }
    .sections__link--active {
      color: var(--c-ink); background: var(--c-surface); border-color: var(--c-line); font-weight: 500;
    }
    .content { flex: 999 1 520px; min-width: 0; }
  `,
})
export default class ConfiguracionPage {
  protected readonly sections = [
    { path: 'negocio', label: 'Negocio' },
    { path: 'cobros-y-cajas', label: 'Cobros y cajas' },
    { path: 'profit-first', label: 'Profit First' },
    { path: 'personal', label: 'Personal' },
    { path: 'gastos', label: 'Categorías de gasto' },
    { path: 'precios-y-moneda', label: 'Precios y moneda' },
    { path: 'impuestos', label: 'Impuestos' },
    { path: 'respaldo', label: 'Respaldo' },
  ];
}
