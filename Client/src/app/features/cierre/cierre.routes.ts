import { Routes } from '@angular/router';

/** Cierre del día (slice 6). Literal segments are declared BEFORE ":id" so they are never taken for an id. */
export const CIERRE_ROUTES: Routes = [
  { path: '', title: 'Cierre del día · Cuadre', loadComponent: () => import('./cierre-page') },
  {
    path: 'historial',
    title: 'Cierres anteriores · Cuadre',
    data: { crumb: 'Cierres anteriores' },
    loadComponent: () => import('./historial/cierres-page'),
  },
  {
    path: 'faltantes',
    title: 'Faltantes y sobrantes · Cuadre',
    data: { crumb: 'Faltantes y sobrantes' },
    children: [
      { path: '', loadComponent: () => import('./faltantes/faltantes-page') },
      {
        path: ':id',
        title: 'Faltante · Cuadre',
        data: { crumb: 'Faltante' },
        loadComponent: () => import('./faltante-detalle/faltante-page'),
      },
    ],
  },
  {
    path: ':id',
    title: 'Cierre · Cuadre',
    data: { crumb: 'Cierre' },
    loadComponent: () => import('./cierre-detalle/cierre-detalle-page'),
  },
];
