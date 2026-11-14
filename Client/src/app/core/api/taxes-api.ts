import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateTaxRequest, ItemsResponse, TaxResponse, UpdateTaxRequest } from './models';

/** D-62: the taxes to suggest setting aside. Thin HTTP wrapper; never deleted, only deactivated. */
@Injectable({ providedIn: 'root' })
export class TaxesApi {
  private readonly http = inject(HttpClient);

  /** Active first, then by name. */
  getAll(): Observable<ItemsResponse<TaxResponse>> {
    return this.http.get<ItemsResponse<TaxResponse>>('/api/taxes');
  }

  create(request: CreateTaxRequest): Observable<TaxResponse> {
    return this.http.post<TaxResponse>('/api/taxes', request);
  }

  update(id: string, request: UpdateTaxRequest): Observable<TaxResponse> {
    return this.http.put<TaxResponse>(`/api/taxes/${id}`, request);
  }
}
