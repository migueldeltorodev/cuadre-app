import { Routes } from '@angular/router';

/**
 * Catalog (slice 2). Literal paths ("nuevo", "lista-de-precios"...) are declared BEFORE ":id",
 * so the router never mistakes them for a product id.
 */
export const PRODUCTOS_ROUTES: Routes = [
  { path: '', title: 'Productos · Cuadre', loadComponent: () => import('./productos-page') },
  {
    path: 'nuevo',
    title: 'Nuevo producto · Cuadre',
    loadComponent: () => import('./producto-form/producto-form-page'),
  },
  {
    path: 'lista-de-precios',
    title: 'Lista de precios · Cuadre',
    loadComponent: () => import('./lista-precios/lista-precios-page'),
  },
  {
    path: 'categorias',
    title: 'Categorías · Cuadre',
    loadComponent: () => import('./categorias/categorias-page'),
  },
  {
    path: 'proveedores',
    title: 'Proveedores · Cuadre',
    loadComponent: () => import('./proveedores/proveedores-page'),
  },
  {
    path: 'proveedores/:id',
    title: 'Proveedor · Cuadre',
    loadComponent: () => import('./proveedores/proveedor-page'),
  },
  {
    path: ':id',
    title: 'Producto · Cuadre',
    loadComponent: () => import('./producto-detalle/producto-page'),
  },
  {
    path: ':id/editar',
    title: 'Editar producto · Cuadre',
    loadComponent: () => import('./producto-form/producto-form-page'),
  },
];
