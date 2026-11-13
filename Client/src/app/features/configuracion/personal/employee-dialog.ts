import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { StaffApi } from '../../../core/api/closings-api';
import { EmployeeResponse } from '../../../core/api/models';

export interface EmployeeDialogData {
  /** Absent = create. */
  employee?: EmployeeResponse;
}

/** Add or edit someone who works the counter. Never deleted: the closings keep who worked. */
@Component({
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()" novalidate>
      <div>
        <h2 class="dialog__title">{{ editing ? 'Editar persona' : 'Nueva persona' }}</h2>
        <p class="card__lead">Solo para saber quién trabajó cada día y a quién reclamarle un faltante.</p>
      </div>

      <label class="field">
        <span class="field__label">Nombre</span>
        <input class="input" formControlName="name" [class.input--invalid]="nameInvalid()" autocomplete="off"
          cdkFocusInitial />
        @if (nameInvalid()) {
          <span class="field__error">El nombre es obligatorio (hasta 60 letras).</span>
        } @else if (error()?.status === 409) {
          <span class="field__error">{{ error()!.message }}</span>
        }
      </label>

      @if (editing) {
        <label class="check">
          <input type="checkbox" formControlName="isActive" />
          <span>Trabaja todavía</span>
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
export class EmployeeDialog {
  protected readonly data = inject<EmployeeDialogData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<EmployeeResponse>>(DialogRef);
  private readonly api = inject(StaffApi);

  protected readonly editing = this.data.employee !== undefined;
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  private readonly submitted = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.data.employee?.name ?? '', [Validators.required, Validators.maxLength(60)]],
    isActive: [this.data.employee?.isActive ?? true],
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

    const request$: Observable<EmployeeResponse> = this.editing
      ? this.api.updateEmployee(this.data.employee!.id, { name, isActive: value.isActive })
      : this.api.createEmployee({ name });

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
