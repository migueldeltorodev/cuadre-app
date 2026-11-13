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
      {
        path: 'cierre',
        loadChildren: () => import('./features/cierre/cierre.routes').then((m) => m.CIERRE_ROUTES),
      },
      {
        path: 'productos',
        loadChildren: () => import('./features/productos/productos.routes').then((m) => m.PRODUCTOS_ROUTES),
      },
      {
        path: 'compras',
        loadChildren: () => import('./features/compras/compras.routes').then((m) => m.COMPRAS_ROUTES),
      },
      {
        path: 'inventario',
        loadChildren: () => import('./features/inventario/inventario.routes').then((m) => m.INVENTARIO_ROUTES),
      },
      {
        path: 'sobres',
        loadChildren: () => import('./features/sobres/sobres.routes').then((m) => m.SOBRES_ROUTES),
      },
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
