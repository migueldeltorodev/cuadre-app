import { Routes } from '@angular/router';
import { Shell } from './core/layout/shell';

/**
 * One route per menu entry. Each feature slice replaces its placeholder with
 * a lazy-loaded page (loadComponent) as it gets built.
 * `data.crumb` names each level of the breadcrumb trail (core/layout/breadcrumbs.ts).
 */
export const routes: Routes = [
  {
    path: '',
    component: Shell,
    children: [
      { path: '', title: 'Inicio · Cuadre', loadComponent: () => import('./features/inicio/inicio-page') },
      {
        path: 'cierre',
        data: { crumb: 'Cierre del día' },
        loadChildren: () => import('./features/cierre/cierre.routes').then((m) => m.CIERRE_ROUTES),
      },
      {
        path: 'productos',
        data: { crumb: 'Productos' },
        loadChildren: () => import('./features/productos/productos.routes').then((m) => m.PRODUCTOS_ROUTES),
      },
      {
        path: 'compras',
        data: { crumb: 'Compras' },
        loadChildren: () => import('./features/compras/compras.routes').then((m) => m.COMPRAS_ROUTES),
      },
      {
        path: 'inventario',
        data: { crumb: 'Inventario' },
        loadChildren: () => import('./features/inventario/inventario.routes').then((m) => m.INVENTARIO_ROUTES),
      },
      {
        path: 'sobres',
        data: { crumb: 'Sobres' },
        loadChildren: () => import('./features/sobres/sobres.routes').then((m) => m.SOBRES_ROUTES),
      },
      {
        path: 'gastos',
        data: { crumb: 'Gastos' },
        loadChildren: () => import('./features/gastos/gastos.routes').then((m) => m.GASTOS_ROUTES),
      },
      placeholder('reportes', 'Reportes'),
      {
        path: 'configuracion',
        title: 'Configuración · Cuadre',
        data: { crumb: 'Configuración' },
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
    data: { name, crumb: name },
    loadComponent: () => import('./shared/placeholder-page'),
  };
}
