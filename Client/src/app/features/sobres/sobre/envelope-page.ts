import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { EnvelopesApi } from '../../../core/api/envelopes-api';
import {
  EnvelopeBoxBalanceResponse,
  EnvelopeLedgerResponse,
  EnvelopeOperationResponse,
  EnvelopeOverviewResponse,
} from '../../../core/api/models';
import { formatShortDate } from '../../../shared/format/date';
import { formatPercent } from '../../../shared/format/money';
import { DIALOG_DEFAULTS } from '../../../shared/ui/dialog-config';
import {
  ENVELOPE_NOTES,
  ENVELOPE_SWATCHES,
  cashMovementLabel,
  cashMovementLink,
  firstOfMonth,
  formatBalance,
  formatSignedMoney,
  totalsByCurrency,
} from '../envelope-format';
import {
  EnvelopeDialogKind,
  EnvelopeOperationDialog,
  EnvelopeOperationDialogData,
} from '../envelope-operation-dialog';

/**
 * The ledger of ONE envelope (RF-PF-07): every cash movement that touched it, oldest first, with the running
 * balance of its box. Voided documents stay visible, muted (their reversal rows are there too).
 */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './envelope-page.html',
  styleUrls: ['../../../shared/ui/list-page.css', './envelope-page.css'],
})
export default class EnvelopePage implements OnInit {
  /** Route param (withComponentInputBinding). */
  readonly id = input.required<string>();

  private readonly api = inject(EnvelopesApi);
  private readonly dialog = inject(Dialog);

  protected readonly formatShortDate = formatShortDate;
  protected readonly formatBalance = formatBalance;
  protected readonly formatSignedMoney = formatSignedMoney;
  protected readonly cashMovementLabel = cashMovementLabel;
  protected readonly cashMovementLink = cashMovementLink;

  protected readonly overview = signal<EnvelopeOverviewResponse | null>(null);
  protected readonly ledger = signal<EnvelopeLedgerResponse | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly lastSaved = signal<EnvelopeOperationResponse | null>(null);

  protected readonly cashBoxId = signal('');
  protected readonly from = signal('');
  protected readonly to = signal('');

  protected readonly row = computed(() => this.overview()?.envelopes.find((r) => r.envelope.id === this.id()) ?? null);
  protected readonly envelope = computed(() => this.ledger()?.envelope ?? this.row()?.envelope ?? null);
  protected readonly swatch = computed(() => {
    const envelope = this.envelope();
    return envelope ? ENVELOPE_SWATCHES[envelope.role] : 'transparent';
  });
  protected readonly note = computed(() => {
    const envelope = this.envelope();
    return envelope ? ENVELOPE_NOTES[envelope.role] : '';
  });
  protected readonly pill = computed(() => {
    const row = this.row();
    if (!row) {
      return '';
    }
    return row.percentBps === null ? 'recibe A2' : `${formatPercent(row.percentBps)} de A3`;
  });

  protected readonly boxes = computed(() => this.overview()?.cashBoxes ?? []);
  protected readonly totals = computed(() => totalsByCurrency(this.row()?.balances ?? [], this.boxes()));

  protected readonly hasFilters = computed(() => !!this.cashBoxId() || !!this.to());

  /** Withdrawal for every envelope but Taxes, which is paid (TaxPayment). */
  protected readonly outKind = computed<EnvelopeDialogKind>(() =>
    this.envelope()?.role === 'Tax' ? 'TaxPayment' : 'Withdrawal',
  );

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.api.balances().subscribe({
      next: (overview) => {
        this.overview.set(overview);
        this.from.set(firstOfMonth(overview.date));
        this.loadLedger();
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }

  protected boxName(id: string): string {
    return this.boxes().find((box) => box.id === id)?.name ?? '—';
  }

  protected balanceText(balance: EnvelopeBoxBalanceResponse): string {
    return formatBalance(balance.amountCents, this.boxes().find((b) => b.id === balance.cashBoxId)?.currency ?? 'CUP');
  }

  protected setBox(value: string): void {
    this.cashBoxId.set(value);
    this.loadLedger();
  }

  protected setFrom(value: string): void {
    this.from.set(value);
    this.loadLedger();
  }

  protected setTo(value: string): void {
    this.to.set(value);
    this.loadLedger();
  }

  protected clearFilters(): void {
    this.cashBoxId.set('');
    this.to.set('');
    this.from.set(firstOfMonth(this.overview()?.date ?? ''));
    this.loadLedger();
  }

  protected open(kind: EnvelopeDialogKind): void {
    const overview = this.overview();
    if (!overview) {
      return;
    }
    this.dialog
      .open<EnvelopeOperationResponse, EnvelopeOperationDialogData>(EnvelopeOperationDialog, {
        ...DIALOG_DEFAULTS,
        data: { kind, overview, envelopeId: this.id() },
      })
      .closed.subscribe((saved) => {
        if (saved) {
          this.lastSaved.set(saved);
          this.load();
        }
      });
  }

  private loadLedger(): void {
    this.api
      .movements(this.id(), {
        from: this.from() || undefined,
        to: this.to() || undefined,
        cashBoxId: this.cashBoxId() || undefined,
      })
      .subscribe({
        next: (ledger) => {
          this.ledger.set(ledger);
          this.loadError.set(null);
        },
        error: (err: unknown) => this.loadError.set(toApiError(err)),
      });
  }
}
