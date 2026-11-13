import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { CategoriesApi } from '../../../core/api/catalog-api';
import { CategoryResponse } from '../../../core/api/models';
import { DIALOG_DEFAULTS } from '../../../shared/ui/dialog-config';
import { CategoryIndex } from '../catalog-labels';
import { CategoryDialog, CategoryDialogData } from './category-dialog';

/**
 * Categories in two levels (RF-PRD-04). Deleting is allowed only while nothing uses the category;
 * it asks for a second click instead of a browser confirm() (blocked in the desktop window).
 */
@Component({
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="head">
      <div>
        <h1 class="page-title">Categorías</h1>
        <p class="page-subtitle">Dos niveles: categoría y, si hace falta, subcategoría.</p>
      </div>
      <button class="btn btn--primary" type="button" (click)="open()">+ Nueva categoría</button>
    </header>

    @if (error(); as e) {
      <p class="alert alert--bad" role="alert">{{ e.message }}</p>
    }

    @if (index(); as idx) {
      @for (category of idx.topLevel; track category.id) {
        <section class="card">
          <div class="row row--top">
            <div class="row__main">
              <div class="row__title">{{ category.name }}</div>
              <div class="row__sub">{{ countText(category) }}</div>
            </div>
            <span class="pill pill--muted num">{{ category.codePrefix }}</span>
            <button class="btn btn--ghost btn--small" type="button" (click)="open({ parent: category })">
              + Subcategoría
            </button>
            <button class="btn btn--ghost btn--small" type="button" (click)="open({ category })"
              [attr.aria-label]="'Editar ' + category.name">Editar</button>
            @if (canDelete(category, idx)) {
              <button class="btn btn--ghost btn--small danger" type="button" (click)="remove(category)">
                {{ confirming() === category.id ? '¿Seguro? Borrar' : 'Borrar' }}
              </button>
            }
          </div>

          @for (sub of idx.childrenOf(category.id); track sub.id) {
            <div class="row row--sub">
              <div class="row__main">
                <div class="row__title">{{ sub.name }}</div>
                <div class="row__sub">{{ countText(sub) }}</div>
              </div>
              <button class="btn btn--ghost btn--small" type="button" (click)="open({ category: sub })"
                [attr.aria-label]="'Editar ' + sub.name">Editar</button>
              @if (canDelete(sub, idx)) {
                <button class="btn btn--ghost btn--small danger" type="button" (click)="remove(sub)">
                  {{ confirming() === sub.id ? '¿Seguro? Borrar' : 'Borrar' }}
                </button>
              }
            </div>
          }
        </section>
      } @empty {
        <section class="card">
          <p class="card__lead">
            Todavía no hay categorías. Empezá por las grandes (Comida, Bebidas, Aseo…); el prefijo de cada una
            arma el código de sus productos.
          </p>
        </section>
      }
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 20px; max-width: 880px; }
    .head { display: flex; flex-wrap: wrap; gap: 16px; justify-content: space-between; align-items: flex-end; }
    .card { padding: 8px 24px; }
    .row--top { border-top: 0; }
    .row--sub { padding-left: 24px; }
    .row--sub .row__title { font-size: 14.5px; }
    .danger { color: var(--c-bad-ink); }
  `,
})
export default class CategoriasPage implements OnInit {
  private readonly dialog = inject(Dialog);
  private readonly api = inject(CategoriesApi);

  protected readonly index = signal<CategoryIndex | null>(null);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly confirming = signal<string | null>(null);

  ngOnInit(): void {
    this.reload();
  }

  protected countText(category: CategoryResponse): string {
    const count = category.productCount;
    return count === 0 ? 'Sin productos' : `${count} ${count === 1 ? 'producto' : 'productos'}`;
  }

  /** Same rule as the server (422 otherwise): no products and no subcategories. */
  protected canDelete(category: CategoryResponse, idx: CategoryIndex): boolean {
    return category.productCount === 0 && idx.childrenOf(category.id).length === 0;
  }

  protected open(data: CategoryDialogData = {}): void {
    this.dialog
      .open<CategoryResponse, CategoryDialogData>(CategoryDialog, { ...DIALOG_DEFAULTS, data })
      .closed.subscribe((saved) => saved && this.reload());
  }

  protected remove(category: CategoryResponse): void {
    if (this.confirming() !== category.id) {
      this.confirming.set(category.id);
      return;
    }

    this.confirming.set(null);
    this.api.delete(category.id).subscribe({
      next: () => this.reload(),
      error: (err: unknown) => this.error.set(toApiError(err)),
    });
  }

  private reload(): void {
    this.api.getAll().subscribe({
      next: (response) => {
        this.index.set(new CategoryIndex(response.items));
        this.error.set(null);
      },
      error: (err: unknown) => this.error.set(toApiError(err)),
    });
  }
}
