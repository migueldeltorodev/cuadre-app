import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { filter } from 'rxjs';
import { ExchangeRateDialog, ExchangeRateDialogData } from '../exchange-rate/exchange-rate-dialog';
import { BusinessSettingsStore } from '../state/business-settings-store';
import { ClosingAlertsStore } from '../state/closing-alerts-store';
import { ExchangeRateStore } from '../state/exchange-rate-store';
import { formatHundredths } from '../../shared/format/money';
import { DIALOG_DEFAULTS } from '../../shared/ui/dialog-config';

type BadgeKey = 'pendingDays' | 'pendingShortages' | 'dueExpenses';

/** A page inside a section of the menu. */
interface NavLink {
  path: string;
  label: string;
  /** Only active on exactly this URL (the index page of the section). */
  exact?: boolean;
  badge?: BadgeKey;
}

/** A section of the menu. With children it unfolds; its own link goes to the main page of the section. */
interface NavItem {
  id: string;
  path: string;
  label: string;
  icon: string;
  children?: NavLink[];
}

interface NavGroup {
  title?: string;
  items: NavItem[];
}

const NAV: NavGroup[] = [
  {
    items: [
      { id: 'inicio', path: '/', label: 'Inicio', icon: 'home' },
      {
        id: 'cierre',
        path: '/cierre',
        label: 'Cierre del día',
        icon: 'close',
        children: [
          { path: '/cierre', label: 'Cerrar un día', exact: true, badge: 'pendingDays' },
          { path: '/cierre/historial', label: 'Cierres anteriores' },
          { path: '/cierre/faltantes', label: 'Faltantes y sobrantes', badge: 'pendingShortages' },
        ],
      },
    ],
  },
  {
    title: 'Mercadería',
    items: [
      {
        id: 'productos',
        path: '/productos',
        label: 'Productos',
        icon: 'box',
        children: [
          { path: '/productos', label: 'Catálogo', exact: true },
          { path: '/productos/lista-de-precios', label: 'Lista de precios' },
          { path: '/productos/categorias', label: 'Categorías' },
          { path: '/productos/proveedores', label: 'Proveedores' },
        ],
      },
      { id: 'compras', path: '/compras', label: 'Compras', icon: 'cart' },
      {
        id: 'inventario',
        path: '/inventario',
        label: 'Inventario',
        icon: 'shelves',
        children: [
          { path: '/inventario/stock', label: 'Stock' },
          { path: '/inventario/traslados', label: 'Traslados' },
          { path: '/inventario/salidas', label: 'Salidas' },
          { path: '/inventario/conteos', label: 'Conteos' },
          { path: '/inventario/historial', label: 'Historial por producto' },
          { path: '/inventario/lista-de-compras', label: 'Lista de compras' },
          { path: '/inventario/ubicaciones', label: 'Ubicaciones' },
        ],
      },
    ],
  },
  {
    title: 'Plata',
    items: [
      {
        id: 'sobres',
        path: '/sobres',
        label: 'Sobres',
        icon: 'envelope',
        children: [
          { path: '/sobres', label: 'Saldos', exact: true },
          { path: '/sobres/operaciones', label: 'Operaciones' },
          { path: '/sobres/evaluacion', label: 'Evaluación del período' },
          { path: '/sobres/saldo-inicial', label: 'Saldo inicial' },
        ],
      },
      {
        id: 'gastos',
        path: '/gastos',
        label: 'Gastos',
        icon: 'receipt',
        children: [
          { path: '/gastos', label: 'Gastos', exact: true },
          { path: '/gastos/fijos', label: 'Gastos fijos', badge: 'dueExpenses' },
        ],
      },
      { id: 'reportes', path: '/reportes', label: 'Reportes', icon: 'chart' },
    ],
  },
  {
    items: [
      {
        id: 'configuracion',
        path: '/configuracion',
        label: 'Configuración',
        icon: 'sliders',
        children: [
          { path: '/configuracion/negocio', label: 'Negocio' },
          { path: '/configuracion/cobros-y-cajas', label: 'Cobros y cajas' },
          { path: '/configuracion/precios-y-moneda', label: 'Precios y moneda' },
          { path: '/configuracion/profit-first', label: 'Profit First' },
          { path: '/configuracion/personal', label: 'Personal' },
          { path: '/configuracion/gastos', label: 'Categorías de gasto' },
          { path: '/configuracion/respaldo', label: 'Respaldo' },
        ],
      },
    ],
  },
];

