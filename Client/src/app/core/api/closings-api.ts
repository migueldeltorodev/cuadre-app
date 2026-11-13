import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CashDiscrepancyQuery,
  CashDiscrepancyResponse,
  CreateDailyClosingRequest,
  CreateEmployeeRequest,
  CreateStaffPaySchemeRequest,
  DailyClosingDraftResponse,
  DailyClosingQuery,
  DailyClosingResponse,
  DailyClosingSummaryResponse,
  EmployeeResponse,
  ItemsResponse,
  PagedResponse,
  PendingClosingDaysResponse,
  PendingShortagesResponse,
  ProfitFirstEvaluationResponse,
  RecoverShortageRequest,
  StaffPaySchemeResponse,
  UpdateEmployeeRequest,
} from './models';
import { toParams } from './query-params';

/** Thin HTTP wrappers for the daily closing (slice 6). Routes mirror ApiEndpoints.cs. No logic here. */

@Injectable({ providedIn: 'root' })
export class DailyClosingsApi {
  private readonly http = inject(HttpClient);

  /** The "planilla" and everything the wizard needs. Without a date: today (the server's). */
  draft(date?: string): Observable<DailyClosingDraftResponse> {
    return this.http.get<DailyClosingDraftResponse>('/api/daily-closings/draft', { params: toParams({ date }) });
  }

  /** The whole closing computed, NOT saved. */
  preview(request: CreateDailyClosingRequest): Observable<DailyClosingResponse> {
    return this.http.post<DailyClosingResponse>('/api/daily-closings/preview', request);
  }

  create(request: CreateDailyClosingRequest): Observable<DailyClosingResponse> {
    return this.http.post<DailyClosingResponse>('/api/daily-closings', request);
  }

  /** Newest first. */
  getAll(query: DailyClosingQuery = {}): Observable<PagedResponse<DailyClosingSummaryResponse>> {
    return this.http.get<PagedResponse<DailyClosingSummaryResponse>>('/api/daily-closings', {
      params: toParams(query),
    });
  }

  get(id: string): Observable<DailyClosingResponse> {
    return this.http.get<DailyClosingResponse>(`/api/daily-closings/${id}`);
  }

  /** Only the latest closed day; its movements are reversed on that same day. */
  reopen(id: string, reason: string): Observable<DailyClosingResponse> {
    return this.http.post<DailyClosingResponse>(`/api/daily-closings/${id}/reopen`, { reason });
  }

  /** RF-CIE-09: business days already over (up to yesterday) still not closed. */
  pendingDays(): Observable<PendingClosingDaysResponse> {
    return this.http.get<PendingClosingDaysResponse>('/api/daily-closings/pending');
  }
}

/** Shortages and surpluses of the closings, and the recoveries of the shortages (slice 6b). */
@Injectable({ providedIn: 'root' })
export class CashDiscrepanciesApi {
  private readonly http = inject(HttpClient);

  /** Newest first. Those of reopened closings are never listed. */
  getAll(query: CashDiscrepancyQuery = {}): Observable<PagedResponse<CashDiscrepancyResponse>> {
    return this.http.get<PagedResponse<CashDiscrepancyResponse>>('/api/cash-discrepancies', {
      params: toParams(query),
    });
  }

  /** With its recoveries. */
  get(id: string): Observable<CashDiscrepancyResponse> {
    return this.http.get<CashDiscrepancyResponse>(`/api/cash-discrepancies/${id}`);
  }

  pendingSummary(): Observable<PendingShortagesResponse> {
    return this.http.get<PendingShortagesResponse>('/api/cash-discrepancies/pending-summary');
  }

  /** Returns the shortage updated. */
  recover(id: string, request: RecoverShortageRequest): Observable<CashDiscrepancyResponse> {
    return this.http.post<CashDiscrepancyResponse>(`/api/cash-discrepancies/${id}/recoveries`, request);
  }

  writeOff(id: string, note: string): Observable<CashDiscrepancyResponse> {
    return this.http.post<CashDiscrepancyResponse>(`/api/cash-discrepancies/${id}/write-off`, { note });
  }

  /** Reversals dated today. Returns the shortage of that recovery. */
  voidRecovery(recoveryId: string, reason: string): Observable<CashDiscrepancyResponse> {
    return this.http.post<CashDiscrepancyResponse>(`/api/shortage-recoveries/${recoveryId}/void`, { reason });
  }
}

/** RF-PF-10: A1, A2, A3 of a period and the real % of each envelope. */
@Injectable({ providedIn: 'root' })
export class ProfitFirstApi {
  private readonly http = inject(HttpClient);

  /** Without dates: this month up to today. */
  evaluation(from?: string, to?: string): Observable<ProfitFirstEvaluationResponse> {
    return this.http.get<ProfitFirstEvaluationResponse>('/api/profit-first/evaluation', {
      params: toParams({ from, to }),
    });
  }
}

/** Configuración › Personal: employees and the pay of the post. */
@Injectable({ providedIn: 'root' })
export class StaffApi {
  private readonly http = inject(HttpClient);

  /** Active first, then by name. */
  employees(): Observable<ItemsResponse<EmployeeResponse>> {
    return this.http.get<ItemsResponse<EmployeeResponse>>('/api/employees');
  }

  createEmployee(request: CreateEmployeeRequest): Observable<EmployeeResponse> {
    return this.http.post<EmployeeResponse>('/api/employees', request);
  }

  updateEmployee(id: string, request: UpdateEmployeeRequest): Observable<EmployeeResponse> {
    return this.http.put<EmployeeResponse>(`/api/employees/${id}`, request);
  }

  /** Newest first: the first one is in force from its date on. */
  paySchemes(): Observable<ItemsResponse<StaffPaySchemeResponse>> {
    return this.http.get<ItemsResponse<StaffPaySchemeResponse>>('/api/staff-pay-schemes');
  }

  /** Same start date as the latest replaces it; an earlier or closed date is refused (422). */
  createPayScheme(request: CreateStaffPaySchemeRequest): Observable<StaffPaySchemeResponse> {
    return this.http.post<StaffPaySchemeResponse>('/api/staff-pay-schemes', request);
  }
}
