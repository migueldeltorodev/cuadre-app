import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { ExpenseCategoriesApi } from '../../../core/api/expenses-api';
import { ExpenseCategoryResponse } from '../../../core/api/models';

export interface ExpenseCategoryDialogData {
  /** Absent = create. */
  category?: ExpenseCategoryResponse;
}

/** Add, rename or (de)activate an expense category. Never deleted: the expenses keep it. */
@Component({
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()" novalidate>
      <div>
        <h2 class="dialog__title">{{ editing ? 'Editar categoría' : 'Nueva categoría de gasto' }}</h2>
        <p class="card__lead">Para saber en qué se va la plata: Transporte, Gestor, Servicios…</p>
      </div>

      <label class="field">
        <span class="field__label">Nombre</span>
        <input class="input" formControlName="name" [class.input--invalid]="nameInvalid()" autocomplete="off"
          maxlength="60" cdkFocusInitial />
        @if (nameInvalid()) {
          <span class="field__error">El nombre es obligatorio (hasta 60 letras).</span>
        } @else if (error()?.status === 409) {
          <span class="field__error">{{ error()!.message }}</span>
        }
      </label>

      @if (editing) {
        <label class="check">
          <input type="checkbox" formControlName="isActive" />
          <span>Se usa (si no, deja de aparecer al registrar gastos)</span>
        </label>
      }

      @if (error(); as e) {
        @if (e.status !== 409) {
          <p class="alert alert--bad" role="alert">{{ e.message }}</p>
        }
      }

      <div class="form__actions">
        <button class="btn" type="button" (click)="ref.close()">Cancelar</button>
        <button class="btn btn--primary" type="submit" [disabled]="saving()">
          {{ saving() ? 'Guardando…' : 'Guardar' }}
        </button>
      </div>
    </form>
  `,
  styles: `
    .check { display: flex; align-items: center; gap: 10px; min-height: 44px; cursor: pointer; }
    .check input { width: 18px; height: 18px; accent-color: var(--c-accent); }
  `,
})
export class ExpenseCategoryDialog {
  protected readonly data = inject<ExpenseCategoryDialogData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<ExpenseCategoryResponse>>(DialogRef);
  private readonly api = inject(ExpenseCategoriesApi);

  protected readonly editing = this.data.category !== undefined;
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  private readonly submitted = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.data.category?.name ?? '', [Validators.required, Validators.maxLength(60)]],
    isActive: [this.data.category?.isActive ?? true],
  });

  protected nameInvalid(): boolean {
    const name = this.form.controls.name;
    return (name.invalid || (this.submitted() && !name.value.trim())) && (name.touched || this.submitted());
  }

  protected save(): void {
    this.submitted.set(true);
    const value = this.form.getRawValue();
    const name = value.name.trim();
    if (this.form.invalid || !name) {
      return;
    }

    const request$: Observable<ExpenseCategoryResponse> = this.editing
      ? this.api.update(this.data.category!.id, { name, isActive: value.isActive })
      : this.api.create({ name });

    this.saving.set(true);
    this.error.set(null);
    request$.subscribe({
      next: (saved) => this.ref.close(saved),
      error: (err: unknown) => {
        this.error.set(toApiError(err));
        this.saving.set(false);
      },
    });
  }
}
