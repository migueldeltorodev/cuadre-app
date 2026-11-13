import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin, of, switchMap } from 'rxjs';
import { ApiError, toApiError } from '../../core/api/api-error';
import { EnvelopesApi } from '../../core/api/envelopes-api';
import {
  CashMovementResponse,
  CurrencyCode,
  EnvelopeBalanceResponse,
  EnvelopeCashBoxResponse,
  EnvelopeLedgerResponse,
  EnvelopeOperationResponse,
  EnvelopeOverviewResponse,
} from '../../core/api/models';
import { formatShortDate } from '../../shared/format/date';
import { formatHundredths, formatPercent } from '../../shared/format/money';
import { DIALOG_DEFAULTS } from '../../shared/ui/dialog-config';
import {
  CurrencyAmount,
  ENVELOPE_SWATCHES,
  addDays,
  cashMovementLabel,
  cashMovementLink,
  envelopeOperationKindLabel,
  firstOfMonth,
  formatBalance,
  formatSignedMoney,
  totalsByCurrency,
} from './envelope-format';
import {
  EnvelopeDialogKind,
  EnvelopeOperationDialog,
  EnvelopeOperationDialogData,
} from './envelope-operation-dialog';

const RECENT_ROWS = 12;
const MONTH = new Intl.DateTimeFormat('es', { month: 'long' });

interface BoxPart {
  label: string;
  cents: number;
  currency: CurrencyCode;
}

interface EnvelopeCard {
  row: EnvelopeBalanceResponse;
  swatch: string;
  pill: string;
  /** The big number: CUP (it is where most money lives); USD goes as a part. */
  cupCents: number;
  usdCents: number | null;
  parts: BoxPart[];
  monthIn: CurrencyAmount[];
  monthOut: CurrencyAmount[];
}

/**
 * Sobres y cajas (RF-PF-05/06/07): envelopes say WHAT the money is for, cash boxes WHERE it is.
 * Per box, the envelopes add up to the box (every cash movement carries an envelope). Pesos and dollars never mix.
 */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sobres-page.html',
  styleUrls: ['../../shared/ui/list-page.css', './sobres-page.css'],
})
export default class SobresPage implements OnInit {
  private readonly api = inject(EnvelopesApi);
  private readonly dialog = inject(Dialog);

  protected readonly formatShortDate = formatShortDate;
  protected readonly formatHundredths = formatHundredths;
  protected readonly formatBalance = formatBalance;
  protected readonly formatSignedMoney = formatSignedMoney;
  protected readonly cashMovementLabel = cashMovementLabel;
  protected readonly cashMovementLink = cashMovementLink;

  protected readonly overview = signal<EnvelopeOverviewResponse | null>(null);
  private readonly movements = signal<CashMovementResponse[]>([]);
  protected readonly loadError = signal<ApiError | null>(null);

  /** What the last saved operation left behind (and its negative-balance warnings, RF-PF-08). */
  protected readonly lastSaved = signal<EnvelopeOperationResponse | null>(null);

  protected readonly monthName = computed(() => {
    const date = this.overview()?.date;
    if (!date) {
      return '';
    }
    const [y, m] = date.split('-').map(Number);
    const name = MONTH.format(new Date(y, m - 1, 1));
    return name.charAt(0).toUpperCase() + name.slice(1);
  });

  private readonly boxesById = computed(
    () => new Map((this.overview()?.cashBoxes ?? []).map((box) => [box.id, box] as const)),
  );

  protected readonly cards = computed<EnvelopeCard[]>(() => {
    const overview = this.overview();
    if (!overview) {
      return [];
    }
    const monthStart = firstOfMonth(overview.date);
    const thisMonth = this.movements().filter((m) => m.date >= monthStart && !m.isVoided);

    return overview.envelopes.map((row) => {
      const totals = totalsByCurrency(row.balances, overview.cashBoxes);
      const own = thisMonth.filter((m) => m.envelopeId === row.envelope.id);
      return {
        row,
        swatch: ENVELOPE_SWATCHES[row.envelope.role],
        pill: row.percentBps === null ? 'recibe A2' : `${formatPercent(row.percentBps)} de A3`,
        cupCents: totals.find((t) => t.currency === 'CUP')?.cents ?? 0,
        usdCents: totals.find((t) => t.currency === 'USD')?.cents ?? null,
        parts: row.balances
          .filter((balance) => balance.amountCents !== 0)
          .map((balance) => {
            const box = this.boxesById().get(balance.cashBoxId);
            return {
              label: box ? boxLabel(box) : '—',
              cents: balance.amountCents,
              currency: box?.currency ?? 'CUP',
            };
          }),
        monthIn: sumByCurrency(own.filter((m) => m.amountCents > 0)),
        monthOut: sumByCurrency(own.filter((m) => m.amountCents < 0)),
      };
    });
  });

