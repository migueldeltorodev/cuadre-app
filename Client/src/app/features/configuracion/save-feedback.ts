import { DestroyRef, inject, signal } from '@angular/core';
import { ApiError } from '../../core/api/api-error';

/**
 * Save state shared by the settings forms: "Guardando…", a short-lived "Guardado ✓", or the error.
 * Call it in a field initializer (it uses inject).
 */
export function createSaveFeedback() {
  const saving = signal(false);
  const saved = signal(false);
  const error = signal<ApiError | null>(null);
  let timer: ReturnType<typeof setTimeout> | undefined;

  inject(DestroyRef).onDestroy(() => clearTimeout(timer));

  return {
    saving: saving.asReadonly(),
    saved: saved.asReadonly(),
    error: error.asReadonly(),
    start(): void {
      clearTimeout(timer);
      saving.set(true);
      saved.set(false);
      error.set(null);
    },
    succeed(): void {
      saving.set(false);
      saved.set(true);
      timer = setTimeout(() => saved.set(false), 3000);
    },
    fail(apiError: ApiError): void {
      saving.set(false);
      error.set(apiError);
    },
  };
}
