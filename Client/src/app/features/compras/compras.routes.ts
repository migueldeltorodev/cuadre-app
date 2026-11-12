import { Routes } from '@angular/router';

/** Purchases (slice 3). "nueva" is declared BEFORE ":id" so it is never taken for an id. */
export const COMPRAS_ROUTES: Routes = [
  { path: '', title: 'Compras · Cuadre', loadComponent: () => import('./compras-page') },
  {
    path: 'nueva',
    title: 'Nueva compra · Cuadre',
    loadComponent: () => import('./compra-form/compra-form-page'),
  },
  {
    path: ':id',
    title: 'Compra · Cuadre',
    loadComponent: () => import('./compra-detalle/compra-page'),
  },
];
