import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { SuppliersApi } from '../../../core/api/catalog-api';
import { PurchaseSummaryResponse, SupplierProductResponse, SupplierResponse } from '../../../core/api/models';
import { PurchasesApi } from '../../../core/api/purchases-api';
import { formatShortDate } from '../../../shared/format/date';
import { formatMicros, formatMoney } from '../../../shared/format/money';
import { perUnit } from '../../../shared/format/quantity';

/**
 * One supplier (RF-PRV-02): what we buy from them and at what landed cost the last time,
 * plus their latest purchases. Confirmed purchases only.
 */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="crumbs" aria-label="Ruta">
      <a routerLink="/productos">Productos</a> <span aria-hidden="true">/</span>
      <a routerLink="/productos/proveedores">Proveedores</a> <span aria-hidden="true">/</span>
      {{ supplier()?.name ?? '…' }}
    </nav>

    @if (loadError(); as e) {
      <p class="alert alert--bad" role="alert">{{ e.status === 404 ? 'Ese proveedor no existe.' : e.message }}</p>
    }

    @if (supplier(); as s) {
      <header class="head">
        <div>
          <h1 class="page-title">{{ s.name }}</h1>
          <p class="page-subtitle">{{ details(s) }}</p>
        </div>
        @if (s.isActive) {
          <a class="btn btn--primary" routerLink="/compras/nueva">Nueva compra</a>
        } @else {
          <span class="pill pill--muted">Inactivo</span>
        }
      </header>

      <section class="table-card" aria-labelledby="products-title">
        <div class="table-card__head">
          <h2 class="section-title" id="products-title">Qué le comprás</h2>
          <span class="sub">Costo final de la última compra (con gastos), por unidad de venta</span>
        </div>
        @if (products().length > 0) {
          <table class="table">
            <thead>
              <tr>
                <th>Producto</th>
                <th class="end">Último costo</th>
                <th>Última compra</th>
                <th class="end">Compras</th>
              </tr>
            </thead>
            <tbody>
              @for (product of products(); track product.productId) {
                <tr>
                  <td>
                    <a class="name" [routerLink]="['/productos', product.productId]">{{ product.productName }}</a>
                    <div class="sub num">{{ product.productCode }}</div>
                  </td>
                  <td class="end num strong">
                    {{ formatMicros(product.lastLandedUnitCostUsdMicros) }} USD
                    <span class="sub">{{ perUnit(product.stockUnit) }}</span>
                  </td>
                  <td>{{ formatShortDate(product.lastPurchaseDate) }}</td>
                  <td class="end num">{{ product.purchaseCount }}</td>
                </tr>
              }
            </tbody>
          </table>
        } @else {
          <p class="none">Todavía no le compraste nada.</p>
        }
      </section>

      <section class="table-card" aria-labelledby="purchases-title">
        <div class="table-card__head">
          <h2 class="section-title" id="purchases-title">Últimas compras</h2>
          @if (purchaseTotal() > purchases().length) {
            <span class="sub">{{ purchases().length }} de {{ purchaseTotal() }}</span>
          }
        </div>
        @if (purchases().length > 0) {
          <table class="table">
            <thead>
              <tr>
                <th>Fecha</th>
                <th>Productos</th>
                <th class="end">Mercadería</th>
              </tr>
            </thead>
            <tbody>
              @for (purchase of purchases(); track purchase.id) {
                <tr>
                  <td>
                    <a class="name" [routerLink]="['/compras', purchase.id]">{{ formatShortDate(purchase.date) }}</a>
                    <div class="sub num">#{{ purchase.number }}</div>
                  </td>
                  <td class="products"><span [title]="purchase.productNames">{{ purchase.productNames }}</span></td>
                  <td class="end num strong">{{ formatMoney(purchase.goodsTotalCents, purchase.currency) }}</td>
                </tr>
              }
            </tbody>
          </table>
        } @else {
          <p class="none">Sin compras.</p>
        }
      </section>
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 20px; }
    .crumbs { font-size: 13.5px; color: var(--c-muted); }
    .crumbs a { text-decoration: none; }
    .head { display: flex; flex-wrap: wrap; gap: 16px; justify-content: space-between; align-items: flex-end; }
    .table-card { background: var(--c-surface); border: 1px solid var(--c-line); border-radius: var(--r-lg); overflow-x: auto; }
    .table-card__head { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: baseline; gap: 8px; padding: 22px 24px 8px; }
    .section-title { margin: 0; font-size: 16px; font-weight: 600; }
    .table { width: 100%; min-width: 560px; border-collapse: collapse; }
    .table th { text-align: left; font-weight: 500; font-size: 12.5px; color: var(--c-muted); padding: 12px; }
    .table td { padding: 14px 12px; border-top: 1px solid var(--c-line-soft); }
    .table th:first-child, .table td:first-child { padding-left: 24px; }
    .table th:last-child, .table td:last-child { padding-right: 24px; }
    .end { text-align: right !important; }
    .name { color: var(--c-ink); text-decoration: none; font-weight: 500; }
    .name:hover { color: var(--c-link); }
    .sub { font-size: 12.5px; font-weight: 400; color: var(--c-muted); }
    .strong { font-weight: 500; }
    .products span { display: block; max-width: 380px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .none { margin: 0; padding: 8px 24px 24px; color: var(--c-muted); }
  `,
})
export default class ProveedorPage implements OnInit {
  /** Route param (withComponentInputBinding). */
  readonly id = input.required<string>();

  private readonly suppliersApi = inject(SuppliersApi);
  private readonly purchasesApi = inject(PurchasesApi);

  protected readonly formatShortDate = formatShortDate;
  protected readonly formatMicros = formatMicros;
  protected readonly formatMoney = formatMoney;
  protected readonly perUnit = perUnit;

  protected readonly supplier = signal<SupplierResponse | null>(null);
  protected readonly products = signal<SupplierProductResponse[]>([]);
  protected readonly purchases = signal<PurchaseSummaryResponse[]>([]);
  protected readonly purchaseTotal = signal(0);
  protected readonly loadError = signal<ApiError | null>(null);

  ngOnInit(): void {
    forkJoin({
      supplier: this.suppliersApi.get(this.id()),
      products: this.suppliersApi.products(this.id()),
      purchases: this.purchasesApi.getAll({ supplierId: this.id(), pageSize: 10 }),
    }).subscribe({
      next: ({ supplier, products, purchases }) => {
        this.supplier.set(supplier);
        this.products.set(products.items);
        this.purchases.set(purchases.items);
        this.purchaseTotal.set(purchases.total);
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }

  /** "Juan Pérez · 5 1234567 · Entrega los martes" — whatever is filled in. */
  protected details(supplier: SupplierResponse): string {
    const parts = [supplier.contactName, supplier.phone, supplier.notes].filter((part) => !!part);
    return parts.length > 0 ? parts.join(' · ') : 'Sin datos de contacto';
  }
}
