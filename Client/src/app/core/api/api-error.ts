import { HttpErrorResponse } from '@angular/common/http';

/**
 * One shape for every failure the UI shows:
 *  - 400 ValidationFailureResponse → per-field messages (keys in camelCase, like the form controls);
 *  - 404/409/422 ProblemDetails    → `title` is already a sentence written for the user (Spanish);
 *  - status 0                      → the local server is not running.
 */
export interface ApiError {
  status: number;
  message: string;
  fields: Record<string, string>;
}

interface ValidationFailureBody {
  errors: { propertyName: string; message: string }[];
}

export function toApiError(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: -1, message: 'Algo salió mal. Probá de nuevo.', fields: {} };
  }

  if (error.status === 0) {
    return { status: 0, message: 'No hay conexión con el servidor de Cuadre.', fields: {} };
  }

  const body = error.error as Partial<ValidationFailureBody> & { title?: string } | null;

  if (error.status === 400 && Array.isArray(body?.errors)) {
    const fields: Record<string, string> = {};
    for (const failure of body.errors) {
      fields[toCamelCase(failure.propertyName)] ??= failure.message;
    }
    return { status: 400, message: 'Revisá los datos marcados.', fields };
  }

  return {
    status: error.status,
    message: body?.title ?? 'El servidor no pudo completar la operación.',
    fields: {},
  };
}

function toCamelCase(propertyName: string): string {
  return propertyName.charAt(0).toLowerCase() + propertyName.slice(1);
}
