import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { CategoriesApi } from '../../../core/api/catalog-api';
import { CategoryResponse } from '../../../core/api/models';

export interface CategoryDialogData {
  /** Absent = create. */
  category?: CategoryResponse;
  /** Create a subcategory under this category. Ignored when editing (the level never changes). */
  parent?: CategoryResponse;
}

/**
 * Create or edit a category or subcategory (RF-PRD-04). Only top-level categories carry the code
 * prefix; left blank it is derived from the name on the server ("Arroz" → ARR).
 */
@Component({
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()" novalidate>
      <h2 class="dialog__title">{{ title }}</h2>

      <label class="field">
        <span class="field__label">Nombre</span>
        <input class="input" formControlName="name" [class.input--invalid]="nameInvalid()" autocomplete="off"
          [placeholder]="isTopLevel ? 'Comida, Bebidas, Aseo…' : 'Arroz, Granos, Refrescos…'" cdkFocusInitial />
        @if (nameInvalid()) {
          <span class="field__error">El nombre es obligatorio (hasta 60 letras).</span>
        } @else if (fieldError('name'); as message) {
          <span class="field__error">{{ message }}</span>
        }
      </label>

      @if (isTopLevel) {
        <label class="field">
          <span class="field__label">Prefijo de los códigos</span>
          <input class="input prefix" formControlName="codePrefix" [class.input--invalid]="prefixInvalid()"
            autocomplete="off" maxlength="4" [placeholder]="editing ? '' : 'Automático'" />
          @if (prefixInvalid()) {
            <span class="field__error">De 2 a 4 letras, sin tildes (ej. ARR).</span>
          } @else if (fieldError('codePrefix'); as message) {
            <span class="field__error">{{ message }}</span>
          } @else {
            <span class="field__hint">
              Los productos nuevos se codifican así: {{ (form.controls.codePrefix.value || 'ARR').toUpperCase() }}-001.
              @if (editing) { Cambiarlo no renombra los códigos que ya existen. }
            </span>
          }
        </label>
      }

      @if (error(); as e) {
        @if (e.status !== 400) {
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
    .prefix { text-transform: uppercase; max-width: 160px; }
  `,
})
export class CategoryDialog {
  protected readonly data = inject<CategoryDialogData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<CategoryResponse>>(DialogRef);
  private readonly api = inject(CategoriesApi);

  protected readonly editing = this.data.category !== undefined;
  protected readonly isTopLevel = this.editing ? this.data.category!.parentId === null : !this.data.parent;
  protected readonly title = this.editing
    ? `Editar ${this.isTopLevel ? 'categoría' : 'subcategoría'}`
    : this.isTopLevel
      ? 'Nueva categoría'
      : `Nueva subcategoría de ${this.data.parent!.name}`;

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  private readonly submitted = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.data.category?.name ?? '', [Validators.required, Validators.maxLength(60)]],
    codePrefix: [this.data.category?.codePrefix ?? '', Validators.pattern(/^[A-Za-z]{2,4}$|^$/)],
  });

  protected nameInvalid(): boolean {
    const name = this.form.controls.name;
    return name.invalid && (name.touched || this.submitted());
  }

  protected prefixInvalid(): boolean {
    const prefix = this.form.controls.codePrefix;
    return prefix.invalid && (prefix.touched || this.submitted());
  }

  /** 409 (name or prefix taken) shows under the field it belongs to. */
  protected fieldError(field: 'name' | 'codePrefix'): string | null {
    const e = this.error();
    if (!e) {
      return null;
    }
    if (e.status === 400) {
      return e.fields[field] ?? null;
    }
    if (e.status === 409) {
      const aboutPrefix = e.message.toLowerCase().includes('prefijo');
      return (field === 'codePrefix') === aboutPrefix ? e.message : null;
    }
    return null;
  }

  protected save(): void {
    this.submitted.set(true);
    if (this.form.invalid) {
      return;
    }

    const name = this.form.controls.name.value.trim();
    const prefix = this.form.controls.codePrefix.value.trim().toUpperCase();
    const codePrefix = this.isTopLevel && prefix !== '' ? prefix : null;

    const request$: Observable<CategoryResponse> = this.editing
      ? this.api.update(this.data.category!.id, { name, codePrefix })
      : this.api.create({ name, codePrefix, parentId: this.data.parent?.id ?? null });

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
