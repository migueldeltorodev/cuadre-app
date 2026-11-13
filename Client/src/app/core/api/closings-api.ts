import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
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
