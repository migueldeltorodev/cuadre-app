import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { EnvelopeOperationsApi, EnvelopesApi } from '../../../core/api/envelopes-api';
import { EnvelopeOverviewResponse } from '../../../core/api/models';
import { formatShortDate } from '../../../shared/format/date';
import { formatHundredths, parseHundredths, toInputText } from '../../../shared/format/money';
import { ENVELOPE_SWATCHES, formatBalance } from '../envelope-format';

interface Cell {
  /** What the envelope has in that box at the end of the chosen day. */
  balanceCents: number;
  /** Empty = leave it as it is (a negative balance starts empty: it cannot be typed). */
  text: string;
}

/** The cell's new target, or null when it is unchanged or empty. */
function targetOf(cell: Cell): number | null {
  const target = parseHundredths(cell.text);
  return target === null || target === cell.balanceCents ? null : target;
}

/**
 * Saldo inicial (RF-PF-04, D-21): the user writes what is REALLY in each box for each envelope, and the server
 * moves the difference (target − what the ledger already has that day). Only changed cells are sent.
 */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './opening-page.html',
  styleUrls: ['../../../shared/ui/list-page.css', './opening-page.css'],
})
export default class OpeningPage implements OnInit {
  private readonly envelopesApi = inject(EnvelopesApi);
  private readonly operationsApi = inject(EnvelopeOperationsApi);
  private readonly router = inject(Router);

  protected readonly formatShortDate = formatShortDate;
  protected readonly formatBalance = formatBalance;
  protected readonly swatches = ENVELOPE_SWATCHES;

  protected readonly overview = signal<EnvelopeOverviewResponse | null>(null);
  protected readonly date = signal('');
  protected readonly notes = signal('');
  private readonly cells = signal<Record<string, Cell>>({});

  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly saveError = signal<ApiError | null>(null);
  protected readonly saving = signal(false);
  private readonly submitted = signal(false);

  protected readonly boxes = computed(() => (this.overview()?.cashBoxes ?? []).filter((box) => box.isActive));
  protected readonly envelopes = computed(() =>
    (this.overview()?.envelopes ?? []).map((row) => row.envelope).filter((envelope) => envelope.isActive),
  );

  /** Cells whose text is not a valid amount (0 or more). */
  protected readonly invalidKeys = computed(() => {
    const invalid = new Set<string>();
    for (const [key, cell] of Object.entries(this.cells())) {
      if (cell.text.trim() !== '' && parseHundredths(cell.text) === null) {
        invalid.add(key);
      }
    }
    return invalid;
  });

  protected readonly changes = computed(() =>
    Object.entries(this.cells())
      .map(([key, cell]) => ({ key, target: targetOf(cell) }))
      .filter((c) => c.target !== null),
  );

  /** Per box: what it will hold after saving (sum of its envelopes' targets). */
  protected readonly boxTotals = computed(() => {
    const totals = new Map<string, number>();
    for (const [key, cell] of Object.entries(this.cells())) {
      const boxId = key.split('|')[1];
      const value = targetOf(cell) ?? cell.balanceCents;
      totals.set(boxId, (totals.get(boxId) ?? 0) + value);
    }
    return totals;
  });

  ngOnInit(): void {
    this.load();
  }

  protected key(envelopeId: string, boxId: string): string {
    return `${envelopeId}|${boxId}`;
  }

  protected cell(envelopeId: string, boxId: string): Cell | undefined {
    return this.cells()[this.key(envelopeId, boxId)];
  }

  protected isChanged(envelopeId: string, boxId: string): boolean {
    const cell = this.cell(envelopeId, boxId);
    return !!cell && targetOf(cell) !== null;
  }

  protected setText(envelopeId: string, boxId: string, text: string): void {
    const key = this.key(envelopeId, boxId);
    this.cells.update((cells) => ({ ...cells, [key]: { ...cells[key], text } }));
  }

  /** Another day: balances are re-read for that day, the typed values are lost (they were for another day). */
  protected setDate(date: string): void {
    if (!date) {
      return;
    }
    this.date.set(date);
    this.load(date);
  }

  protected boxTotal(boxId: string): number {
    return this.boxTotals().get(boxId) ?? 0;
  }

  protected save(): void {
    this.submitted.set(true);
    if (this.invalidKeys().size > 0 || this.changes().length === 0) {
      return;
    }
    this.saving.set(true);
    this.saveError.set(null);
    this.operationsApi
      .createOpening({
        date: this.date(),
        notes: this.notes().trim() || null,
        targets: this.changes().map(({ key, target }) => {
          const [envelopeId, cashBoxId] = key.split('|');
          return { envelopeId, cashBoxId, amountCents: target! };
        }),
      })
      .subscribe({
        next: (saved) => void this.router.navigate(['/sobres/operaciones', saved.id]),
        error: (err: unknown) => {
          this.saveError.set(toApiError(err));
          this.saving.set(false);
        },
      });
  }

  protected showInvalid(envelopeId: string, boxId: string): boolean {
    return this.submitted() && this.invalidKeys().has(this.key(envelopeId, boxId));
  }

  protected showInvalidSummary(): boolean {
    return this.submitted() && this.invalidKeys().size > 0;
  }

  protected fieldMessages(error: ApiError): string[] {
    return Object.values(error.fields);
  }

  protected showNothingChanged(): boolean {
    return this.submitted() && this.changes().length === 0 && this.invalidKeys().size === 0;
  }

  protected amountText(cents: number): string {
    return cents < 0 ? `−${formatHundredths(-cents)}` : formatHundredths(cents);
  }

  private load(date?: string): void {
    this.envelopesApi.balances(date).subscribe({
      next: (overview) => {
        const cells: Record<string, Cell> = {};
        for (const row of overview.envelopes.filter((r) => r.envelope.isActive)) {
          for (const box of overview.cashBoxes.filter((b) => b.isActive)) {
            const balanceCents = row.balances.find((b) => b.cashBoxId === box.id)?.amountCents ?? 0;
            cells[this.key(row.envelope.id, box.id)] = { balanceCents, text: balanceCents < 0 ? '' : toInputText(balanceCents) };
          }
        }
        this.overview.set(overview);
        this.date.set(overview.date);
        this.cells.set(cells);
        this.submitted.set(false);
        this.loadError.set(null);
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }
}
