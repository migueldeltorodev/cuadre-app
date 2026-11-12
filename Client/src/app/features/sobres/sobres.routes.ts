import { Routes } from '@angular/router';

/** Sobres (slice 5). Literal segments are declared BEFORE ":id" so they are never taken for an id. */
export const SOBRES_ROUTES: Routes = [
  { path: '', title: 'Sobres · Cuadre', loadComponent: () => import('./sobres-page') },
  {
    path: 'saldo-inicial',
    title: 'Saldo inicial · Sobres · Cuadre',
    loadComponent: () => import('./saldo-inicial/opening-page'),
  },
  {
    path: 'operaciones',
    title: 'Operaciones · Sobres · Cuadre',
    loadComponent: () => import('./operaciones/envelope-operations-page'),
  },
  {
    path: 'operaciones/:id',
    title: 'Operación · Sobres · Cuadre',
    loadComponent: () => import('./operacion/envelope-operation-page'),
  },
  {
    path: ':id',
    title: 'Sobre · Cuadre',
    loadComponent: () => import('./sobre/envelope-page'),
  },
];
