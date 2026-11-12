import { HttpParams } from '@angular/common/http';

/** Query object → HttpParams, skipping what means "no filter": undefined, null, '' and false. */
export function toParams(query: object): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '' && value !== false) {
      params = params.set(key, String(value));
    }
  }
  return params;
}
