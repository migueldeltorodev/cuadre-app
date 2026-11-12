import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { LocationsApi } from '../../../core/api/purchases-api';
import { LocationResponse, LocationType } from '../../../core/api/models';

const KIND_LABELS: Record<LocationType, string> = { Storage: 'Almacén', Counter: 'Mostrador' };

/**
 * Ubicaciones (RF-INV-01): where the merchandise is. The kind never changes. Deactivating is refused by the
 * server while it still holds stock or when it is the last active one of its kind; its message is shown as is.
 */
@Component({
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loadError(); as e) {
      <p class="alert alert--bad" role="alert">{{ e.message }}</p>
    }

    <section class="table-card" aria-labelledby="locations-title">
      <div class="table-card__head">
        <h2 class="section-title" id="locations-title">Ubicaciones</h2>
        <p class="muted">
          El almacén es donde se guarda lo comprado; el mostrador, de donde se vende. Una ubicación con mercadería no
          se puede desactivar: primero hay que trasladarla o darle salida.
        </p>
      </div>
      <table class="table">
        <thead>
          <tr>
            <th>Nombre</th>
            <th>Tipo</th>
            <th>Estado</th>
            <th><span class="visually-hidden">Acciones</span></th>
          </tr>
        </thead>
        <tbody>
          @for (location of sorted(); track location.id) {
            <tr [class.voided]="!location.isActive">
              <td>
                @if (editingId() === location.id) {
                  <input class="input" [formControl]="rename" autocomplete="off" aria-label="Nuevo nombre"
                    [class.input--invalid]="rename.invalid && rename.touched" (keydown.enter)="saveRename(location)"
                    (keydown.escape)="cancelRename()" />
                } @else {
                  <span class="strong">{{ location.name }}</span>
                }
              </td>
              <td>{{ kindLabel(location.kind) }}</td>
              <td>
                <span class="pill" [class]="location.isActive ? 'pill--ok' : 'pill--muted'">
                  {{ location.isActive ? 'Activa' : 'Desactivada' }}
                </span>
              </td>
              <td class="end actions">
                @if (editingId() === location.id) {
                  <button class="btn btn--small" type="button" (click)="cancelRename()">Cancelar</button>
                  <button class="btn btn--primary btn--small" type="button" [disabled]="busyId() === location.id"
                    (click)="saveRename(location)">Guardar</button>
                } @else {
                  <button class="btn btn--ghost btn--small" type="button" (click)="startRename(location)">Renombrar</button>
                  <button class="btn btn--ghost btn--small" type="button" [disabled]="busyId() === location.id"
                    (click)="toggleActive(location)">{{ location.isActive ? 'Desactivar' : 'Activar' }}</button>
                }
              </td>
            </tr>
            @if (rowError()?.id === location.id) {
              <tr class="problem-row">
                <td colspan="4"><span class="field__error" role="alert">{{ rowError()!.error.message }}</span></td>
              </tr>
            }
          } @empty {
            <tr><td colspan="4" class="none">Cargando…</td></tr>
          }
        </tbody>
      </table>
    </section>

    <form class="card new" [formGroup]="form" (ngSubmit)="create()" novalidate aria-labelledby="new-title">
      <h2 class="card__title" id="new-title">Nueva ubicación</h2>
      <div class="form__row">
        <label class="field">
          <span class="field__label">Nombre</span>
          <input class="input" formControlName="name" autocomplete="off" placeholder="Almacén del fondo"
            [class.input--invalid]="form.controls.name.invalid && form.controls.name.touched" />
          @if (form.controls.name.invalid && form.controls.name.touched) {
            <span class="field__error">Escribí el nombre.</span>
          }
        </label>
        <label class="field">
          <span class="field__label">Tipo</span>
          <select class="input" formControlName="kind">
            <option value="Storage">Almacén</option>
            <option value="Counter">Mostrador</option>
          </select>
        </label>
      </div>
      @if (createError(); as e) {
        <p class="alert alert--bad" role="alert">{{ e.message }}</p>
      }
      <div class="form__actions">
        <button class="btn btn--primary" type="submit" [disabled]="creating()">
          {{ creating() ? 'Creando…' : 'Crear ubicación' }}
        </button>
      </div>
    </form>
  `,
  styleUrls: ['../inventory-shared.css'],
  styles: `
    .table {
      min-width: 560px;
    }

    .table td {
      vertical-align: middle;
    }

    .actions {
      white-space: nowrap;
    }

    .actions .btn + .btn {
      margin-left: 6px;
    }

    .problem-row td {
      border-top: 0;
      padding-top: 0;
    }

    .new {
      display: flex;
      flex-direction: column;
      gap: 16px;
      max-width: 640px;
    }
  `,
})
export default class LocationsTab implements OnInit {
  private readonly api = inject(LocationsApi);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly locations = signal<LocationResponse[]>([]);
  protected readonly loadError = signal<ApiError | null>(null);

  protected readonly editingId = signal<string | null>(null);
  protected readonly busyId = signal<string | null>(null);
  protected readonly rowError = signal<{ id: string; error: ApiError } | null>(null);
  protected readonly rename = this.fb.control('', [Validators.required, Validators.maxLength(80)]);

  protected readonly creating = signal(false);
  protected readonly createError = signal<ApiError | null>(null);
  protected readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(80)]],
    kind: this.fb.control<LocationType>('Storage'),
  });

  /** Active first, then by kind (counter first) and name. */
  protected readonly sorted = computed(() =>
    [...this.locations()].sort(
      (a, b) =>
        Number(b.isActive) - Number(a.isActive) ||
        (a.kind === b.kind ? 0 : a.kind === 'Counter' ? -1 : 1) ||
        a.name.localeCompare(b.name, 'es'),
    ),
  );

  ngOnInit(): void {
    this.api.getAll().subscribe({
      next: (response) => this.locations.set(response.items),
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }

  protected kindLabel(kind: LocationType): string {
    return KIND_LABELS[kind];
  }

  protected startRename(location: LocationResponse): void {
    this.rowError.set(null);
    this.rename.reset(location.name);
    this.editingId.set(location.id);
  }

  protected cancelRename(): void {
    this.editingId.set(null);
  }

  protected saveRename(location: LocationResponse): void {
    this.rename.markAsTouched();
    const name = this.rename.value.trim();
    if (this.rename.invalid || !name) {
      return;
    }
    if (name === location.name) {
      this.editingId.set(null);
      return;
    }
    this.update(location, { name, isActive: location.isActive }, () => this.editingId.set(null));
  }

  protected toggleActive(location: LocationResponse): void {
    this.update(location, { name: location.name, isActive: !location.isActive });
  }

  protected create(): void {
    this.form.markAllAsTouched();
    const name = this.form.controls.name.value.trim();
    if (this.form.invalid || !name) {
      return;
    }
    this.creating.set(true);
    this.createError.set(null);
    this.api.create({ name, kind: this.form.controls.kind.value }).subscribe({
      next: (created) => {
        this.locations.update((all) => [...all, created]);
        this.form.reset({ name: '', kind: 'Storage' });
        this.creating.set(false);
      },
      error: (err: unknown) => {
        this.createError.set(toApiError(err));
        this.creating.set(false);
      },
    });
  }

  private update(location: LocationResponse, request: { name: string; isActive: boolean }, done?: () => void): void {
    this.busyId.set(location.id);
    this.rowError.set(null);
    this.api.update(location.id, request).subscribe({
      next: (updated) => {
        this.locations.update((all) => all.map((l) => (l.id === updated.id ? updated : l)));
        this.busyId.set(null);
        done?.();
      },
      error: (err: unknown) => {
        this.rowError.set({ id: location.id, error: toApiError(err) });
        this.busyId.set(null);
      },
    });
  }
}
