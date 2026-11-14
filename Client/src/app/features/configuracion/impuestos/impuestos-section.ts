import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { TaxResponse } from '../../../core/api/models';
import { TaxesApi } from '../../../core/api/taxes-api';
import { formatPercent } from '../../../shared/format/money';
import { DIALOG_DEFAULTS } from '../../../shared/ui/dialog-config';
import { TaxDialog, TaxDialogData } from './tax-dialog';

/** Configuración › Impuestos (D-62). None by default; deactivated, never deleted. */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loadError(); as e) {
      <p class="alert alert--bad" role="alert">{{ e.message }}</p>
    }

    <section class="card" aria-labelledby="taxes-title">
      <div class="card__head">
        <div>
          <h2 class="card__title" id="taxes-title">Impuestos</h2>
          <p class="card__lead">
            Si el negocio tiene que pagar un impuesto, cargalo acá y la app te <strong>sugiere</strong> cuánto apartar
            cada mes: en el <a routerLink="/reportes/contador">resumen para el contador</a> y junto al sobre Impuestos.
            Es solo una sugerencia: el reparto de Profit First sigue igual.
          </p>
        </div>
        <button class="btn btn--small" type="button" (click)="open()">+ Agregar impuesto</button>
      </div>

      @if (taxes(); as list) {
        <div class="rows">
          @for (tax of list; track tax.id) {
            <div class="row" [class.row--inactive]="!tax.isActive">
              <div class="row__main">
                <div class="row__title">{{ tax.name }}</div>
                <div class="row__sub num">
                  {{ formatPercent(tax.rateBps) }} {{ tax.base === 'Sales' ? 'de las ventas' : 'de la ganancia' }}
                </div>
              </div>
              @if (!tax.isActive) {
                <span class="pill pill--muted">Desactivado</span>
              }
              <button class="btn btn--ghost btn--small" type="button" (click)="open(tax)"
                [attr.aria-label]="'Editar ' + tax.name">Editar</button>
            </div>
          } @empty {
            <p class="empty">No hay impuestos cargados. Mientras no haya ninguno, no se sugiere apartar nada.</p>
          }
        </div>
      }
    </section>
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 20px; }
    .card { display: flex; flex-direction: column; gap: 12px; }
    .empty {
      margin: 0; padding: 14px 0; border-top: 1px solid var(--c-line-soft); color: var(--c-muted); font-size: 14px;
    }
  `,
})
export default class ImpuestosSection implements OnInit {
  private readonly dialog = inject(Dialog);
  private readonly api = inject(TaxesApi);

  protected readonly formatPercent = formatPercent;

  protected readonly taxes = signal<TaxResponse[] | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);

  ngOnInit(): void {
    this.load();
  }

  protected open(tax?: TaxResponse): void {
    this.dialog
      .open<TaxResponse, TaxDialogData>(TaxDialog, { ...DIALOG_DEFAULTS, data: { tax } })
      .closed.subscribe((saved) => saved && this.load());
  }

  private load(): void {
    this.api.getAll().subscribe({
      next: (result) => {
        this.taxes.set(result.items);
        this.loadError.set(null);
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }
}
