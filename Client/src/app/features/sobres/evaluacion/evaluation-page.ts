import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { ProfitFirstApi } from '../../../core/api/closings-api';
import { EnvelopeEvaluationResponse, ProfitFirstEvaluationResponse } from '../../../core/api/models';
import { formatShortDate } from '../../../shared/format/date';
import { formatMoney, formatPercent } from '../../../shared/format/money';
import { ENVELOPE_SWATCHES, addDays, firstOfMonth } from '../envelope-format';

/** Real − target, in points, to read at a glance. Below half a point counts as "on target". */
const ON_TARGET_BPS = 50;

/**
 * RF-PF-10: how the money of the closed days of a period was split, against the plan. The server sums everything;
 * this page only compares and draws.
 */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './evaluation-page.html',
  styleUrls: ['../../../shared/ui/list-page.css', './evaluation-page.css'],
})
export default class EvaluationPage implements OnInit {
  private readonly api = inject(ProfitFirstApi);

  protected readonly formatMoney = formatMoney;
  protected readonly formatPercent = formatPercent;
  protected readonly formatShortDate = formatShortDate;
  protected readonly swatches = ENVELOPE_SWATCHES;

  protected readonly evaluation = signal<ProfitFirstEvaluationResponse | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly loading = signal(false);

  /** The server's today, learned from the first answer (it defaults "to" to today). */
  private readonly today = signal<string | null>(null);

  protected readonly presets = computed(() => {
    const today = this.today();
    if (!today) {
      return [];
    }
    const thisMonth = firstOfMonth(today);
    const lastMonthEnd = addDays(thisMonth, -1);
    return [
      { label: 'Este mes', from: thisMonth, to: today },
      { label: 'Mes pasado', from: firstOfMonth(lastMonthEnd), to: lastMonthEnd },
      { label: 'Últimos 30 días', from: addDays(today, -29), to: today },
    ];
  });

  /** The envelopes with a %: the ones the plan is about. */
  protected readonly percentEnvelopes = computed(
    () => this.evaluation()?.envelopes.filter((e) => e.role !== 'Replenishment') ?? [],
  );

  protected readonly replenishment = computed(
    () => this.evaluation()?.envelopes.find((e) => e.role === 'Replenishment') ?? null,
  );

  ngOnInit(): void {
    this.load();
  }

  protected isPreset(from: string, to: string): boolean {
    const e = this.evaluation();
    return !!e && e.from === from && e.to === to;
  }

  protected setFrom(value: string): void {
    if (value) {
      this.load(value, this.evaluation()?.to);
    }
  }

  protected setTo(value: string): void {
    if (value) {
      this.load(this.evaluation()?.from, value);
    }
  }

  protected load(from?: string, to?: string): void {
    this.loading.set(true);
    this.api.evaluation(from, to).subscribe({
      next: (evaluation) => {
        if (!this.today() && !from && !to) {
          this.today.set(evaluation.to);
        }
        this.evaluation.set(evaluation);
        this.loadError.set(null);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loadError.set(toApiError(err));
        this.loading.set(false);
      },
    });
  }

  /** Real − target in points: "+2.5 pts" / "−1 pt" / "en el objetivo". */
  protected deviation(envelope: EnvelopeEvaluationResponse): { text: string; tone: 'ok' | 'warn' | 'muted' } {
    if (envelope.realPercentBps === null || envelope.targetPercentBps === null) {
      return { text: '—', tone: 'muted' };
    }
    const diff = envelope.realPercentBps - envelope.targetPercentBps;
    if (Math.abs(diff) < ON_TARGET_BPS) {
      return { text: 'En el objetivo', tone: 'ok' };
    }
    const sign = diff > 0 ? '+' : '−';
    return { text: `${sign}${formatPercent(Math.abs(diff)).replace('%', '')} pts`, tone: 'warn' };
  }

  /** Width of a bar, 0–100%, for a share in basis points. */
  protected width(bps: number | null): string {
    return `${Math.max(0, Math.min(100, (bps ?? 0) / 100))}%`;
  }
}
