import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DailyClosingAllocationResponse, DailyClosingResponse } from '../../core/api/models';
import { formatMoney, formatPercent } from '../../shared/format/money';
import { ENVELOPE_SWATCHES } from '../sobres/envelope-format';
import { differenceText, formatCup } from './closing-format';

interface Instruction {
  key: string;
  text: string;
  amount: string;
  swatch?: string;
}

/**
 * The figures of a closing (or its preview) and what to do with the money, in words: "poné X en el sobre Y"
 * (RF-CIE-07). Presentational: everything comes computed by the server.
 */
@Component({
  selector: 'app-closing-summary',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let c = closing();
    <section class="stats" aria-label="Números del día">
      <div class="stat">
        <span class="stat__label">Venta teórica</span>
        <span class="stat__value num">{{ formatCup(c.theoreticalSalesCents) }}</span>
        <span class="stat__sub">lo vendido a precio del día</span>
      </div>
      <div class="stat">
        <span class="stat__label">Cobrado (A1)</span>
        <span class="stat__value num">{{ formatCup(c.collectedCents) }}</span>
        <span class="stat__sub num">
          {{ formatCup(c.cashCollectedCents) }} efectivo · {{ formatCup(c.transferCollectedCents) }} transferencia
        </span>
      </div>
      <div class="stat" [class.stat--bad]="c.adjustedDifferenceCents < 0" [class.stat--warn]="c.adjustedDifferenceCents > 0">
        <span class="stat__label">Diferencia de caja</span>
        <span class="stat__value num">{{ differenceText(c.adjustedDifferenceCents) }}</span>
        <span class="stat__sub num">
          @if (c.shrinkageToleranceCents > 0) {
            merma aceptada {{ formatCup(c.shrinkageToleranceCents) }} · sin merma {{ formatCup(c.cashDifferenceCents) }}
          } @else {
            umbral para pedir nota: {{ formatCup(c.thresholdCents) }}
          }
        </span>
      </div>
      <div class="stat">
        <span class="stat__label">Costo de lo vendido (A2)</span>
        <span class="stat__value num">{{ formatCup(c.costOfSalesCents) }}</span>
        <span class="stat__sub num">
          @if (bufferCents() !== 0) {
            incluye colchón {{ formatCup(bufferCents()) }} · va a Reposición
          } @else {
            va a Reposición para volver a comprar
          }
        </span>
      </div>
      <div class="stat" [class.stat--bad]="c.realIncomeCents < 0">
        <span class="stat__label">Ingreso real (A3)</span>
        <span class="stat__value num">{{ formatCup(c.realIncomeCents) }}</span>
        <span class="stat__sub">se reparte por los % de los sobres</span>
      </div>
      <div class="stat">
        <span class="stat__label">Pago al dependiente</span>
        <span class="stat__value num">{{ formatCup(c.staffPayCents) }}</span>
        <span class="stat__sub num">{{ formatCup(c.payFixedCents) }} + {{ formatPercent(c.payPercentBps) }} de lo cobrado</span>
      </div>
    </section>

    <section class="card todo" aria-labelledby="todo-title">
      <h2 class="card__title" id="todo-title">Qué hacer con la plata</h2>
      <ol class="todo__list">
        @for (step of instructions(); track step.key) {
          <li class="todo__item">
            <span class="todo__text">
              @if (step.swatch) {
                <span class="swatch" [style.background]="step.swatch"></span>
              }
              {{ step.text }}
            </span>
            <span class="num strong">{{ step.amount }}</span>
          </li>
        }
      </ol>
    </section>

    <section class="table-card" aria-labelledby="split-title">
      <div class="table-card__head">
        <h2 class="section-title" id="split-title">Reparto en los sobres</h2>
      </div>
      <table class="table">
        <thead>
          <tr>
            <th>Sobre</th>
            <th class="end">%</th>
            <th class="end">Le toca</th>
            <th class="end">En efectivo</th>
            <th class="end">Ya en el banco</th>
          </tr>
        </thead>
        <tbody>
          @for (a of c.allocations; track a.envelopeId) {
            <tr>
              <td>
                <span class="envelope"><span class="swatch" [style.background]="swatches[a.role]"></span>{{ a.envelopeName }}</span>
              </td>
              <td class="end num">{{ a.percentBps === null ? 'A2' : formatPercent(a.percentBps) }}</td>
              <td class="end num strong" [class.negative]="a.amountCents < 0">{{ formatCup(a.amountCents) }}</td>
              <td class="end num" [class.negative]="a.cashCents < 0">{{ formatCup(a.cashCents) }}</td>
              <td class="end num">{{ a.bankCents === 0 ? '—' : formatCup(a.bankCents) }}</td>
            </tr>
          }
        </tbody>
      </table>
    </section>

    @if (hasDebt()) {
      <section class="card debt" aria-labelledby="debt-title">
        <h2 class="card__title" id="debt-title">Deuda interna de Reposición</h2>
        <p class="card__lead">
          Cuando entra por transferencia más de lo que Reposición necesita, esa plata queda en el banco y Reposición se
          la debe a los otros sobres. La devuelve con el efectivo que le tocaría en los días siguientes.
        </p>
        <div class="debt__rows num">
          <div><span>Debía antes</span><span>{{ formatCup(c.debtBeforeCents) }}</span></div>
          @if (c.debtCreatedCents > 0) {
            <div><span>Se generó hoy</span><span>+{{ formatCup(c.debtCreatedCents) }}</span></div>
          }
          @if (c.debtRepaidCents > 0) {
            <div><span>Devolvió hoy</span><span>−{{ formatCup(c.debtRepaidCents) }}</span></div>
          }
          <div class="strong"><span>Debe ahora</span><span>{{ formatCup(c.debtAfterCents) }}</span></div>
        </div>
      </section>
    }
  `,
  styleUrls: ['../../shared/ui/list-page.css'],
  styles: `
    :host { gap: 16px; }
    .stats { display: grid; grid-template-columns: repeat(auto-fill, minmax(210px, 1fr)); gap: 12px; }
    .stat {
      display: flex; flex-direction: column; gap: 4px; padding: 16px 18px;
      background: var(--c-surface); border: 1px solid var(--c-line); border-radius: var(--r-lg);
    }
    .stat__label { font-size: 12.5px; color: var(--c-muted); }
    .stat__value { font-size: 20px; font-weight: 600; }
    .stat__sub { font-size: 12.5px; color: var(--c-muted); }
    .stat--bad .stat__value { color: var(--c-bad-ink); }
    .stat--warn .stat__value { color: var(--c-ink-2); }
    .todo { display: flex; flex-direction: column; gap: 10px; }
    .todo__list { list-style: none; margin: 0; padding: 0; counter-reset: step; display: flex; flex-direction: column; gap: 8px; }
    .todo__item { counter-increment: step; display: flex; justify-content: space-between; align-items: baseline; gap: 16px; }
    .todo__item::before { content: counter(step) "."; flex: none; width: 20px; color: var(--c-muted); }
    .todo__text { flex: 1; display: inline-flex; align-items: center; gap: 8px; }
    .envelope { display: inline-flex; align-items: center; gap: 10px; font-weight: 500; }
    .swatch { width: 12px; height: 12px; border-radius: 4px; flex: none; display: inline-block; }
    .debt { display: flex; flex-direction: column; gap: 10px; }
    .debt__rows { display: flex; flex-direction: column; gap: 6px; max-width: 360px; }
    .debt__rows > div { display: flex; justify-content: space-between; gap: 16px; }
  `,
})
export class ClosingSummary {
  readonly closing = input.required<DailyClosingResponse>();

  protected readonly formatCup = formatCup;
  protected readonly formatPercent = formatPercent;
  protected readonly differenceText = differenceText;
  protected readonly swatches = ENVELOPE_SWATCHES;

  /** D-60: the colchón inside A2 (what sold, in USD, × colchón). Reposición keeps it for when the rate goes up. */
  protected readonly bufferCents = computed(() =>
    this.closing().lines.reduce((sum, line) => sum + line.bufferCents, 0),
  );

  protected readonly hasDebt = computed(() => {
    const c = this.closing();
    return c.debtBeforeCents > 0 || c.debtCreatedCents > 0 || c.debtAfterCents > 0;
  });

  /** The physical steps, in order: pay the clerk, fill each envelope, keep Reposición's cash. */
  protected readonly instructions = computed<Instruction[]>(() => {
    const c = this.closing();
    const steps: Instruction[] = [];

    if (c.staffPayCents > 0) {
      steps.push({
        key: 'pay',
        text: `Pagale al dependiente${c.employeeName ? ` (${c.employeeName})` : ''}, del efectivo`,
        amount: formatMoney(c.staffPayCents),
      });
    }

    for (const a of this.others(c.allocations)) {
      if (a.cashCents > 0) {
        steps.push({
          key: a.envelopeId,
          text: `Poné en el sobre ${a.envelopeName}`,
          amount: formatMoney(a.cashCents),
          swatch: this.swatches[a.role],
        });
      } else if (a.cashCents < 0) {
        steps.push({
          key: a.envelopeId,
          text: `Sacá del sobre ${a.envelopeName} para completar los demás`,
          amount: formatMoney(-a.cashCents),
          swatch: this.swatches[a.role],
        });
      }
    }

    const replenishment = c.allocations.find((a) => a.role === 'Replenishment');
    if (replenishment && replenishment.cashCents > 0) {
      steps.push({
        key: 'replenishment',
        text: 'Lo que queda del efectivo va al sobre Reposición',
        amount: formatMoney(replenishment.cashCents),
        swatch: this.swatches.Replenishment,
      });
    }
    if (c.transferCollectedCents > 0) {
      steps.push({
        key: 'bank',
        text: 'Lo cobrado por transferencia ya está en el banco, como Reposición: no hay que hacer nada',
        amount: formatMoney(c.transferCollectedCents),
      });
    }
    if (steps.length === 0) {
      steps.push({ key: 'none', text: 'No se movió plata este día', amount: '' });
    }
    return steps;
  });

  private others(allocations: DailyClosingAllocationResponse[]): DailyClosingAllocationResponse[] {
    return allocations.filter((a) => a.role !== 'Replenishment');
  }
}
