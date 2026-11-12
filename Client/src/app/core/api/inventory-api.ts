import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CreateCountRequest,
  CreateExitRequest,
  CreateTransferRequest,
  ItemsResponse,
  KardexQuery,
  KardexResponse,
  PagedResponse,
  ShoppingListItemResponse,
  StockMovementsResponse,
  StockOperationQuery,
  StockOperationResponse,
  StockOperationSummaryResponse,
  StockQuery,
  StockResponse,
} from './models';

/** Thin HTTP wrappers for inventory (slice 4). Routes mirror ApiEndpoints.cs. No logic here. */

function toParams(query: object): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '' && value !== false) {
      params = params.set(key, String(value));
    }
  }
  return params;
}

/** Transfers, exits and counts. Saving = confirming; never edited, only voided. */
@Injectable({ providedIn: 'root' })
export class StockOperationsApi {
  private readonly http = inject(HttpClient);

  getAll(query: StockOperationQuery = {}): Observable<PagedResponse<StockOperationSummaryResponse>> {
    return this.http.get<PagedResponse<StockOperationSummaryResponse>>('/api/stock-operations', {
      params: toParams(query),
    });
  }

  get(id: string): Observable<StockOperationResponse> {
    return this.http.get<StockOperationResponse>(`/api/stock-operations/${id}`);
  }

  createTransfer(request: CreateTransferRequest): Observable<StockOperationResponse> {
    return this.http.post<StockOperationResponse>('/api/stock-operations/transfers', request);
  }

  createExit(request: CreateExitRequest): Observable<StockOperationResponse> {
    return this.http.post<StockOperationResponse>('/api/stock-operations/exits', request);
  }

  createCount(request: CreateCountRequest): Observable<StockOperationResponse> {
    return this.http.post<StockOperationResponse>('/api/stock-operations/counts', request);
  }

  void(id: string, reason: string): Observable<StockOperationResponse> {
    return this.http.post<StockOperationResponse>(`/api/stock-operations/${id}/void`, { reason });
  }
}

/** Read-only views over the stock ledger. */
@Injectable({ providedIn: 'root' })
export class InventoryApi {
  private readonly http = inject(HttpClient);

  stock(query: StockQuery = {}): Observable<StockResponse> {
    return this.http.get<StockResponse>('/api/inventory/stock', { params: toParams(query) });
  }

  /** Without a date: today (the server's day). */
  movements(date?: string): Observable<StockMovementsResponse> {
    return this.http.get<StockMovementsResponse>('/api/inventory/movements', { params: toParams({ date }) });
  }

  shoppingList(): Observable<ItemsResponse<ShoppingListItemResponse>> {
    return this.http.get<ItemsResponse<ShoppingListItemResponse>>('/api/inventory/shopping-list');
  }

  kardex(productId: string, query: KardexQuery = {}): Observable<KardexResponse> {
    return this.http.get<KardexResponse>(`/api/products/${productId}/kardex`, { params: toParams(query) });
  }
}
