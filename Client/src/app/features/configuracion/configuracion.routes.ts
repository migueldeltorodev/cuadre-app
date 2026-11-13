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
        loadComponent: () => import('./negocio/negocio-section'),
      },
      {
        path: 'cobros-y-cajas',
        title: 'Cobros y cajas · Configuración · Cuadre',
        loadComponent: () => import('./cobros-y-cajas/cobros-y-cajas-section'),
      },
      {
        path: 'precios-y-moneda',
        title: 'Precios y moneda · Configuración · Cuadre',
        loadComponent: () => import('./precios-y-moneda/precios-y-moneda-section'),
      },
      {
        path: 'profit-first',
        title: 'Profit First · Configuración · Cuadre',
        loadComponent: () => import('./profit-first/profit-first-section'),
      },
      later('personal', 'Personal'),
      later('respaldo', 'Respaldo'),
    ],
  },
];

function later(path: string, name: string) {
  return {
    path,
    title: `${name} · Configuración · Cuadre`,
    data: { name },
    loadComponent: () => import('../../shared/placeholder-page'),
  };
}
