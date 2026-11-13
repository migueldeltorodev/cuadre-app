import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { SuppliersApi } from '../../../core/api/catalog-api';
import { SupplierResponse } from '../../../core/api/models';
import { DIALOG_DEFAULTS } from '../../../shared/ui/dialog-config';
import { SupplierDialog, SupplierDialogData } from './supplier-dialog';

/** Suppliers (RF-PRV-01). Each name opens its page: what we buy from them and at what cost (RF-PRV-02). */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="head">
      <div>
        <h1 class="page-title">Proveedores</h1>
        <p class="page-subtitle">A quién le comprás. No se borran: se desactivan.</p>
      </div>
      <button class="btn btn--primary" type="button" (click)="open()">+ Nuevo proveedor</button>
    </header>

    @if (error(); as e) {
      <p class="alert alert--bad" role="alert">{{ e.message }}</p>
    }

    @if (suppliers(); as list) {
      <section class="card">
        <div class="rows">
          @for (supplier of list; track supplier.id) {
            <div class="row" [class.row--inactive]="!supplier.isActive">
              <div class="row__main">
                <a class="row__title row__link" [routerLink]="supplier.id">{{ supplier.name }}</a>
                <div class="row__sub">{{ details(supplier) }}</div>
              </div>
              @if (supplier.isActive) {
                <span class="pill pill--ok">Activo</span>
              } @else {
                <span class="pill pill--muted">Inactivo</span>
              }
              <button class="btn btn--ghost btn--small" type="button" (click)="open(supplier)"
                [attr.aria-label]="'Editar ' + supplier.name">Editar</button>
            </div>
          } @empty {
            <p class="card__lead">Todavía no hay proveedores.</p>
          }
        </div>
      </section>
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 20px; max-width: 880px; }
    .head { display: flex; flex-wrap: wrap; gap: 16px; justify-content: space-between; align-items: flex-end; }
    .card { padding: 8px 24px; }
    .rows .row:first-child { border-top: 0; }
    .card__lead { padding: 14px 0; margin: 0; }
    .row__link { display: block; color: var(--c-ink); text-decoration: none; }
    .row__link:hover { color: var(--c-link); }
  `,
})
export default class ProveedoresPage implements OnInit {
  private readonly dialog = inject(Dialog);
  private readonly api = inject(SuppliersApi);

  protected readonly suppliers = signal<SupplierResponse[] | null>(null);
  protected readonly error = signal<ApiError | null>(null);

  ngOnInit(): void {
    this.reload();
  }

  /** "Juan Pérez · 5 1234567 · Entrega los martes" — whatever is filled in. */
  protected details(supplier: SupplierResponse): string {
    const parts = [supplier.contactName, supplier.phone, supplier.notes].filter((part) => !!part);
    return parts.length > 0 ? parts.join(' · ') : 'Sin datos de contacto';
  }

  protected open(supplier?: SupplierResponse): void {
    this.dialog
      .open<SupplierResponse, SupplierDialogData>(SupplierDialog, { ...DIALOG_DEFAULTS, data: { supplier } })
      .closed.subscribe((saved) => saved && this.reload());
  }

  private reload(): void {
    this.api.getAll().subscribe({
      next: (response) => {
        this.suppliers.set(response.items);
        this.error.set(null);
      },
      error: (err: unknown) => this.error.set(toApiError(err)),
    });
  }
}
