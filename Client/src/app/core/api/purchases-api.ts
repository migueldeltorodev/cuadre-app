import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CreateLocationRequest,
  CreatePurchaseRequest,
  ItemsResponse,
  LocationResponse,
  PagedResponse,
  PurchaseQuery,
  PurchaseResponse,
  PurchaseSummaryResponse,
  UpdateLocationRequest,
} from './models';

/** Thin HTTP wrappers for purchases (slice 3). Routes mirror ApiEndpoints.cs. No logic here. */

@Injectable({ providedIn: 'root' })
export class PurchasesApi {
  private readonly http = inject(HttpClient);

  getAll(query: PurchaseQuery = {}): Observable<PagedResponse<PurchaseSummaryResponse>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, value);
      }
    }
    return this.http.get<PagedResponse<PurchaseSummaryResponse>>('/api/purchases', { params });
  }

  get(id: string): Observable<PurchaseResponse> {
    return this.http.get<PurchaseResponse>(`/api/purchases/${id}`);
  }

  /** Saving = confirming: stock, cash and costs move at once. */
  create(request: CreatePurchaseRequest): Observable<PurchaseResponse> {
    return this.http.post<PurchaseResponse>('/api/purchases', request);
  }

  void(id: string, reason: string): Observable<PurchaseResponse> {
    return this.http.post<PurchaseResponse>(`/api/purchases/${id}/void`, { reason });
  }
}

@Injectable({ providedIn: 'root' })
export class LocationsApi {
  private readonly http = inject(HttpClient);

  getAll(): Observable<ItemsResponse<LocationResponse>> {
    return this.http.get<ItemsResponse<LocationResponse>>('/api/locations');
  }

  create(request: CreateLocationRequest): Observable<LocationResponse> {
    return this.http.post<LocationResponse>('/api/locations', request);
  }

  /** Rename or (de)activate. Deactivating needs it empty and another active one of its kind. */
  update(id: string, request: UpdateLocationRequest): Observable<LocationResponse> {
    return this.http.put<LocationResponse>(`/api/locations/${id}`, request);
  }
}
