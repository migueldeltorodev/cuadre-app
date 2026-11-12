import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { PriceListApi } from '../../../core/api/catalog-api';
import { PriceListEntryResponse, PriceListResponse } from '../../../core/api/models';
import { formatTimestampDay } from '../../../shared/format/date';
import { formatHundredths } from '../../../shared/format/money';
import { UNIT_LABELS } from '../../../shared/format/quantity';

/**
 * The price list to copy BY HAND to the shop sign (RF-PRD-09; there is no printer).
 * Prices changed since the last copy are highlighted with the old price struck through.
 * "Ya la copié" snapshots the current prices, so the next visit only flags new changes.
 */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './lista-precios-page.html',
  styleUrl: './lista-precios-page.css',
})
export default class ListaPreciosPage implements OnInit {
  private readonly api = inject(PriceListApi);

  protected readonly formatHundredths = formatHundredths;

  protected readonly list = signal<PriceListResponse | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly marking = signal(false);
  protected readonly justMarked = signal(false);

  protected readonly lastCopyDay = computed(() => {
    const copy = this.list()?.lastCopy;
    return copy ? formatTimestampDay(copy.copiedAt) : null;
  });

  protected readonly isEmpty = computed(() => (this.list()?.groups.length ?? 0) === 0);

  ngOnInit(): void {
    this.load();
  }

  /** What goes next to the name: the presentation if there is one ("bolsa 1 kg"), else the unit ("libra"). */
  protected unitText(entry: PriceListEntryResponse): string {
    return entry.presentation ?? UNIT_LABELS[entry.stockUnit].one;
  }

  protected markCopied(): void {
    this.marking.set(true);
    this.api.markCopied().subscribe({
      next: () => {
        this.marking.set(false);
        this.justMarked.set(true);
        this.load();
      },
      error: (err: unknown) => {
        this.marking.set(false);
        this.loadError.set(toApiError(err));
      },
    });
  }

  private load(): void {
    this.api.get().subscribe({
      next: (list) => {
        this.list.set(list);
        this.loadError.set(null);
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }
}
