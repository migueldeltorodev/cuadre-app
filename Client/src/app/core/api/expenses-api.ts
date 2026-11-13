import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ConfirmRecurringExpenseRequest,
  CreateExpenseCategoryRequest,
  CreateExpenseRequest,
  CreateRecurringExpenseRequest,
  DueExpenseResponse,
  ExpenseCategoryResponse,
  ExpenseQuery,
  ExpenseResponse,
  ExpensesResponse,
  ItemsResponse,
  RecurringExpenseResponse,
  UpdateExpenseCategoryRequest,
  UpdateRecurringExpenseRequest,
} from './models';
import { toParams } from './query-params';

/** Thin HTTP wrappers for Gastos (slice 7). Routes mirror ApiEndpoints.cs. No logic here. */

/** Never deleted: deactivated. */
@Injectable({ providedIn: 'root' })
export class ExpenseCategoriesApi {
  private readonly http = inject(HttpClient);

  /** Active first, then by name. */
  getAll(): Observable<ItemsResponse<ExpenseCategoryResponse>> {
    return this.http.get<ItemsResponse<ExpenseCategoryResponse>>('/api/expense-categories');
  }

  create(request: CreateExpenseCategoryRequest): Observable<ExpenseCategoryResponse> {
    return this.http.post<ExpenseCategoryResponse>('/api/expense-categories', request);
  }

  update(id: string, request: UpdateExpenseCategoryRequest): Observable<ExpenseCategoryResponse> {
    return this.http.put<ExpenseCategoryResponse>(`/api/expense-categories/${id}`, request);
  }
}

/** Saved = confirmed. Never edited, only voided. */
@Injectable({ providedIn: 'root' })
export class ExpensesApi {
  private readonly http = inject(HttpClient);

  getAll(query: ExpenseQuery = {}): Observable<ExpensesResponse> {
    return this.http.get<ExpensesResponse>('/api/expenses', { params: toParams(query) });
  }

  get(id: string): Observable<ExpenseResponse> {
    return this.http.get<ExpenseResponse>(`/api/expenses/${id}`);
  }

  create(request: CreateExpenseRequest): Observable<ExpenseResponse> {
    return this.http.post<ExpenseResponse>('/api/expenses', request);
  }

  void(id: string, reason: string): Observable<ExpenseResponse> {
    return this.http.post<ExpenseResponse>(`/api/expenses/${id}/void`, { reason });
  }
}

/** The templates, what is due, and paying or skipping an occurrence. */
@Injectable({ providedIn: 'root' })
export class RecurringExpensesApi {
  private readonly http = inject(HttpClient);

  /** Active first, then by name. */
  getAll(): Observable<ItemsResponse<RecurringExpenseResponse>> {
    return this.http.get<ItemsResponse<RecurringExpenseResponse>>('/api/recurring-expenses');
  }

  create(request: CreateRecurringExpenseRequest): Observable<RecurringExpenseResponse> {
    return this.http.post<RecurringExpenseResponse>('/api/recurring-expenses', request);
  }

  update(id: string, request: UpdateRecurringExpenseRequest): Observable<RecurringExpenseResponse> {
    return this.http.put<RecurringExpenseResponse>(`/api/recurring-expenses/${id}`, request);
  }

  /** Oldest first. */
  due(): Observable<ItemsResponse<DueExpenseResponse>> {
    return this.http.get<ItemsResponse<DueExpenseResponse>>('/api/recurring-expenses/due');
  }

  /** Creates the expense of that occurrence. */
  confirm(id: string, request: ConfirmRecurringExpenseRequest): Observable<ExpenseResponse> {
    return this.http.post<ExpenseResponse>(`/api/recurring-expenses/${id}/confirm`, request);
  }

  /** No money moves; that occurrence stops being proposed. */
  skip(id: string, periodStart: string): Observable<void> {
    return this.http.post<void>(`/api/recurring-expenses/${id}/skip`, { periodStart });
  }
}
