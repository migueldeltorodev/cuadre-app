import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { formatShortDate } from '../../format/date';
import { formatMicros } from '../../format/money';
import { SERIES_COLORS, compactNumber, niceScale } from './chart-scale';

/** A purchase: its landed cost per stock unit. */
export interface CostPoint {
  key: string;
  date: string;
  cupMicros: number;
  usdMicros: number;
  supplierName: string;
  label: string;
}

/** The sale price from that date on (cents per stock unit). */
export interface PricePoint {
  date: string;
  cents: number;
}

const HEIGHT = 240;
const PAD = { top: 16, right: 16, bottom: 28, left: 56 };
const DAY_MS = 86_400_000;

function dayNumber(date: string): number {
  const [y, m, d] = date.split('-').map(Number);
  return Date.UTC(y, m - 1, d) / DAY_MS;
}

/**
 * RF-REP-02: the cost of each purchase (dots joined by a line) against the sale price (a step line), both in CUP
 * per stock unit, so they share ONE axis and the gap between them is the margin. Hand-made SVG measured to its
 * real width, so text never scales. Hover snaps to the nearest purchase.
 */
@Component({
  selector: 'app-cost-price-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="legend" aria-hidden="true">
      <span><span class="line" [style.background]="colors.cost"></span>Costo puesto (por compra)</span>
      <span><span class="line" [style.background]="colors.income"></span>Precio de venta</span>
    </div>

    <div class="frame" (mouseleave)="active.set(null)">
      @if (width() > 0 && geometry(); as g) {
        <svg [attr.width]="width()" [attr.height]="height" role="img" [attr.aria-label]="ariaLabel()"
          (mousemove)="hover($event)">
          @for (tick of g.ticks; track tick.value) {
            <line class="grid" [attr.x1]="pad.left" [attr.x2]="width() - pad.right" [attr.y1]="tick.y" [attr.y2]="tick.y"
              [class.grid--base]="tick.value === 0" />
            <text class="tick" [attr.x]="pad.left - 8" [attr.y]="tick.y + 4" text-anchor="end">{{ tick.label }}</text>
          }
          @for (tick of g.dateTicks; track tick.x) {
            <text class="tick" [attr.x]="tick.x" [attr.y]="height - 8" text-anchor="middle">{{ tick.label }}</text>
          }

          @if (g.pricePath) {
            <path class="series" [attr.d]="g.pricePath" [attr.stroke]="colors.income" />
          }
          @if (g.costPath) {
            <path class="series" [attr.d]="g.costPath" [attr.stroke]="colors.cost" />
          }

          @if (active(); as a) {
            <line class="cross" [attr.x1]="a.x" [attr.x2]="a.x" [attr.y1]="pad.top" [attr.y2]="height - pad.bottom" />
          }
          @for (dot of g.dots; track dot.key) {
            <circle [attr.cx]="dot.x" [attr.cy]="dot.y" [attr.r]="active()?.key === dot.key ? 6 : 4"
              [attr.fill]="colors.cost" class="dot" />
          }
        </svg>
      }

      @if (active(); as a) {
        <div class="tip" role="status" [style.left.px]="tipLeft(a.x)">
          <div class="tip__title">{{ formatShortDate(a.point.date) }} · {{ a.point.label }}</div>
          <div class="tip__sub">{{ a.point.supplierName }}</div>
          <div class="tip__row">
            <span>Costo puesto</span><span class="num">{{ formatMicros(a.point.cupMicros) }} CUP</span>
          </div>
          <div class="tip__row">
            <span>En USD</span><span class="num">{{ formatMicros(a.point.usdMicros) }} USD</span>
          </div>
          @if (a.priceCents !== null) {
            <div class="tip__row">
              <span>Precio ese día</span><span class="num">{{ formatMicros(a.priceCents * 10000) }} CUP</span>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 10px; }
    .legend { display: flex; flex-wrap: wrap; gap: 6px 18px; font-size: 12.5px; color: var(--c-ink-2); }
    .legend > span { display: inline-flex; align-items: center; gap: 8px; }
    .line { display: inline-block; width: 18px; height: 2px; border-radius: 1px; }
    .frame { position: relative; min-height: 240px; }
    svg { display: block; overflow: visible; }
    .grid { stroke: var(--c-line-soft); stroke-width: 1; }
    .grid--base { stroke: var(--c-line-strong); }
    .tick { font-size: 11.5px; fill: var(--c-muted); font-variant-numeric: tabular-nums; }
    .series { fill: none; stroke-width: 2; stroke-linejoin: round; stroke-linecap: round; }
    .dot { stroke: var(--c-surface); stroke-width: 2; }
    .cross { stroke: var(--c-line-strong); stroke-width: 1; stroke-dasharray: 3 3; }
    .tip {
      position: absolute; top: 0; z-index: 2; transform: translateX(-50%); min-width: 200px; pointer-events: none;
      padding: 10px 12px; border: 1px solid var(--c-line); border-radius: 10px;
      background: var(--c-surface); box-shadow: 0 6px 20px rgba(31, 30, 27, 0.1); font-size: 13px;
    }
    .tip__title { font-weight: 600; }
    .tip__sub { margin-bottom: 6px; color: var(--c-muted); }
    .tip__row { display: flex; justify-content: space-between; gap: 16px; padding: 2px 0; color: var(--c-ink-2); }
  `,
})
export class CostPriceChart {
  readonly costs = input.required<CostPoint[]>();
  /** Oldest first. */
  readonly prices = input<PricePoint[]>([]);
  /** Where the price line ends (default: the last date shown). */
  readonly until = input<string | null>(null);

  protected readonly colors = SERIES_COLORS;
  protected readonly pad = PAD;
  protected readonly height = HEIGHT;
  protected readonly formatMicros = formatMicros;
  protected readonly formatShortDate = formatShortDate;

  protected readonly width = signal(0);
  protected readonly active = signal<{ key: string; x: number; point: CostPoint; priceCents: number | null } | null>(
    null,
  );

  constructor() {
    const host = inject(ElementRef<HTMLElement>);
    const destroyRef = inject(DestroyRef);
    afterNextRender(() => {
      const observer = new ResizeObserver((entries) => this.width.set(Math.floor(entries[0].contentRect.width)));
      observer.observe(host.nativeElement);
      destroyRef.onDestroy(() => observer.disconnect());
    });
  }

  protected readonly ariaLabel = computed(() => {
    const costs = this.costs();
    if (costs.length === 0) {
      return 'Sin compras en el período';
    }
    const first = costs[0];
    const last = costs[costs.length - 1];
    return (
      `Costo puesto de ${costs.length} compras: de ${formatMicros(first.cupMicros)} CUP el ${formatShortDate(first.date)} ` +
      `a ${formatMicros(last.cupMicros)} CUP el ${formatShortDate(last.date)}.`
    );
  });

  /** Everything the SVG draws, in pixels. Values in CUP (display only). */
  protected readonly geometry = computed(() => {
    const width = this.width();
    const costs = this.costs();
    const prices = this.prices();
    const dates = [...costs.map((c) => c.date), ...prices.map((p) => p.date)];
    const until = this.until();
    if (until) {
      dates.push(until);
    }
    if (dates.length === 0 || width <= PAD.left + PAD.right) {
      return null;
    }

    let start = Math.min(...dates.map(dayNumber));
    let end = Math.max(...dates.map(dayNumber));
    if (end - start < 14) {
      start -= 7;
      end += 7;
    }
    const plotWidth = width - PAD.left - PAD.right;
    const plotHeight = HEIGHT - PAD.top - PAD.bottom;
    const x = (date: string) => PAD.left + ((dayNumber(date) - start) / (end - start)) * plotWidth;

    const maxValue = Math.max(0, ...costs.map((c) => c.cupMicros / 1_000_000), ...prices.map((p) => p.cents / 100));
    const { max, step } = niceScale(maxValue);
    const y = (value: number) => PAD.top + plotHeight - (value / max) * plotHeight;

    const ticks = [];
    for (let value = 0; value <= max + step / 2; value += step) {
      ticks.push({ value, y: y(value), label: compactNumber(value) });
    }

    const dots = costs.map((c) => ({ key: c.key, x: x(c.date), y: y(c.cupMicros / 1_000_000) }));
    const costPath = dots.length > 1 ? dots.map((d, i) => `${i === 0 ? 'M' : 'L'}${d.x},${d.y}`).join(' ') : null;

    // Step line: each price holds until the next change, the last one up to the right edge.
    let pricePath: string | null = null;
    if (prices.length > 0) {
      const right = PAD.left + plotWidth;
      const parts: string[] = [];
      prices.forEach((p, i) => {
        const px = Math.max(PAD.left, x(p.date));
        const py = y(p.cents / 100);
        parts.push(i === 0 ? `M${px},${py}` : `V${py}`);
        const next = prices[i + 1];
        parts.push(`H${next ? Math.max(PAD.left, x(next.date)) : right}`);
      });
      pricePath = parts.join(' ');
    }

    // About 5 dates under the axis, evenly spread.
    const dateTicks = [];
    const count = Math.max(2, Math.min(5, Math.floor(plotWidth / 120)));
    for (let i = 0; i < count; i++) {
      const day = start + ((end - start) * i) / (count - 1);
      const date = new Date(Math.round(day) * DAY_MS);
      dateTicks.push({
        x: PAD.left + (plotWidth * i) / (count - 1),
        label: new Intl.DateTimeFormat('es', { day: 'numeric', month: 'short', timeZone: 'UTC' })
          .format(date)
          .replace('.', ''),
      });
    }

    return { ticks, dots, costPath, pricePath, dateTicks };
  });

  protected hover(event: MouseEvent): void {
    const g = this.geometry();
    if (!g || g.dots.length === 0) {
      return;
    }
    const svg = event.currentTarget as SVGSVGElement;
    const mouseX = event.clientX - svg.getBoundingClientRect().left;
    let nearest = 0;
    g.dots.forEach((dot, i) => {
      if (Math.abs(dot.x - mouseX) < Math.abs(g.dots[nearest].x - mouseX)) {
        nearest = i;
      }
    });
    const point = this.costs()[nearest];
    this.active.set({ key: point.key, x: g.dots[nearest].x, point, priceCents: this.priceOn(point.date) });
  }

  /** The price in force that day: the last change on or before it. */
  private priceOn(date: string): number | null {
    let price: number | null = null;
    for (const p of this.prices()) {
      if (p.date <= date) {
        price = p.cents;
      }
    }
    return price;
  }

  protected tipLeft(x: number): number {
    return Math.max(110, Math.min(this.width() - 110, x));
  }
}