/**
 * Side menu. Sections unfold into their pages; the section of the page on screen opens by itself, and the user can
 * open or fold any other. Badges tell what the closing still asks for. On a phone it folds into a "Menú" button.
 */
@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.css',
})
export class Sidebar implements OnInit {
  private readonly dialog = inject(Dialog);
  private readonly router = inject(Router);
  protected readonly settings = inject(BusinessSettingsStore);
  protected readonly rate = inject(ExchangeRateStore);
  protected readonly alerts = inject(ClosingAlertsStore);
  protected readonly formatHundredths = formatHundredths;

  protected readonly groups = NAV;

  /** The URL on screen, without query or fragment. */
  private readonly url = signal(stripQuery(this.router.url));

  /** Sections the user unfolded (the current one is always added on navigation). */
  protected readonly expanded = signal<ReadonlySet<string>>(new Set());

  /** Phone only: the menu is folded behind a button. */
  protected readonly mobileOpen = signal(false);

  private readonly badges = computed<Record<BadgeKey, number>>(() => ({
    pendingDays: this.alerts.pendingDaysCount(),
    pendingShortages: this.alerts.pendingShortagesCount(),
    dueExpenses: this.alerts.dueExpensesCount(),
  }));

  /** Anything pending at all (the dot on the phone's "Menú" button). */
  protected readonly anyBadge = computed(() => Object.values(this.badges()).some((count) => count > 0));

  constructor() {
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe((event) => {
        this.url.set(stripQuery(event.urlAfterRedirects));
        this.openCurrentSection();
        this.mobileOpen.set(false);
        this.alerts.refresh();
      });
  }

  ngOnInit(): void {
    // The menu is always on screen, so it is the natural place to load the shared state once.
    this.settings.load();
    this.rate.load();
    this.alerts.refresh();
    this.openCurrentSection();
  }

  /** The page on screen is this section or one of its pages. */
  protected isWithin(item: NavItem): boolean {
    const url = this.url();
    return item.path === '/' ? url === '/' : url === item.path || url.startsWith(item.path + '/');
  }

  protected isExpanded(item: NavItem): boolean {
    return this.expanded().has(item.id);
  }

  protected toggle(item: NavItem): void {
    this.expanded.update((open) => {
      const next = new Set(open);
      if (next.has(item.id)) {
        next.delete(item.id);
      } else {
        next.add(item.id);
      }
      return next;
    });
  }

  protected badge(key: BadgeKey | undefined): number {
    return key ? this.badges()[key] : 0;
  }

  /** What a folded section hides: the sum of its children's badges. */
  protected sectionBadge(item: NavItem): number {
    return (item.children ?? []).reduce((sum, child) => sum + this.badge(child.badge), 0);
  }

  protected badgeHint(key: BadgeKey | undefined): string {
    const count = this.badge(key);
    if (key === 'pendingDays') {
      return count === 1 ? '1 día sin cerrar' : `${count} días sin cerrar`;
    }
    if (key === 'dueExpenses') {
      return count === 1 ? '1 gasto fijo por pagar' : `${count} gastos fijos por pagar`;
    }
    return count === 1 ? '1 faltante por reclamar' : `${count} faltantes por reclamar`;
  }

  protected openRateDialog(): void {
    const current = this.rate.current();
    if (current === null) {
      return;
    }

    const loadedToday = current.isMissing ? null : current.latest;
    this.dialog.open<unknown, ExchangeRateDialogData>(ExchangeRateDialog, {
      ...DIALOG_DEFAULTS,
      data: { date: current.today, today: current.today, existing: loadedToday, reference: current.latest },
    });
  }

  private openCurrentSection(): void {
    const current = NAV.flatMap((group) => group.items).find((item) => item.children && this.isWithin(item));
    if (current && !this.expanded().has(current.id)) {
      this.expanded.update((open) => new Set(open).add(current.id));
    }
  }
}

function stripQuery(url: string): string {
  const end = url.search(/[?#]/);
  return end === -1 ? url : url.slice(0, end);
}
