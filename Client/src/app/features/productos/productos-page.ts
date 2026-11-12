import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { ApiError, toApiError } from '../../core/api/api-error';
import { CategoriesApi, ProductsApi } from '../../core/api/catalog-api';
import { PagedResponse, ProductResponse } from '../../core/api/models';
import { formatHundredths } from '../../shared/format/money';
import { perUnit } from '../../shared/format/quantity';
import { CategoryIndex, productState } from './catalog-labels';

const PAGE_SIZE = 50;

/**
 * Product list (design "Productos", RF-PRD-01/06). Search and category filter run on the server.
 * Stock, cost and margin columns arrive with purchases (slice 3) and inventory (slice 4).
 */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './productos-page.html',
  styleUrl: './productos-page.css',
})
export default class ProductosPage implements OnInit {
  private readonly productsApi = inject(ProductsApi);
  private readonly categoriesApi = inject(CategoriesApi);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly formatHundredths = formatHundredths;
  protected readonly perUnit = perUnit;
  protected readonly productState = productState;

  protected readonly categories = signal(new CategoryIndex([]));
  protected readonly result = signal<PagedResponse<ProductResponse> | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);

  protected readonly search = signal('');
  protected readonly categoryId = signal<string | null>(null);
  protected readonly includeInactive = signal(false);
  protected readonly page = signal(1);

  /** Totals for the subtitle, independent of the filters (active products only). */
  private readonly totals = signal<{ active: number; pending: number } | null>(null);
  protected readonly subtitle = computed(() => {
    const totals = this.totals();
    if (totals === null) {
      return '';
    }
    const active = `${totals.active} ${totals.active === 1 ? 'producto activo' : 'productos activos'}`;
    return totals.pending === 0 ? active : `${active} · ${totals.pending} para revisar`;
  });

  private readonly searchInput = new Subject<string>();

  ngOnInit(): void {
    this.categoriesApi.getAll().subscribe({
      next: (response) => this.categories.set(new CategoryIndex(response.items)),
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });

    // Typing waits 250 ms of calm before asking the server.
    this.searchInput
      .pipe(debounceTime(250), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe((text) => {
        this.search.set(text);
        this.page.set(1);
        this.load();
      });

    this.load();
    this.loadTotals();
  }

  protected onSearch(text: string): void {
    this.searchInput.next(text.trim());
  }

  protected pickCategory(id: string | null): void {
    this.categoryId.set(id);
    this.page.set(1);
    this.load();
  }

  protected toggleInactive(checked: boolean): void {
    this.includeInactive.set(checked);
    this.page.set(1);
    this.load();
  }

  protected goTo(page: number): void {
    this.page.set(page);
    this.load();
  }

  private load(): void {
    this.productsApi
      .getAll({
        search: this.search() || undefined,
        categoryId: this.categoryId() ?? undefined,
        includeInactive: this.includeInactive() || undefined,
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

  /** A bodegón has hundreds of products, not millions: one unfiltered page of 200 is enough to count. */
  private loadTotals(): void {
    this.productsApi.getAll({ pageSize: 200 }).subscribe({
      next: (all) =>
        this.totals.set({
          active: all.total,
          pending: all.items.filter((product) => productState(product).tone !== 'ok').length,
        }),
      error: () => this.totals.set(null),
    });
  }
}
