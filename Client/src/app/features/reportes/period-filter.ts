import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { addDays, firstOfMonth } from '../sobres/envelope-format';

export interface Period {
  from: string;
  to: string;
}

/**
 * Quick periods + from/to. It does not keep state: the page owns the period (what the server answered) and gets
 * a new one through `periodChange`. `today` is the server's, learned from a default answer.
 */
@Component({
  selector: 'app-period-filter',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dates">
      <label class="field">
        <span class="field__label">Desde</span>
        <input class="input num" type="date" [value]="from()" (change)="setFrom($any($event.target).value)" />
      </label>
      <label class="field">
        <span class="field__label">Hasta</span>
        <input class="input num" type="date" [value]="to()" (change)="setTo($any($event.target).value)" />
      </label>
    </div>
    <div class="presets" role="group" aria-label="Períodos rápidos">
      @for (p of presets(); track p.label) {
        <button class="preset" type="button" [class.preset--on]="p.from === from() && p.to === to()"
          (click)="periodChange.emit({ from: p.from, to: p.to })">
          {{ p.label }}
        </button>
      }
    </div>
  `,
  styles: `
    /* The dates side by side right under the page header, the quick periods under them. */
    :host { display: flex; flex-direction: column; align-items: flex-start; gap: 12px; }
    .presets { display: flex; flex-wrap: wrap; gap: 8px; }
    .preset {
      min-height: 36px; padding: 0 14px; border: 1px solid var(--c-line); border-radius: var(--r-pill);
      background: var(--c-surface); color: var(--c-ink-2); font: inherit; font-size: 13.5px; cursor: pointer;
    }
    .preset:hover { color: var(--c-ink); border-color: var(--c-line-strong); }
    .preset--on { background: var(--c-ink); border-color: var(--c-ink); color: #ffffff; }
    .preset:focus-visible { outline: 2px solid var(--c-accent); outline-offset: 1px; }
    .dates { display: flex; flex-wrap: wrap; gap: 12px; }
    /* A fixed width, never squeezed: they only go one under the other on a narrow phone. */
    .dates .field { flex: 0 0 170px; }
  `,
})
export class PeriodFilter {
  readonly from = input.required<string>();
  readonly to = input.required<string>();
  readonly today = input<string | null>(null);
  /** Add "Últimos 12 meses" (cost reports look further back). */
  readonly longPresets = input(false);

  /** Not "change": the native change of the inner date inputs bubbles up to the host. */
  readonly periodChange = output<Period>();

  protected readonly presets = computed(() => {
    const today = this.today();
    if (!today) {
      return [];
    }
    const thisMonth = firstOfMonth(today);
    const lastMonthEnd = addDays(thisMonth, -1);
    const presets = [
      { label: 'Este mes', from: thisMonth, to: today },
      { label: 'Mes pasado', from: firstOfMonth(lastMonthEnd), to: lastMonthEnd },
      { label: 'Últimos 30 días', from: addDays(today, -29), to: today },
      { label: 'Este año', from: `${today.slice(0, 4)}-01-01`, to: today },
    ];
    if (this.longPresets()) {
      const yearAgo = `${Number(today.slice(0, 4)) - 1}${today.slice(4)}`;
      presets.push({ label: 'Últimos 12 meses', from: addDays(yearAgo, 1), to: today });
    }
    return presets;
  });

  protected setFrom(value: string): void {
    if (value) {
      this.periodChange.emit({ from: value, to: this.to() });
    }
  }

  protected setTo(value: string): void {
    if (value) {
      this.periodChange.emit({ from: this.from(), to: value });
    }
  }
}
