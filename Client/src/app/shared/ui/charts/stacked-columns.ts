import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { formatMoney } from '../../format/money';
import { SERIES_COLORS, compactNumber, niceScale } from './chart-scale';

/**
 * One column: what was collected (A1) split into what it cost (A2) and what was left (A3).
 * Amounts in CUP cents. A3 can be negative (sold below cost): then the whole column is cost.
 */
export interface StackedColumn {
  key: string;
  /** Under the axis (only some are shown when there are many). */
  label: string;
  /** Header of the tooltip and of the hidden table row. */
  title: string;
  totalCents: number;
  costCents: number;
  incomeCents: number;
  /** One more line for the tooltip ("Faltaron 1,000 CUP"). */
  note?: string;
  noteTone?: 'bad' | 'muted';
}

const MAX_AXIS_LABELS = 10;

/**
 * Hand-made stacked column chart (no chart library). HTML, not SVG, so the text stays crisp at any width.
 * Hover (or focus) a column for its numbers; screen readers get a hidden table.
 */
@Component({
  selector: 'app-stacked-columns',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="legend" aria-hidden="true">
      <span><span class="swatch" [style.background]="colors.cost"></span>{{ costLabel() }}</span>
      <span><span class="swatch" [style.background]="colors.income"></span>{{ incomeLabel() }}</span>
    </div>

    <div class="plot" (mouseleave)="active.set(null)">
      <div class="grid" aria-hidden="true">
        @for (tick of ticks(); track tick.value) {
          <div class="grid__line" [style.bottom.%]="tick.at">
            <span class="grid__label num">{{ tick.label }}</span>
          </div>
        }
      </div>

      <div class="columns" aria-hidden="true">
        @for (column of columns(); track column.key; let i = $index) {
          @let bar = bars()[i];
          <div class="column" [class.column--dim]="active() !== null && active() !== i"
            (mouseenter)="active.set(i)" (click)="active.set(i)">
            <div class="stack" [style.height.%]="bar.height">
              @if (bar.income > 0) {
                <div class="seg seg--top" [style.flex-grow]="bar.income" [style.background]="colors.income"></div>
              }
              @if (bar.cost > 0) {
                <div class="seg" [class.seg--top]="bar.income === 0" [style.flex-grow]="bar.cost"
                  [style.background]="colors.cost"></div>
              }
            </div>
          </div>
        }
      </div>

      @if (active() !== null) {
        @let column = columns()[active()!];
        <div class="tip" role="status" [style.left.%]="tipLeft()">
          <div class="tip__title">{{ column.title }}</div>
          <div class="tip__row"><span>{{ totalLabel() }}</span><span class="num">{{ money(column.totalCents) }}</span></div>
          <div class="tip__row">
            <span><span class="swatch" [style.background]="colors.cost"></span>{{ costLabel() }}</span>
            <span class="num">{{ money(column.costCents) }}</span>
          </div>
          <div class="tip__row">
            <span><span class="swatch" [style.background]="colors.income"></span>{{ incomeLabel() }}</span>
            <span class="num" [class.bad]="column.incomeCents < 0">{{ money(column.incomeCents) }}</span>
          </div>
          @if (column.note) {
            <div class="tip__note" [class.bad]="column.noteTone === 'bad'">{{ column.note }}</div>
          }
        </div>
      }
    </div>

    <div class="axis" aria-hidden="true">
      @for (column of columns(); track column.key; let i = $index) {
        <span class="axis__label">{{ showLabel(i) ? column.label : '' }}</span>
      }
    </div>

    <table class="visually-hidden">
      <caption>{{ caption() }}</caption>
      <thead>
        <tr><th>Período</th><th>{{ totalLabel() }}</th><th>{{ costLabel() }}</th><th>{{ incomeLabel() }}</th></tr>
      </thead>
      <tbody>
        @for (column of columns(); track column.key) {
          <tr>
            <td>{{ column.title }}</td>
            <td>{{ money(column.totalCents) }}</td>
            <td>{{ money(column.costCents) }}</td>
            <td>{{ money(column.incomeCents) }}</td>
          </tr>
        }
      </tbody>
    </table>
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 10px; }
    .legend { display: flex; flex-wrap: wrap; gap: 6px 18px; font-size: 12.5px; color: var(--c-ink-2); }
    .legend > span { display: inline-flex; align-items: center; gap: 6px; }
    .swatch { display: inline-block; flex: none; width: 10px; height: 10px; border-radius: 3px; margin-right: 6px; }
    .legend .swatch { margin-right: 0; }
    .plot { position: relative; height: 190px; margin-left: 52px; }
    .grid { position: absolute; inset: 0; }
    .grid__line { position: absolute; left: 0; right: 0; border-top: 1px solid var(--c-line-soft); }
    .grid__line:first-child { border-top-color: var(--c-line-strong); }
    .grid__label {
      position: absolute; right: calc(100% + 8px); top: -8px;
      font-size: 11.5px; color: var(--c-muted); white-space: nowrap;
    }
    .columns { position: absolute; inset: 0; display: flex; align-items: flex-end; gap: 2px; }
    .column {
      flex: 1 1 0; min-width: 0; height: 100%; display: flex; align-items: flex-end; justify-content: center;
      cursor: default; transition: opacity 0.12s;
    }
    .column--dim { opacity: 0.45; }
    .stack { width: min(100%, 28px); display: flex; flex-direction: column; gap: 2px; min-height: 0; }
    .seg { flex-basis: 0; min-height: 1px; }
    .seg--top { border-radius: 4px 4px 0 0; }
    .axis { display: flex; gap: 2px; margin-left: 52px; }
    .axis__label {
      flex: 1 1 0; min-width: 0; text-align: center; font-size: 11.5px; color: var(--c-muted);
      white-space: nowrap; overflow: visible;
    }
    .tip {
      position: absolute; top: 0; z-index: 2; transform: translateX(-50%); min-width: 190px; pointer-events: none;
      padding: 10px 12px; border: 1px solid var(--c-line); border-radius: var(--r-md, 10px);
      background: var(--c-surface); box-shadow: 0 6px 20px rgba(31, 30, 27, 0.1); font-size: 13px;
    }
    .tip__title { font-weight: 600; margin-bottom: 6px; }
    .tip__row { display: flex; justify-content: space-between; gap: 16px; padding: 2px 0; color: var(--c-ink-2); }
    .tip__row > span:first-child { display: inline-flex; align-items: center; }
    .tip__note { margin-top: 6px; color: var(--c-muted); }
    .bad { color: var(--c-bad-ink); }
    .visually-hidden {
      position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap;
    }
    @media (prefers-reduced-motion: reduce) { .column { transition: none; } }
  `,
})
export class StackedColumns {
  readonly columns = input.required<StackedColumn[]>();
  readonly caption = input('Cobrado, costo e ingreso real');
  readonly totalLabel = input('Cobrado (A1)');
  readonly costLabel = input('Costo (A2)');
  readonly incomeLabel = input('Ingreso real (A3)');

  protected readonly colors = SERIES_COLORS;
  protected readonly active = signal<number | null>(null);

  private readonly scale = computed(() => niceScale(Math.max(0, ...this.columns().map((c) => c.totalCents)) / 100));

  protected readonly ticks = computed(() => {
    const { max, step } = this.scale();
    const ticks: { value: number; at: number; label: string }[] = [];
    for (let value = 0; value <= max + step / 2; value += step) {
      ticks.push({ value, at: (value / max) * 100, label: compactNumber(value) });
    }
    return ticks;
  });

  /** Height of each column (% of the plot) and the weight of each segment inside it. */
  protected readonly bars = computed(() => {
    const max = this.scale().max * 100;
    return this.columns().map((column) => {
      const total = Math.max(0, column.totalCents);
      const income = Math.max(0, Math.min(column.incomeCents, total));
      return { height: (total / max) * 100, cost: total - income, income };
    });
  });

  protected readonly tipLeft = computed(() => {
    const i = this.active();
    const count = this.columns().length;
    if (i === null || count === 0) {
      return 50;
    }
    // Keep the card inside the plot: no closer than 18% to either edge.
    return Math.max(18, Math.min(82, ((i + 0.5) / count) * 100));
  });

  /** One label every few columns, so they never collide. */
  protected showLabel(index: number): boolean {
    return index % Math.ceil(this.columns().length / MAX_AXIS_LABELS) === 0;
  }

  protected readonly money = formatMoney;
}
