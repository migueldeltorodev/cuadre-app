import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../core/api/api-error';
import { DailyClosingsApi, StaffApi } from '../../core/api/closings-api';
import {
  ClosingCounterLineResponse,
  CreateDailyClosingRequest,
  DailyClosingDraftResponse,
  DailyClosingResponse,
  EmployeeResponse,
} from '../../core/api/models';
import { ExchangeRateDialog, ExchangeRateDialogData } from '../../core/exchange-rate/exchange-rate-dialog';
import { ExchangeRateStore } from '../../core/state/exchange-rate-store';
import { formatDayMonth, formatShortDate } from '../../shared/format/date';
import { formatMoney, formatPercent, parseHundredths } from '../../shared/format/money';
import {
  formatQuantityWithUnit,
  isWhole,
  parseThousandths,
  perUnit,
  toQuantityInput,
} from '../../shared/format/quantity';
import { DIALOG_DEFAULTS } from '../../shared/ui/dialog-config';
import { formatCup } from './closing-format';
import { ClosingSummary } from './closing-summary';

type Step = 1 | 2 | 3 | 4 | 5;

const STEPS: { step: Step; label: string }[] = [
  { step: 1, label: 'Día y tasa' },
  { step: 2, label: 'Movimientos' },
  { step: 3, label: 'Conteo' },
  { step: 4, label: 'Cobro' },
  { step: 5, label: 'Resumen' },
];

function lineKey(line: { productId: string; locationId: string }): string {
  return `${line.productId}|${line.locationId}`;
}

/**
 * Cierre del día (RF-CIE): a guided wizard, one step at a time and without skipping ahead (going back is fine).
 * The browser only collects what the user types; the server computes everything (the preview of step 5 is the
 * same calculation the close saves).
 */
@Component({
  imports: [RouterLink, ClosingSummary],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cierre-page.html',
  styleUrls: ['../../shared/ui/list-page.css', './cierre-page.css'],
})
export default class CierrePage implements OnInit {
  /** ?fecha=2026-11-12 (withComponentInputBinding). Without it: the server's today. */
  readonly fecha = input<string>();

  private readonly api = inject(DailyClosingsApi);
  private readonly staffApi = inject(StaffApi);
  private readonly dialog = inject(Dialog);
  private readonly router = inject(Router);
  private readonly rate = inject(ExchangeRateStore);

  protected readonly steps = STEPS;
  protected readonly formatShortDate = formatShortDate;
  protected readonly formatDayMonth = formatDayMonth;
  protected readonly formatMoney = formatMoney;
  protected readonly formatPercent = formatPercent;
  protected readonly formatCup = formatCup;
  protected readonly formatQuantityWithUnit = formatQuantityWithUnit;
  protected readonly perUnit = perUnit;
  protected readonly lineKey = lineKey;

  protected readonly step = signal<Step>(1);
  protected readonly draft = signal<DailyClosingDraftResponse | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly employees = signal<EmployeeResponse[]>([]);

  protected readonly counts = signal<Record<string, string>>({});
  protected readonly collections = signal<Record<string, string>>({});
  protected readonly employeeId = signal('');
  protected readonly notes = signal('');

  protected readonly countSubmitted = signal(false);
  protected readonly collectSubmitted = signal(false);
  protected readonly confirmSubmitted = signal(false);

  protected readonly preview = signal<DailyClosingResponse | null>(null);
  protected readonly previewError = signal<ApiError | null>(null);
  protected readonly previewing = signal(false);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<ApiError | null>(null);

  /** Lines that moved today (transfers in, exits...): what step 2 shows. */
  protected readonly movedLines = computed(() =>
    (this.draft()?.lines ?? []).filter((line) => line.inMilli !== 0 || line.outMilli !== 0),
  );

  /** Keys whose count is missing or badly written (whole units only where the product asks for it). */
  protected readonly invalidCounts = computed(() => {
    const invalid = new Set<string>();
    for (const line of this.draft()?.lines ?? []) {
      if (this.countedMilli(line) === null) {
        invalid.add(lineKey(line));
      }
    }
    return invalid;
  });

  protected readonly pendingCounts = computed(
    () => (this.draft()?.lines ?? []).filter((line) => (this.counts()[lineKey(line)] ?? '').trim() === '').length,
  );

  protected readonly unpricedSold = computed(() =>
    (this.draft()?.lines ?? []).filter((line) => {
      const counted = this.countedMilli(line);
      return line.unitPriceCents === null && counted !== null && counted !== line.expectedMilli;
    }),
  );

  /** Payment methods whose text is not a valid amount (blank = 0). */
  protected readonly invalidCollections = computed(() => {
    const invalid = new Set<string>();
    for (const [id, text] of Object.entries(this.collections())) {
      if (text.trim() !== '' && parseHundredths(text) === null) {
        invalid.add(id);
      }
    }
    return invalid;
  });

  /** For reading only: the server adds it up again (A1). */
  protected readonly typedTotal = computed(() =>
    Object.values(this.collections()).reduce((sum, text) => sum + (parseHundredths(text) ?? 0), 0),
  );

  protected readonly needsNote = computed(() => !!this.preview()?.requiresNote && this.notes().trim() === '');

  ngOnInit(): void {
    this.loadDraft(this.fecha());
    this.staffApi.employees().subscribe({
      next: (result) => this.employees.set(result.items.filter((employee) => employee.isActive)),
    });
  }

  // ---------- Navigation ----------

  protected goTo(step: Step): void {
    if (step < this.step()) {
      this.step.set(step);
    }
  }

