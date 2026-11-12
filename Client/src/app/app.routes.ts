import { Routes } from '@angular/router';
import { Shell } from './core/layout/shell';

/**
 * One route per menu entry. Each feature slice replaces its placeholder with
 * a lazy-loaded page (loadComponent) as it gets built.
 */
export const routes: Routes = [
  {
    path: '',
    component: Shell,
    children: [
      { path: '', title: 'Inicio · Cuadre', loadComponent: () => import('./features/inicio/inicio-page') },
      placeholder('cierre', 'Cierre del día'),
      placeholder('productos', 'Productos'),
      placeholder('compras', 'Compras'),
      placeholder('inventario', 'Inventario'),
      placeholder('sobres', 'Sobres'),
      placeholder('gastos', 'Gastos'),
      placeholder('reportes', 'Reportes'),
      {
        path: 'configuracion',
        title: 'Configuración · Cuadre',
        loadChildren: () =>
          import('./features/configuracion/configuracion.routes').then((m) => m.CONFIGURACION_ROUTES),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];

function placeholder(path: string, name: string) {
  return {
    path,
    title: `${name} · Cuadre`,
    data: { name },
    loadComponent: () => import('./shared/placeholder-page'),
  };
}
