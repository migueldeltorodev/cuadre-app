import { Routes } from '@angular/router';

/**
 * Inventory (slice 4). The tabs live under the InventarioPage shell; the forms and the detail of an
 * operation are full pages of their own. Literal segments come before ":id".
 */
export const INVENTARIO_ROUTES: Routes = [
  {
    path: 'traslado',
    title: 'Llevar mercadería · Cuadre',
    data: { kind: 'Transfer', crumb: 'Nuevo traslado' },
    loadComponent: () => import('./operacion-form/operacion-form-page'),
  },
  {
    path: 'salida',
    title: 'Registrar salida · Cuadre',
    data: { kind: 'Exit', crumb: 'Nueva salida' },
    loadComponent: () => import('./operacion-form/operacion-form-page'),
  },
  {
    path: 'conteo',
    title: 'Conteo · Cuadre',
    data: { kind: 'Count', crumb: 'Nuevo conteo' },
    loadComponent: () => import('./operacion-form/operacion-form-page'),
  },
  {
    path: 'operaciones/:id',
    title: 'Operación de stock · Cuadre',
    data: { crumb: 'Operación' },
    loadComponent: () => import('./operacion-detalle/operacion-page'),
  },
  {
    path: '',
    loadComponent: () => import('./inventario-page'),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'stock' },
      {
        path: 'stock',
        title: 'Inventario · Cuadre',
        data: { crumb: 'Stock' },
        loadComponent: () => import('./stock/stock-tab'),
      },
      {
        path: 'traslados',
        title: 'Traslados · Cuadre',
        data: { kind: 'Transfer', crumb: 'Traslados' },
        loadComponent: () => import('./operaciones/operations-tab'),
      },
      {
        path: 'salidas',
        title: 'Salidas · Cuadre',
        data: { kind: 'Exit', crumb: 'Salidas' },
        loadComponent: () => import('./operaciones/operations-tab'),
      },
      {
        path: 'conteos',
        title: 'Conteos · Cuadre',
        data: { kind: 'Count', crumb: 'Conteos' },
        loadComponent: () => import('./operaciones/operations-tab'),
      },
      {
        path: 'historial',
        title: 'Historial por producto · Cuadre',
        data: { crumb: 'Historial por producto' },
        loadComponent: () => import('./kardex/kardex-tab'),
      },
      {
        path: 'lista-de-compras',
        title: 'Lista de compras · Cuadre',
        data: { crumb: 'Lista de compras' },
        loadComponent: () => import('./lista-compras/shopping-list-tab'),
      },
      {
        path: 'ubicaciones',
        title: 'Ubicaciones · Cuadre',
        data: { crumb: 'Ubicaciones' },
        loadComponent: () => import('./ubicaciones/locations-tab'),
      },
    ],
  },
];
