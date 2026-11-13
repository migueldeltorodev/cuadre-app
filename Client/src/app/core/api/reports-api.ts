import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { toParams } from './query-params';
import {
  CostChangesReportResponse,
  DashboardResponse,
  LossReportResponse,
  MonthlyReportResponse,
  ProductCostHistoryResponse,
  ProductRankingReportResponse,
} from './report-models';

/** Thin HTTP wrappers for the dashboard and the reports (slice 8). Every filter is optional. */

export interface PeriodQuery {
  from?: string;
  to?: string;
}

@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private readonly http = inject(HttpClient);

  get(): Observable<DashboardResponse> {
    return this.http.get<DashboardResponse>('/api/dashboard');
  }
}

@Injectable({ providedIn: 'root' })
export class ReportsApi {
  private readonly http = inject(HttpClient);

  /** Without year/month: this month up to today. */
  monthly(year?: number, month?: number): Observable<MonthlyReportResponse> {
    return this.http.get<MonthlyReportResponse>('/api/reports/monthly', { params: toParams({ year, month }) });
  }

  losses(query: PeriodQuery = {}): Observable<LossReportResponse> {
    return this.http.get<LossReportResponse>('/api/reports/losses', { params: toParams(query) });
  }

  productRanking(query: PeriodQuery & { categoryId?: string } = {}): Observable<ProductRankingReportResponse> {
    return this.http.get<ProductRankingReportResponse>('/api/reports/product-ranking', { params: toParams(query) });
  }

  /** Without dates: the last 12 months. */
  costChanges(
    query: PeriodQuery & { categoryId?: string; supplierId?: string } = {},
  ): Observable<CostChangesReportResponse> {
    return this.http.get<CostChangesReportResponse>('/api/reports/cost-changes', { params: toParams(query) });
  }

  /** Without dates: the whole history. supplierId filters the purchases, not the comparison. */
  productCosts(
    productId: string,
    query: PeriodQuery & { supplierId?: string } = {},
  ): Observable<ProductCostHistoryResponse> {
    return this.http.get<ProductCostHistoryResponse>(`/api/reports/products/${productId}/costs`, {
      params: toParams(query),
    });
  }
}
