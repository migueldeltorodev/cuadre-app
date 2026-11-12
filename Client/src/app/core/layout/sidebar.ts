import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { ExchangeRateDialog, ExchangeRateDialogData } from '../exchange-rate/exchange-rate-dialog';
import { BusinessSettingsStore } from '../state/business-settings-store';
import { ExchangeRateStore } from '../state/exchange-rate-store';
import { formatHundredths } from '../../shared/format/money';
import { DIALOG_DEFAULTS } from '../../shared/ui/dialog-config';

interface NavItem {
  path: string;
  label: string;
  icon: string;
}

interface NavGroup {
  title?: string;
  items: NavItem[];
}

/** Side menu from the approved design ("Componente · Menú lateral"). */
@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.css',
})
export class Sidebar implements OnInit {
  private readonly dialog = inject(Dialog);
  protected readonly settings = inject(BusinessSettingsStore);
  protected readonly rate = inject(ExchangeRateStore);
  protected readonly formatHundredths = formatHundredths;

  protected readonly groups: NavGroup[] = [
    {
      items: [
        { path: '/', label: 'Inicio', icon: 'home' },
        { path: '/cierre', label: 'Cierre del día', icon: 'close' },
      ],
    },
    {
      title: 'Mercadería',
      items: [
        { path: '/productos', label: 'Productos', icon: 'box' },
        { path: '/compras', label: 'Compras', icon: 'cart' },
        { path: '/inventario', label: 'Inventario', icon: 'shelves' },
      ],
    },
    {
      title: 'Plata',
      items: [
        { path: '/sobres', label: 'Sobres', icon: 'envelope' },
        { path: '/gastos', label: 'Gastos', icon: 'receipt' },
        { path: '/reportes', label: 'Reportes', icon: 'chart' },
      ],
    },
    {
      items: [{ path: '/configuracion', label: 'Configuración', icon: 'sliders' }],
    },
  ];

  ngOnInit(): void {
    // The menu is always on screen, so it is the natural place to load the shared state once.
    this.settings.load();
    this.rate.load();
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
}
