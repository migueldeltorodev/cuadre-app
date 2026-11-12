import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AllocationPlanResponse,
  CreateAllocationPlanRequest,
  CreateEnvelopeOpeningRequest,
  CreateEnvelopeRequest,
  CreateEnvelopeTransferRequest,
  CreateEnvelopeWithdrawalRequest,
  CreateTaxPaymentRequest,
  EnvelopeLedgerQuery,
  EnvelopeLedgerResponse,
  EnvelopeOperationQuery,
  EnvelopeOperationResponse,
  EnvelopeOperationSummaryResponse,
  EnvelopeOverviewResponse,
  EnvelopeResponse,
  ItemsResponse,
  PagedResponse,
  UpdateEnvelopeRequest,
} from './models';
import { toParams } from './query-params';

/** Thin HTTP wrappers for Profit First (slice 5). Routes mirror ApiEndpoints.cs. No logic here. */

@Injectable({ providedIn: 'root' })
export class EnvelopesApi {
  private readonly http = inject(HttpClient);

  getAll(): Observable<ItemsResponse<EnvelopeResponse>> {
    return this.http.get<ItemsResponse<EnvelopeResponse>>('/api/envelopes');
  }

  /** A user's own envelope: no % until a new plan gives it one. */
  create(request: CreateEnvelopeRequest): Observable<EnvelopeResponse> {
    return this.http.post<EnvelopeResponse>('/api/envelopes', request);
  }

  /** Rename or (de)activate. Only the user's own envelopes, empty and without a %, can be deactivated. */
  update(id: string, request: UpdateEnvelopeRequest): Observable<EnvelopeResponse> {
    return this.http.put<EnvelopeResponse>(`/api/envelopes/${id}`, request);
  }

  /** Envelopes × cash boxes at the end of a day. Without a date: today. */
  balances(date?: string): Observable<EnvelopeOverviewResponse> {
    return this.http.get<EnvelopeOverviewResponse>('/api/envelopes/balances', { params: toParams({ date }) });
  }

  movements(id: string, query: EnvelopeLedgerQuery = {}): Observable<EnvelopeLedgerResponse> {
    return this.http.get<EnvelopeLedgerResponse>(`/api/envelopes/${id}/movements`, { params: toParams(query) });
  }
}

@Injectable({ providedIn: 'root' })
export class AllocationPlansApi {
  private readonly http = inject(HttpClient);

  /** Newest first. */
  getAll(): Observable<ItemsResponse<AllocationPlanResponse>> {
    return this.http.get<ItemsResponse<AllocationPlanResponse>>('/api/allocation-plans');
  }

  /** Same start date as the latest plan replaces it; an earlier one is refused (422). */
  create(request: CreateAllocationPlanRequest): Observable<AllocationPlanResponse> {
    return this.http.post<AllocationPlanResponse>('/api/allocation-plans', request);
  }
}

/** Openings, moves between envelopes, withdrawals and tax payments. Never edited, only voided. */
@Injectable({ providedIn: 'root' })
export class EnvelopeOperationsApi {
  private readonly http = inject(HttpClient);

  getAll(query: EnvelopeOperationQuery = {}): Observable<PagedResponse<EnvelopeOperationSummaryResponse>> {
    return this.http.get<PagedResponse<EnvelopeOperationSummaryResponse>>('/api/envelope-operations', {
      params: toParams(query),
    });
  }

  get(id: string): Observable<EnvelopeOperationResponse> {
    return this.http.get<EnvelopeOperationResponse>(`/api/envelope-operations/${id}`);
  }

  createOpening(request: CreateEnvelopeOpeningRequest): Observable<EnvelopeOperationResponse> {
    return this.http.post<EnvelopeOperationResponse>('/api/envelope-operations/openings', request);
  }

  createTransfer(request: CreateEnvelopeTransferRequest): Observable<EnvelopeOperationResponse> {
    return this.http.post<EnvelopeOperationResponse>('/api/envelope-operations/transfers', request);
  }

  createWithdrawal(request: CreateEnvelopeWithdrawalRequest): Observable<EnvelopeOperationResponse> {
    return this.http.post<EnvelopeOperationResponse>('/api/envelope-operations/withdrawals', request);
  }

  createTaxPayment(request: CreateTaxPaymentRequest): Observable<EnvelopeOperationResponse> {
    return this.http.post<EnvelopeOperationResponse>('/api/envelope-operations/tax-payments', request);
  }

  void(id: string, reason: string): Observable<EnvelopeOperationResponse> {
    return this.http.post<EnvelopeOperationResponse>(`/api/envelope-operations/${id}/void`, { reason });
  }
}
