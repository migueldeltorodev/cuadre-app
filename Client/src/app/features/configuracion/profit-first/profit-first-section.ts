import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { AllocationPlansApi, EnvelopesApi } from '../../../core/api/envelopes-api';
import { AllocationPlanResponse, EnvelopeResponse } from '../../../core/api/models';
import { formatShortDate } from '../../../shared/format/date';
import { formatHundredths, formatPercent, parseHundredths, toInputText } from '../../../shared/format/money';
import { DIALOG_DEFAULTS } from '../../../shared/ui/dialog-config';
import { localToday } from '../../compras/purchase-format';
import { ENVELOPE_NOTES, ENVELOPE_SWATCHES, addDays, planStartLabel } from '../../sobres/envelope-format';
import { createSaveFeedback } from '../save-feedback';
import { EnvelopeDialog, EnvelopeDialogData } from './envelope-dialog';

const FULL_BPS = 10_000;
const STEP_BPS = 100;
/** "De cada 1,000 CUP": an example, there is no A3 until the daily closing exists. */
const EXAMPLE_CENTS = 100_000;

/**
 * Configuración › Profit First (RF-PF-01/02/03): how the real income (A3) is split, with a start date.
 * Past days keep the % they had: a new plan never rewrites history (D-20).
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './profit-first-section.html',
  styleUrl: './profit-first-section.css',
})
export default class ProfitFirstSection implements OnInit {
  private readonly dialog = inject(Dialog);
  private readonly envelopesApi = inject(EnvelopesApi);
  private readonly plansApi = inject(AllocationPlansApi);

  protected readonly feedback = createSaveFeedback();
  protected readonly formatShortDate = formatShortDate;
  protected readonly formatPercent = formatPercent;
  protected readonly planStartLabel = planStartLabel;
  protected readonly swatches = ENVELOPE_SWATCHES;
  protected readonly notes = ENVELOPE_NOTES;

  protected readonly today = localToday();
  protected readonly tomorrow = addDays(this.today, 1);

  protected readonly envelopes = signal<EnvelopeResponse[] | null>(null);
  protected readonly plans = signal<AllocationPlanResponse[]>([]);
  protected readonly loadError = signal<ApiError | null>(null);

  /** % typed per envelope, as text ("50", "12.5"). */
  protected readonly texts = signal<Record<string, string>>({});
  protected readonly effectiveFrom = signal(this.tomorrow);
  private readonly submitted = signal(false);

  protected readonly withPercent = computed(() =>
    (this.envelopes() ?? []).filter((envelope) => envelope.isActive && envelope.takesPercent),
  );
  protected readonly replenishment = computed(() =>
    (this.envelopes() ?? []).find((envelope) => envelope.isActive && !envelope.takesPercent) ?? null,
  );
  protected readonly inactive = computed(() => (this.envelopes() ?? []).filter((envelope) => !envelope.isActive));

  /** Newest first (as the server sends them). */
  protected readonly latestPlan = computed(() => this.plans()[0] ?? null);

  /** The plan that applies today: the newest one already started. */
  protected readonly currentPlanId = computed(
    () => this.plans().find((plan) => plan.effectiveFrom <= this.today)?.id ?? null,
  );

  protected readonly totalBps = computed(() =>
    this.withPercent().reduce((total, envelope) => total + (this.bps(envelope.id) ?? 0), 0),
  );

  protected readonly invalidIds = computed(
    () => new Set(this.withPercent().filter((envelope) => this.bps(envelope.id) === null).map((e) => e.id)),
  );

  protected readonly isBalanced = computed(() => this.totalBps() === FULL_BPS && this.invalidIds().size === 0);

  /** An earlier start than the latest plan is refused by the server; the same day replaces it. */
  protected readonly dateProblem = computed(() => {
    const latest = this.latestPlan();
    const date = this.effectiveFrom();
    if (!date) {
      return 'Elegí desde cuándo vale.';
    }
    if (latest && date < latest.effectiveFrom) {
      return `Tiene que ser desde el ${formatShortDate(latest.effectiveFrom)} en adelante: hay un reparto que empieza ese día.`;
    }
    return null;
  });

  protected readonly replacesLatest = computed(() => this.latestPlan()?.effectiveFrom === this.effectiveFrom());

  protected readonly saveLabel = computed(() => {
    if (this.feedback.saving()) {
      return 'Guardando…';
    }
    const date = this.effectiveFrom();
    if (date === this.tomorrow) {
      return 'Guardar desde mañana';
    }
    if (date === this.today) {
      return 'Guardar desde hoy';
    }
    return date ? `Guardar desde el ${formatShortDate(date)}` : 'Guardar';
  });

  ngOnInit(): void {
    this.load();
  }

  protected bps(envelopeId: string): number | null {
    const value = parseHundredths(this.texts()[envelopeId]);
    return value === null || value > FULL_BPS ? null : value;
  }

  protected example(envelopeId: string): string {
    const bps = this.bps(envelopeId);
    return bps === null ? '—' : formatHundredths(Math.round((EXAMPLE_CENTS * bps) / FULL_BPS));
  }

  protected setText(envelopeId: string, text: string): void {
    this.texts.update((texts) => ({ ...texts, [envelopeId]: text }));
  }

  protected step(envelopeId: string, delta: number): void {
    const current = this.bps(envelopeId) ?? 0;
    const next = Math.min(FULL_BPS, Math.max(0, current + delta * STEP_BPS));
    this.setText(envelopeId, toInputText(next));
  }

  protected totalText(): string {
    const total = this.totalBps();
    if (total === FULL_BPS) {
      return 'Suma 100%';
    }
    const diff = FULL_BPS - total;
    return diff > 0 ? `Suma ${formatPercent(total)} · faltan ${formatPercent(diff)}` : `Suma ${formatPercent(total)} · sobran ${formatPercent(-diff)}`;
  }

  protected showInvalid(envelopeId: string): boolean {
    return this.invalidIds().has(envelopeId);
  }

  protected savePlan(): void {
    this.submitted.set(true);
    if (!this.isBalanced() || this.dateProblem()) {
      return;
    }
    this.feedback.start();
    this.plansApi
      .create({
        effectiveFrom: this.effectiveFrom(),
        items: this.withPercent().map((envelope) => ({ envelopeId: envelope.id, percentBps: this.bps(envelope.id)! })),
      })
      .subscribe({
        next: () => {
          this.feedback.succeed();
          this.load();
        },
        error: (err: unknown) => this.feedback.fail(toApiError(err)),
      });
  }

  protected showUnbalanced(): boolean {
    return this.submitted() && !this.isBalanced();
  }

  protected openEnvelope(envelope?: EnvelopeResponse): void {
    this.dialog
      .open<EnvelopeResponse, EnvelopeDialogData>(EnvelopeDialog, { ...DIALOG_DEFAULTS, data: { envelope } })
      .closed.subscribe((saved) => saved && this.load(true));
  }

  /** "Ganancia 5% · Compensación del dueño 50% · …" */
  protected planSummary(plan: AllocationPlanResponse): string {
    return plan.items.map((item) => `${item.envelopeName} ${formatPercent(item.percentBps)}`).join(' · ');
  }

  /**
   * Reload envelopes and plans. The % being typed are kept when only an envelope changed
   * (keepTyped): a new envelope starts at 0.
   */
  private load(keepTyped = false): void {
    forkJoin({ envelopes: this.envelopesApi.getAll(), plans: this.plansApi.getAll() }).subscribe({
      next: ({ envelopes, plans }) => {
        const latest = plans.items[0];
        const texts: Record<string, string> = {};
        for (const envelope of envelopes.items) {
          const typed = keepTyped ? this.texts()[envelope.id] : undefined;
          const planned = latest?.items.find((item) => item.envelopeId === envelope.id)?.percentBps ?? 0;
          texts[envelope.id] = typed ?? toInputText(planned);
        }
        this.envelopes.set(envelopes.items);
        this.plans.set(plans.items);
        this.texts.set(texts);
        if (!keepTyped) {
          this.submitted.set(false);
          if (latest && this.effectiveFrom() < latest.effectiveFrom) {
            this.effectiveFrom.set(latest.effectiveFrom > this.tomorrow ? latest.effectiveFrom : this.tomorrow);
          }
        }
        this.loadError.set(null);
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }
}
