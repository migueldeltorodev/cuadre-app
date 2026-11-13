import { Routes } from '@angular/router';

/** Sobres (slice 5). Literal segments are declared BEFORE ":id" so they are never taken for an id. */
export const SOBRES_ROUTES: Routes = [
  { path: '', title: 'Sobres · Cuadre', loadComponent: () => import('./sobres-page') },
  {
    path: 'saldo-inicial',
    title: 'Saldo inicial · Sobres · Cuadre',
    data: { crumb: 'Saldo inicial' },
    loadComponent: () => import('./saldo-inicial/opening-page'),
  },
  {
    path: 'evaluacion',
    title: 'Evaluación · Sobres · Cuadre',
    data: { crumb: 'Evaluación del período' },
    loadComponent: () => import('./evaluacion/evaluation-page'),
  },
  {
    path: 'operaciones',
    title: 'Operaciones · Sobres · Cuadre',
    data: { crumb: 'Operaciones' },
    children: [
      { path: '', loadComponent: () => import('./operaciones/envelope-operations-page') },
      {
        path: ':id',
        title: 'Operación · Sobres · Cuadre',
        data: { crumb: 'Operación' },
        loadComponent: () => import('./operacion/envelope-operation-page'),
      },
    ],
  },
  {
    path: ':id',
    title: 'Sobre · Cuadre',
    data: { crumb: 'Sobre' },
    loadComponent: () => import('./sobre/envelope-page'),
  },
];