  protected next(): void {
    switch (this.step()) {
      case 1:
        if (this.draft()?.canClose) {
          this.step.set(2);
        }
        break;
      case 2:
        this.step.set(3);
        break;
      case 3:
        this.countSubmitted.set(true);
        if (this.invalidCounts().size === 0 && this.unpricedSold().length === 0) {
          this.step.set(4);
        }
        break;
      case 4:
        this.collectSubmitted.set(true);
        if (this.invalidCollections().size === 0) {
          this.step.set(5);
          this.loadPreview();
        }
        break;
    }
  }

  protected back(): void {
    if (this.step() > 1) {
      this.step.set((this.step() - 1) as Step);
    }
  }

  // ---------- Step 1: day and rate ----------

  /** Another day: everything typed was for the other day, so it starts over. */
  protected setDate(date: string): void {
    if (!date || date === this.draft()?.date) {
      return;
    }
    this.counts.set({});
    this.collections.set({});
    this.notes.set('');
    this.preview.set(null);
    this.countSubmitted.set(false);
    this.collectSubmitted.set(false);
    this.loadDraft(date);
  }

  protected openRateDialog(): void {
    const draft = this.draft();
    if (!draft) {
      return;
    }
    const current = this.rate.current();
    this.dialog
      .open<unknown, ExchangeRateDialogData>(ExchangeRateDialog, {
        ...DIALOG_DEFAULTS,
        data: { date: draft.date, today: current?.today ?? draft.date, reference: current?.latest ?? null },
      })
      .closed.subscribe((saved) => saved && this.loadDraft(draft.date));
  }

  /** Step 2: after registering a transfer or an exit in another tab, read the counter again. */
  protected refresh(): void {
    const draft = this.draft();
    if (draft) {
      this.loadDraft(draft.date);
    }
  }

  // ---------- Step 3: count ----------

  protected countText(line: ClosingCounterLineResponse): string {
    return this.counts()[lineKey(line)] ?? '';
  }

  protected setCount(line: ClosingCounterLineResponse, text: string): void {
    this.counts.update((counts) => ({ ...counts, [lineKey(line)]: text }));
  }

  /** "Lo que no tocaste": blanks get what should be there (nothing sold). Typed ones are kept. */
  protected fillBlanksWithExpected(): void {
    this.counts.update((counts) => {
      const next = { ...counts };
      for (const line of this.draft()?.lines ?? []) {
        const key = lineKey(line);
        if ((next[key] ?? '').trim() === '') {
          next[key] = toQuantityInput(Math.max(0, line.expectedMilli));
        }
      }
      return next;
    });
  }

  protected countedMilli(line: ClosingCounterLineResponse): number | null {
    const milli = parseThousandths(this.counts()[lineKey(line)]);
    if (milli === null || (!line.allowsDecimals && !isWhole(milli))) {
      return null;
    }
    return milli;
  }

  /** For reading only (the server recomputes it): expected − counted. */
  protected soldMilli(line: ClosingCounterLineResponse): number | null {
    const counted = this.countedMilli(line);
    return counted === null ? null : line.expectedMilli - counted;
  }

  protected showCountError(line: ClosingCounterLineResponse): boolean {
    const typed = this.countText(line).trim() !== '';
    return (typed || this.countSubmitted()) && this.invalidCounts().has(lineKey(line));
  }

  // ---------- Step 4: collections ----------

  protected collectionText(methodId: string): string {
    return this.collections()[methodId] ?? '';
  }

  protected setCollection(methodId: string, text: string): void {
    this.collections.update((collections) => ({ ...collections, [methodId]: text }));
  }

  protected showCollectionError(methodId: string): boolean {
    return this.invalidCollections().has(methodId) && (this.collectSubmitted() || this.collectionText(methodId) !== '');
  }

  // ---------- Step 5: summary and close ----------

  protected confirm(): void {
    this.confirmSubmitted.set(true);
    if (!this.preview() || this.needsNote()) {
      return;
    }
    this.saving.set(true);
    this.saveError.set(null);
    this.api.create(this.request()).subscribe({
      next: (closing) => void this.router.navigate(['/cierre', closing.id], { state: { justClosed: true } }),
      error: (err: unknown) => {
        this.saveError.set(toApiError(err));
        this.saving.set(false);
      },
    });
  }

  protected fieldMessages(error: ApiError): string[] {
    return Object.values(error.fields);
  }

  private loadPreview(): void {
    this.preview.set(null);
    this.previewError.set(null);
    this.saveError.set(null);
    this.confirmSubmitted.set(false);
    this.previewing.set(true);
    this.api.preview(this.request()).subscribe({
      next: (preview) => {
        this.preview.set(preview);
        this.previewing.set(false);
      },
      error: (err: unknown) => {
        this.previewError.set(toApiError(err));
        this.previewing.set(false);
      },
    });
  }

  private request(): CreateDailyClosingRequest {
    const draft = this.draft()!;
    return {
      date: draft.date,
      employeeId: this.employeeId() || null,
      notes: this.notes().trim() || null,
      counts: draft.lines.map((line) => ({
        productId: line.productId,
        locationId: line.locationId,
        countedMilli: this.countedMilli(line) ?? 0,
      })),
      collections: draft.paymentMethods
        .map((method) => ({ paymentMethodId: method.id, amountCents: parseHundredths(this.collectionText(method.id)) ?? 0 }))
        .filter((collection) => collection.amountCents > 0),
    };
  }

  private loadDraft(date?: string): void {
    this.api.draft(date).subscribe({
      next: (draft) => {
        this.draft.set(draft);
        this.loadError.set(null);
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }
}
