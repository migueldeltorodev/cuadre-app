import { Routes } from '@angular/router';

/**
 * Inventory (slice 4). The tabs live under the InventarioPage shell; the forms and the detail of an
 * operation are full pages of their own. Literal segments come before ":id".
 */
export const INVENTARIO_ROUTES: Routes = [
  {
    path: 'traslado',
    title: 'Llevar mercadería · Cuadre',
    data: { kind: 'Transfer' },
    loadComponent: () => import('./operacion-form/operacion-form-page'),
  },
  {
    path: 'salida',
    title: 'Registrar salida · Cuadre',
    data: { kind: 'Exit' },
    loadComponent: () => import('./operacion-form/operacion-form-page'),
  },
  {
    path: 'conteo',
    title: 'Conteo · Cuadre',
    data: { kind: 'Count' },
    loadComponent: () => import('./operacion-form/operacion-form-page'),
  },
  {
    path: 'operaciones/:id',
    title: 'Operación de stock · Cuadre',
    loadComponent: () => import('./operacion-detalle/operacion-page'),
  },
  {
    path: '',
    loadComponent: () => import('./inventario-page'),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'stock' },
      { path: 'stock', title: 'Inventario · Cuadre', loadComponent: () => import('./stock/stock-tab') },
      {
        path: 'traslados',
        title: 'Traslados · Cuadre',
        data: { kind: 'Transfer' },
        loadComponent: () => import('./operaciones/operations-tab'),
      },
      {
        path: 'salidas',
        title: 'Salidas · Cuadre',
        data: { kind: 'Exit' },
        loadComponent: () => import('./operaciones/operations-tab'),
      },
      {
        path: 'conteos',
        title: 'Conteos · Cuadre',
        data: { kind: 'Count' },
        loadComponent: () => import('./operaciones/operations-tab'),
      },
      {
        path: 'historial',
        title: 'Historial por producto · Cuadre',
        loadComponent: () => import('./kardex/kardex-tab'),
      },
      {
        path: 'lista-de-compras',
        title: 'Lista de compras · Cuadre',
        loadComponent: () => import('./lista-compras/shopping-list-tab'),
      },
      {
        path: 'ubicaciones',
        title: 'Ubicaciones · Cuadre',
        loadComponent: () => import('./ubicaciones/locations-tab'),
      },
    ],
  },
];
