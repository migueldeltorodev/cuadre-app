import { Routes } from '@angular/router';

/** Gastos (slice 7). Literal segments are declared BEFORE ":id" so they are never taken for an id. */
export const GASTOS_ROUTES: Routes = [
  { path: '', title: 'Gastos · Cuadre', loadComponent: () => import('./gastos-page') },
  {
    path: 'fijos',
    title: 'Gastos fijos · Cuadre',
    data: { crumb: 'Gastos fijos' },
    loadComponent: () => import('./fijos/gastos-fijos-page'),
  },
  {
    path: ':id',
    title: 'Gasto · Cuadre',
    data: { crumb: 'Gasto' },
    loadComponent: () => import('./gasto-detalle/gasto-page'),
  },
];
