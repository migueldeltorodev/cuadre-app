import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  BusinessSettingsResponse,
  CashBoxResponse,
  CreateCashBoxRequest,
  CreatePaymentMethodRequest,
  CurrentExchangeRateResponse,
  ExchangeRateResponse,
  ItemsResponse,
  PagedResponse,
  PaymentMethodResponse,
  UpdateBusinessSettingsRequest,
  UpdateCashBoxRequest,
  UpdatePaymentMethodRequest,
} from './models';

/** Thin HTTP wrappers: one per backend resource, routes mirror ApiEndpoints.cs. No logic here. */

@Injectable({ providedIn: 'root' })
export class SettingsApi {
  private readonly http = inject(HttpClient);

  get(): Observable<BusinessSettingsResponse> {
    return this.http.get<BusinessSettingsResponse>('/api/settings');
  }

  update(request: UpdateBusinessSettingsRequest): Observable<BusinessSettingsResponse> {
    return this.http.put<BusinessSettingsResponse>('/api/settings', request);
  }
}

@Injectable({ providedIn: 'root' })
export class CashBoxesApi {
  private readonly http = inject(HttpClient);

  getAll(): Observable<ItemsResponse<CashBoxResponse>> {
    return this.http.get<ItemsResponse<CashBoxResponse>>('/api/cash-boxes');
  }

  create(request: CreateCashBoxRequest): Observable<CashBoxResponse> {
    return this.http.post<CashBoxResponse>('/api/cash-boxes', request);
  }

  update(id: string, request: UpdateCashBoxRequest): Observable<CashBoxResponse> {
    return this.http.put<CashBoxResponse>(`/api/cash-boxes/${id}`, request);
  }
}

@Injectable({ providedIn: 'root' })
export class PaymentMethodsApi {
  private readonly http = inject(HttpClient);

  getAll(): Observable<ItemsResponse<PaymentMethodResponse>> {
    return this.http.get<ItemsResponse<PaymentMethodResponse>>('/api/payment-methods');
  }

  create(request: CreatePaymentMethodRequest): Observable<PaymentMethodResponse> {
    return this.http.post<PaymentMethodResponse>('/api/payment-methods', request);
  }

  update(id: string, request: UpdatePaymentMethodRequest): Observable<PaymentMethodResponse> {
    return this.http.put<PaymentMethodResponse>(`/api/payment-methods/${id}`, request);
  }
}

@Injectable({ providedIn: 'root' })
export class ExchangeRatesApi {
  private readonly http = inject(HttpClient);

  getCurrent(): Observable<CurrentExchangeRateResponse> {
    return this.http.get<CurrentExchangeRateResponse>('/api/exchange-rates/current');
  }

  getHistory(page = 1, pageSize = 14): Observable<PagedResponse<ExchangeRateResponse>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResponse<ExchangeRateResponse>>('/api/exchange-rates', { params });
  }

  /** Create or correct the rate of one day (yyyy-MM-dd). */
  upsert(date: string, cupPerUsdCents: number): Observable<ExchangeRateResponse> {
    return this.http.put<ExchangeRateResponse>(`/api/exchange-rates/${date}`, { cupPerUsdCents });
  }
}
