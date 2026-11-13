import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { DailyClosingsApi } from '../../../core/api/closings-api';
import { DailyClosingSummaryResponse, PagedResponse } from '../../../core/api/models';
import { formatShortDate } from '../../../shared/format/date';
import { closingStateLabel, differenceText, formatCup } from '../closing-format';

const PAGE_SIZE = 25;

/** Every closed day, newest first. Reopened ones only when asked. */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cierres-page.html',
  styleUrls: ['../../../shared/ui/list-page.css'],
})
export default class CierresPage implements OnInit {
  private readonly api = inject(DailyClosingsApi);

  protected readonly formatShortDate = formatShortDate;
  protected readonly formatCup = formatCup;
  protected readonly differenceText = differenceText;
  protected readonly stateLabel = closingStateLabel;

  protected readonly result = signal<PagedResponse<DailyClosingSummaryResponse> | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly includeVoided = signal(false);
  protected readonly page = signal(1);

  protected readonly hasFilters = computed(() => !!(this.from() || this.to()));

  ngOnInit(): void {
    this.load();
  }

  protected setFrom(value: string): void {
    this.from.set(value);
    this.reload();
  }

  protected setTo(value: string): void {
    this.to.set(value);
    this.reload();
  }

  protected toggleVoided(checked: boolean): void {
    this.includeVoided.set(checked);
    this.reload();
  }

  protected clearFilters(): void {
    this.from.set('');
    this.to.set('');
    this.reload();
  }

  protected goTo(page: number): void {
    this.page.set(page);
    this.load();
  }

  private reload(): void {
    this.page.set(1);
    this.load();
  }

  private load(): void {
    this.api
      .getAll({
        from: this.from() || undefined,
        to: this.to() || undefined,
        includeVoided: this.includeVoided() || undefined,
        page: this.page(),
        pageSize: PAGE_SIZE,
      })
      .subscribe({
        next: (result) => {
          this.result.set(result);
          this.loadError.set(null);
        },
        error: (err: unknown) => this.loadError.set(toApiError(err)),
      });
  }
}
