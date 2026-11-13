import { Routes } from '@angular/router';

/** Cierre del día (slice 6). Literal segments are declared BEFORE ":id" so they are never taken for an id. */
export const CIERRE_ROUTES: Routes = [
  { path: '', title: 'Cierre del día · Cuadre', loadComponent: () => import('./cierre-page') },
  {
    path: 'historial',
    title: 'Cierres anteriores · Cuadre',
    loadComponent: () => import('./historial/cierres-page'),
  },
  {
    path: ':id',
    title: 'Cierre · Cuadre',
    loadComponent: () => import('./cierre-detalle/cierre-detalle-page'),
  },
];
