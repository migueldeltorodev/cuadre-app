import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { ExpenseCategoriesApi } from '../../../core/api/expenses-api';
import { ExpenseCategoryResponse } from '../../../core/api/models';
import { DIALOG_DEFAULTS } from '../../../shared/ui/dialog-config';
import { ExpenseCategoryDialog, ExpenseCategoryDialogData } from './expense-category-dialog';

/** Configuración › Categorías de gasto (RF-GAS-03). Deactivated, never deleted. */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loadError(); as e) {
      <p class="alert alert--bad" role="alert">{{ e.message }}</p>
    }

    <section class="card" aria-labelledby="categories-title">
      <div class="card__head">
        <div>
          <h2 class="card__title" id="categories-title">Categorías de gasto</h2>
          <p class="card__lead">
            Cada gasto lleva una. Las que no usás, desactivalas: los gastos viejos la conservan. Los gastos en sí se
            registran en <a routerLink="/gastos">Gastos</a>.
          </p>
        </div>
        <button class="btn btn--small" type="button" (click)="open()">+ Agregar categoría</button>
      </div>

      @if (categories(); as list) {
        <div class="rows">
          @for (category of list; track category.id) {
            <div class="row" [class.row--inactive]="!category.isActive">
              <div class="row__main">
                <div class="row__title">{{ category.name }}</div>
                <div class="row__sub">
                  {{ category.expenseCount === 0 ? 'Sin gastos' : category.expenseCount === 1 ? '1 gasto' : category.expenseCount + ' gastos' }}
                </div>
              </div>
              @if (!category.isActive) {
                <span class="pill pill--muted">Desactivada</span>
              }
              <button class="btn btn--ghost btn--small" type="button" (click)="open(category)"
                [attr.aria-label]="'Editar ' + category.name">Editar</button>
            </div>
          }
        </div>
      }
    </section>
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 20px; }
    .card { display: flex; flex-direction: column; gap: 12px; }
  `,
})
export default class GastosSection implements OnInit {
  private readonly dialog = inject(Dialog);
  private readonly api = inject(ExpenseCategoriesApi);

  protected readonly categories = signal<ExpenseCategoryResponse[] | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);

  ngOnInit(): void {
    this.load();
  }

  protected open(category?: ExpenseCategoryResponse): void {
    this.dialog
      .open<ExpenseCategoryResponse, ExpenseCategoryDialogData>(ExpenseCategoryDialog, {
        ...DIALOG_DEFAULTS,
        data: { category },
      })
      .closed.subscribe((saved) => saved && this.load());
  }

  private load(): void {
    this.api.getAll().subscribe({
      next: (result) => {
        this.categories.set(result.items);
        this.loadError.set(null);
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }
}
