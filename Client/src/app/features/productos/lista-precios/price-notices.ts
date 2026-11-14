import { ChangeDetectionStrategy, Component, OnInit, inject, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { ProductsApi } from '../../../core/api/catalog-api';
import { PriceNoticeResponse } from '../../../core/api/models';
import { formatHundredths, formatMicros } from '../../../shared/format/money';
import { perUnit } from '../../../shared/format/quantity';

/**
 * D-61: the prices to look at, because a new batch went on sale or the next one will not fit in the price.
 * The app never changes a price by itself: each notice is answered here. Taking the suggested price is a normal
 * price change (in force from today), so the price list flags it to copy it to the sign.
 */
@Component({
  selector: 'app-price-notices',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (notices().length > 0) {
      <section class="notices" aria-labelledby="notices-title">
        <h2 class="notices__title" id="notices-title">
          {{ notices().length === 1 ? 'Un precio para revisar' : notices().length + ' precios para revisar' }}
        </h2>
        <p class="notices__hint">
          La app nunca cambia un precio sola. Si aceptás uno, rige desde hoy y queda marcado abajo para pasarlo al cartel.
        </p>
        @for (n of notices(); track n.productId + n.kind) {
          <article class="notice" [class.notice--warn]="n.kind === 'NewBatchCostlier'">
            <div class="notice__body">
              <div class="notice__head">
                <a class="notice__name" [routerLink]="['/productos', n.productId]">{{ n.productName }}</a>
                <span [class]="n.kind === 'BatchChanged' ? 'pill pill--muted' : 'pill pill--warn'">
                  {{ n.kind === 'BatchChanged' ? 'Tanda nueva en venta' : 'La próxima tanda no entra' }}
                </span>
              </div>
              <p class="notice__text num">
                @if (n.kind === 'BatchChanged') {
                  Ahora se vende la tanda de {{ formatMicros(n.batchCostCupMicros) }} CUP {{ perUnit(n.stockUnit) }}
                  (tasa {{ formatHundredths(n.batchRateCents) }}). Le corresponde
                  <strong>{{ formatHundredths(n.suggestedPriceCents) }}</strong>{{ priceToday(n) }}.
                } @else {
                  Ya compraste una tanda a {{ formatMicros(n.batchCostCupMicros) }} CUP {{ perUnit(n.stockUnit) }}
                  (tasa {{ formatHundredths(n.batchRateCents) }}): va a necesitar
                  <strong>{{ formatHundredths(n.suggestedPriceCents) }}</strong>{{ priceToday(n) }}.
                  Si subís antes de que se termine la tanda de ahora, Reposición no se queda corta.
                }
              </p>
            </div>
            <div class="notice__actions">
              <button class="btn btn--primary btn--small" type="button" [disabled]="busy() !== null"
                (click)="resolve(n, true)">
                {{ n.kind === 'BatchChanged' ? 'Usar' : 'Subir a' }} {{ formatHundredths(n.suggestedPriceCents) }}
              </button>
              <button class="btn btn--small" type="button" [disabled]="busy() !== null" (click)="resolve(n, false)">
                {{ n.kind === 'BatchChanged' ? keepText(n) : 'Esperar' }}
              </button>
            </div>
          </article>
        }
        @if (error(); as e) {
          <p class="alert alert--bad" role="alert">{{ e.message }}</p>
        }
      </section>
    }
  `,
  styles: `
    .notices { display: flex; flex-direction: column; gap: 10px; }
    .notices__title { margin: 0; font-size: 15px; font-weight: 600; }
    .notice {
      display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px 20px;
      padding: 14px 16px; border: 1px solid var(--c-line); border-radius: var(--r-lg);
      background: var(--c-surface);
    }
    .notice--warn { border-color: var(--c-warn-soft); background: var(--c-surface-soft); }
    .notices__hint { margin: 0; font-size: 13px; color: var(--c-muted); }
    .notice__body { flex: 1 1 320px; min-width: 0; display: flex; flex-direction: column; gap: 6px; }
    .notice__head { display: flex; flex-wrap: wrap; align-items: center; gap: 6px 10px; }
    .notice__name { font-weight: 600; }
    .notice__text { margin: 0; font-size: 14px; color: var(--c-ink-2); }
    .notice__actions { display: flex; flex-wrap: wrap; gap: 8px; }
  `,
})
export class PriceNotices implements OnInit {
  private readonly api = inject(ProductsApi);

  protected readonly formatHundredths = formatHundredths;
  protected readonly formatMicros = formatMicros;
  protected readonly perUnit = perUnit;

  protected readonly notices = signal<PriceNoticeResponse[]>([]);
  protected readonly busy = signal<string | null>(null);
  protected readonly error = signal<ApiError | null>(null);

  /** A price changed: the page reloads what depends on it (the list flags it for the sign). */
  readonly priceChanged = output<void>();

  ngOnInit(): void {
    this.load();
  }

  protected priceToday(n: PriceNoticeResponse): string {
    return n.salePriceCents === null ? ' y todavía no tiene precio' : ` y hoy está a ${formatHundredths(n.salePriceCents)}`;
  }

  protected keepText(n: PriceNoticeResponse): string {
    return n.salePriceCents === null ? 'Ahora no' : `Mantener ${formatHundredths(n.salePriceCents)}`;
  }

  protected resolve(n: PriceNoticeResponse, acceptSuggested: boolean): void {
    this.busy.set(n.productId + n.kind);
    this.api.resolvePriceNotice(n.productId, { kind: n.kind, acceptSuggested }).subscribe({
      next: () => {
        this.busy.set(null);
        this.error.set(null);
        if (acceptSuggested) {
          this.priceChanged.emit();
        }
        this.load();
      },
      error: (err: unknown) => {
        this.busy.set(null);
        this.error.set(toApiError(err));
      },
    });
  }

  private load(): void {
    this.api.priceNotices().subscribe({
      next: (response) => this.notices.set(response.items),
      error: (err: unknown) => this.error.set(toApiError(err)),
    });
  }
}