  protected readonly boxes = computed(() => this.overview()?.cashBoxes ?? []);

  protected readonly trappedCents = computed(() =>
    this.boxes()
      .filter((box) => box.kind === 'Bank')
      .reduce((total, box) => total + box.totalCents, 0),
  );

  /** "Ganancia en Efectivo CUP quedó en −1,500 CUP": allowed (RF-PF-08), never hidden. */
  protected readonly negatives = computed(() => {
    const overview = this.overview();
    if (!overview) {
      return [];
    }
    return overview.envelopes.flatMap((row) =>
      row.balances
        .filter((balance) => balance.amountCents < 0)
        .map((balance) => {
          const box = this.boxesById().get(balance.cashBoxId);
          return {
            key: `${row.envelope.id}-${balance.cashBoxId}`,
            text: `${row.envelope.name} en ${box?.name ?? '—'}: ${formatBalance(balance.amountCents, box?.currency ?? 'CUP')}`,
          };
        }),
    );
  });

  /** Nothing loaded yet: the first thing to do is the opening balance. */
  protected readonly isEmpty = computed(() => {
    const overview = this.overview();
    return !!overview && overview.envelopes.every((row) => row.balances.length === 0) && this.movements().length === 0;
  });

  protected readonly canOperate = computed(() => this.boxes().some((box) => box.isActive));

  protected readonly recent = computed(() =>
    this.movements()
      .filter((movement) => !movement.isVoided)
      .sort((a, b) => b.date.localeCompare(a.date) || b.createdAt.localeCompare(a.createdAt))
      .slice(0, RECENT_ROWS),
  );

  ngOnInit(): void {
    this.load();
  }

  protected open(kind: EnvelopeDialogKind, envelopeId?: string): void {
    const overview = this.overview();
    if (!overview) {
      return;
    }
    this.dialog
      .open<EnvelopeOperationResponse, EnvelopeOperationDialogData>(EnvelopeOperationDialog, {
        ...DIALOG_DEFAULTS,
        data: { kind, overview, envelopeId },
      })
      .closed.subscribe((saved) => {
        if (saved) {
          this.lastSaved.set(saved);
          this.load();
        }
      });
  }

  protected savedLabel(saved: EnvelopeOperationResponse): string {
    return `${envelopeOperationKindLabel(saved.kind)} #${saved.number}`;
  }

  protected warningText(saved: EnvelopeOperationResponse): string[] {
    return saved.warnings.map(
      (w) => `${w.envelopeName} en ${w.cashBoxName} quedó en ${formatBalance(w.balanceCents, w.currency)}.`,
    );
  }

  /** The card's big number, without the currency (it goes smaller beside it). */
  protected signedNumber(cents: number): string {
    return cents < 0 ? `−${formatHundredths(-cents)}` : formatHundredths(cents);
  }

  /** "1,000 CUP y 20 USD": each currency on its own, never summed. */
  protected joinAmounts(amounts: CurrencyAmount[]): string {
    return amounts.map((a) => `${formatHundredths(a.cents)} ${a.currency}`).join(' y ');
  }

  protected boxNote(box: EnvelopeCashBoxResponse): string | null {
    if (box.kind === 'Bank') {
      return 'atrapada';
    }
    return box.isActive ? null : 'inactiva';
  }

  private load(): void {
    this.api
      .balances()
      .pipe(
        switchMap((overview) => {
          const from = addDays(overview.date, -31);
          const ledgers = overview.envelopes.map((row) => this.api.movements(row.envelope.id, { from }));
          return forkJoin({ overview: of(overview), ledgers: ledgers.length ? forkJoin(ledgers) : of([] as EnvelopeLedgerResponse[]) });
        }),
      )
      .subscribe({
        next: ({ overview, ledgers }) => {
          this.overview.set(overview);
          this.movements.set(ledgers.flatMap((ledger) => ledger.entries.map((entry) => entry.movement)));
          this.loadError.set(null);
        },
        error: (err: unknown) => this.loadError.set(toApiError(err)),
      });
  }
}

/** "Banco CUP (atrapada)": the bank cannot pay in cash nor be changed to dollars. */
function boxLabel(box: EnvelopeCashBoxResponse): string {
  return box.kind === 'Bank' ? `${box.name} (atrapada)` : box.name;
}

function sumByCurrency(movements: CashMovementResponse[]): CurrencyAmount[] {
  const totals = new Map<CurrencyCode, number>();
  for (const movement of movements) {
    totals.set(movement.currency, (totals.get(movement.currency) ?? 0) + Math.abs(movement.amountCents));
  }
  return (['CUP', 'USD'] as CurrencyCode[])
    .filter((currency) => totals.has(currency))
    .map((currency) => ({ currency, cents: totals.get(currency)! }));
}
