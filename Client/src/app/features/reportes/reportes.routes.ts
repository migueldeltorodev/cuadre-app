import { Routes } from '@angular/router';

/** Reportes (slice 8). Literal segments are declared BEFORE ":id" so they are never taken for an id. */
export const REPORTES_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'mensual' },
  {
    path: 'mensual',
    title: 'Cierre del mes · Cuadre',
    data: { crumb: 'Cierre del mes' },
    loadComponent: () => import('./mensual/monthly-report-page'),
  },
  {
    path: 'productos',
    title: 'Productos · Reportes · Cuadre',
    data: { crumb: 'Productos' },
    loadComponent: () => import('./productos/product-ranking-page'),
  },
  {
    path: 'perdidas',
    title: 'Pérdidas · Cuadre',
    data: { crumb: 'Pérdidas' },
    loadComponent: () => import('./perdidas/loss-report-page'),
  },
  {
    path: 'costos',
    data: { crumb: 'Costos y proveedores' },
    children: [
      { path: '', title: 'Costos y proveedores · Cuadre', loadComponent: () => import('./costos/cost-changes-page') },
      {
        path: ':id',
        title: 'Costos del producto · Cuadre',
        data: { crumb: 'Producto' },
        loadComponent: () => import('./costos/product-costs-page'),
      },
    ],
  },
];
