import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { InventoryApi } from '../../../core/api/inventory-api';
import { ShoppingListItemResponse } from '../../../core/api/models';
import { formatMicros, formatMoney } from '../../../shared/format/money';
import { formatQuantityWithUnit, perUnit } from '../../../shared/format/quantity';

interface SupplierGroup {
  supplierId: string | null;
  supplierName: string;
  items: ShoppingListItemResponse[];
  /** Null when no item of the group has a known cost. */
  estimatedUsdCents: number | null;
}

/**
 * Lista de compras (RF-INV-07): what is below its minimum, grouped by the usual supplier, with how much to buy
 * in whole purchase units and a rough USD estimate at the last landed cost.
 */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loadError(); as e) {
      <p class="alert alert--bad" role="alert">{{ e.message }}</p>
    }

    @if (loaded()) {
      @if (groups().length === 0) {
        <section class="card empty">
          <h2 class="card__title">No falta nada</h2>
          <p class="card__lead">
            Ningún producto está por debajo de su mínimo. Los mínimos se ponen en la ficha de cada producto.
          </p>
        </section>
      } @else {
        <div class="head">
          <p class="muted">
            {{ items().length }} {{ items().length === 1 ? 'producto está' : 'productos están' }} por debajo del mínimo.
            @if (totalUsdCents() !== null) {
              Estimado: <strong class="num">{{ formatMoney(totalUsdCents()!, 'USD') }}</strong> al último costo.
            }
          </p>
          <a class="btn btn--primary" routerLink="/compras/nueva">Registrar una compra</a>
        </div>

        @for (group of groups(); track group.supplierId) {
          <section class="table-card" [attr.aria-label]="group.supplierName">
            <div class="table-card__head group-head">
              <h2 class="section-title">{{ group.supplierName }}</h2>
              @if (group.estimatedUsdCents !== null) {
                <span class="num muted">≈ {{ formatMoney(group.estimatedUsdCents, 'USD') }}</span>
              }
            </div>
            <table class="table">
              <thead>
                <tr>
                  <th>Producto</th>
                  <th class="end">Hay</th>
                  <th class="end">Mínimo</th>
                  <th class="end">Falta</th>
                  <th class="end">Comprar</th>
                  <th class="end">Último costo</th>
                  <th class="end">Estimado</th>
                </tr>
              </thead>
              <tbody>
                @for (item of group.items; track item.productId) {
                  <tr>
                    <td>
                      <a class="name" routerLink="../historial" [queryParams]="{ producto: item.productId }">{{ item.name }}</a>
                      <div class="sub num">{{ item.code }}</div>
                    </td>
                    <td class="end num" [class.negative]="item.stockMilli < 0">{{ formatQuantityWithUnit(item.stockMilli, item.stockUnit) }}</td>
                    <td class="end num">{{ formatQuantityWithUnit(item.minStockMilli, item.stockUnit) }}</td>
                    <td class="end num out">{{ formatQuantityWithUnit(item.missingMilli, item.stockUnit) }}</td>
                    <td class="end num strong">{{ formatQuantityWithUnit(item.suggestedPurchaseQuantityMilli, item.purchaseUnit) }}</td>
                    <td class="end num">
                      @if (item.lastUnitCostUsdMicros !== null) {
                        {{ formatMicros(item.lastUnitCostUsdMicros) }} USD <span class="sub">{{ perUnit(item.purchaseUnit) }}</span>
                      } @else { — }
                    </td>
                    <td class="end num">{{ item.estimatedUsdCents === null ? '—' : formatMoney(item.estimatedUsdCents, 'USD') }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </section>
        }
        <p class="muted">
          "Comprar" redondea hacia arriba a unidades de compra enteras. El estimado es orientativo: usa el último costo
          puesto en el almacén, que puede haber cambiado.
        </p>
      }
    }
  `,
  styleUrls: ['../inventory-shared.css'],
  styles: `
    .group-head {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      gap: 16px;
    }
  `,
})
export default class ShoppingListTab implements OnInit {
  private readonly api = inject(InventoryApi);

  protected readonly formatMoney = formatMoney;
  protected readonly formatMicros = formatMicros;
  protected readonly formatQuantityWithUnit = formatQuantityWithUnit;
  protected readonly perUnit = perUnit;

  protected readonly items = signal<ShoppingListItemResponse[]>([]);
  protected readonly loaded = signal(false);
  protected readonly loadError = signal<ApiError | null>(null);

  /** By supplier name; the products without a usual supplier go last. */
  protected readonly groups = computed<SupplierGroup[]>(() => {
    const bySupplier = new Map<string | null, SupplierGroup>();
    for (const item of this.items()) {
      const key = item.usualSupplierId;
      let group = bySupplier.get(key);
      if (!group) {
        group = {
          supplierId: key,
          supplierName: item.usualSupplierName ?? 'Sin proveedor habitual',
          items: [],
          estimatedUsdCents: null,
        };
        bySupplier.set(key, group);
      }
      group.items.push(item);
      if (item.estimatedUsdCents !== null) {
        group.estimatedUsdCents = (group.estimatedUsdCents ?? 0) + item.estimatedUsdCents;
      }
    }
    return [...bySupplier.values()].sort((a, b) => {
      if (a.supplierId === null) return 1;
      if (b.supplierId === null) return -1;
      return a.supplierName.localeCompare(b.supplierName, 'es');
    });
  });

  protected readonly totalUsdCents = computed(() => {
    const known = this.groups().filter((group) => group.estimatedUsdCents !== null);
    return known.length === 0 ? null : known.reduce((total, group) => total + group.estimatedUsdCents!, 0);
  });

  ngOnInit(): void {
    this.api.shoppingList().subscribe({
      next: (response) => {
        this.items.set(response.items);
        this.loaded.set(true);
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }
}
