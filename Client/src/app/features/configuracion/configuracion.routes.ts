import { Routes } from '@angular/router';
import ConfiguracionPage from './configuracion-page';

/** Sub-sections from the approved design. */
export const CONFIGURACION_ROUTES: Routes = [
  {
    path: '',
    component: ConfiguracionPage,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'negocio' },
      {
        path: 'negocio',
        title: 'Negocio · Configuración · Cuadre',
        data: { crumb: 'Negocio' },
        loadComponent: () => import('./negocio/negocio-section'),
      },
      {
        path: 'cobros-y-cajas',
        title: 'Cobros y cajas · Configuración · Cuadre',
        data: { crumb: 'Cobros y cajas' },
        loadComponent: () => import('./cobros-y-cajas/cobros-y-cajas-section'),
      },
      {
        path: 'precios-y-moneda',
        title: 'Precios y moneda · Configuración · Cuadre',
        data: { crumb: 'Precios y moneda' },
        loadComponent: () => import('./precios-y-moneda/precios-y-moneda-section'),
      },
      {
        path: 'profit-first',
        title: 'Profit First · Configuración · Cuadre',
        data: { crumb: 'Profit First' },
        loadComponent: () => import('./profit-first/profit-first-section'),
      },
      {
        path: 'personal',
        title: 'Personal · Configuración · Cuadre',
        data: { crumb: 'Personal' },
        loadComponent: () => import('./personal/personal-section'),
      },
      {
        path: 'gastos',
        title: 'Categorías de gasto · Configuración · Cuadre',
        data: { crumb: 'Categorías de gasto' },
        loadComponent: () => import('./gastos/gastos-section'),
      },
      {
        path: 'impuestos',
        title: 'Impuestos · Configuración · Cuadre',
        data: { crumb: 'Impuestos' },
        loadComponent: () => import('./impuestos/impuestos-section'),
      },
      {
        path: 'respaldo',
        title: 'Respaldo · Configuración · Cuadre',
        data: { crumb: 'Respaldo' },
        loadComponent: () => import('./respaldo/respaldo-section'),
      },
    ],
  },
];
