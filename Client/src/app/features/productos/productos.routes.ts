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
    data: { crumb: 'Nuevo producto' },
    loadComponent: () => import('./producto-form/producto-form-page'),
  },
  {
    path: 'lista-de-precios',
    title: 'Lista de precios · Cuadre',
    data: { crumb: 'Lista de precios' },
    loadComponent: () => import('./lista-precios/lista-precios-page'),
  },
  {
    path: 'categorias',
    title: 'Categorías · Cuadre',
    data: { crumb: 'Categorías' },
    loadComponent: () => import('./categorias/categorias-page'),
  },
  {
    path: 'proveedores',
    title: 'Proveedores · Cuadre',
    data: { crumb: 'Proveedores' },
    children: [
      { path: '', loadComponent: () => import('./proveedores/proveedores-page') },
      {
        path: ':id',
        title: 'Proveedor · Cuadre',
        data: { crumb: 'Proveedor' },
        loadComponent: () => import('./proveedores/proveedor-page'),
      },
    ],
  },
  {
    path: ':id',
    title: 'Producto · Cuadre',
    data: { crumb: 'Producto' },
    loadComponent: () => import('./producto-detalle/producto-page'),
  },
  {
    path: ':id/editar',
    title: 'Editar producto · Cuadre',
    data: { crumb: 'Editar' },
    loadComponent: () => import('./producto-form/producto-form-page'),
  },
];
