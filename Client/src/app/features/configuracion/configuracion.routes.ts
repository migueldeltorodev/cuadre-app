import { Routes } from '@angular/router';
import ConfiguracionPage from './configuracion-page';

/** Sub-sections from the approved design. The ones of later slices show a placeholder. */
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
      later('respaldo', 'Respaldo'),
    ],
  },
];

function later(path: string, name: string) {
  return {
    path,
    title: `${name} · Configuración · Cuadre`,
    data: { name, crumb: name },
    loadComponent: () => import('../../shared/placeholder-page'),
  };
}
