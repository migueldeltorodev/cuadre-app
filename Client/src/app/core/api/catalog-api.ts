import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CategoryResponse,
  CreateCategoryRequest,
  CreateProductRequest,
  CreateSupplierRequest,
  ItemsResponse,
  PagedResponse,
  PriceChangeResponse,
  PriceListCopyResponse,
  PriceListResponse,
  ProductQuery,
  ProductResponse,
  SupplierResponse,
  UnitResponse,
  UpdateCategoryRequest,
  UpdateProductRequest,
  UpdateSupplierRequest,
} from './models';

/** Thin HTTP wrappers for the catalog (slice 2). Routes mirror ApiEndpoints.cs. No logic here. */

@Injectable({ providedIn: 'root' })
export class CategoriesApi {
  private readonly http = inject(HttpClient);

  getAll(): Observable<ItemsResponse<CategoryResponse>> {
    return this.http.get<ItemsResponse<CategoryResponse>>('/api/categories');
  }

  create(request: CreateCategoryRequest): Observable<CategoryResponse> {
    return this.http.post<CategoryResponse>('/api/categories', request);
  }

  update(id: string, request: UpdateCategoryRequest): Observable<CategoryResponse> {
    return this.http.put<CategoryResponse>(`/api/categories/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`/api/categories/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class SuppliersApi {
  private readonly http = inject(HttpClient);

  getAll(): Observable<ItemsResponse<SupplierResponse>> {
    return this.http.get<ItemsResponse<SupplierResponse>>('/api/suppliers');
  }

  create(request: CreateSupplierRequest): Observable<SupplierResponse> {
    return this.http.post<SupplierResponse>('/api/suppliers', request);
  }

  update(id: string, request: UpdateSupplierRequest): Observable<SupplierResponse> {
    return this.http.put<SupplierResponse>(`/api/suppliers/${id}`, request);
  }
}

@Injectable({ providedIn: 'root' })
export class UnitsApi {
  private readonly http = inject(HttpClient);

  getAll(): Observable<ItemsResponse<UnitResponse>> {
    return this.http.get<ItemsResponse<UnitResponse>>('/api/units');
  }
}

@Injectable({ providedIn: 'root' })
export class ProductsApi {
  private readonly http = inject(HttpClient);

  getAll(query: ProductQuery = {}): Observable<PagedResponse<ProductResponse>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, value);
      }
    }
    return this.http.get<PagedResponse<ProductResponse>>('/api/products', { params });
  }

  get(id: string): Observable<ProductResponse> {
    return this.http.get<ProductResponse>(`/api/products/${id}`);
  }

  create(request: CreateProductRequest): Observable<ProductResponse> {
    return this.http.post<ProductResponse>('/api/products', request);
  }

  update(id: string, request: UpdateProductRequest): Observable<ProductResponse> {
    return this.http.put<ProductResponse>(`/api/products/${id}`, request);
  }

  nextCode(categoryId: string): Observable<{ code: string }> {
    return this.http.get<{ code: string }>('/api/products/next-code', { params: { categoryId } });
  }

  priceHistory(id: string): Observable<ItemsResponse<PriceChangeResponse>> {
    return this.http.get<ItemsResponse<PriceChangeResponse>>(`/api/products/${id}/price-history`);
  }
}

@Injectable({ providedIn: 'root' })
export class PriceListApi {
  private readonly http = inject(HttpClient);

  get(): Observable<PriceListResponse> {
    return this.http.get<PriceListResponse>('/api/price-list');
  }

  /** "Ya la copié al cartel". */
  markCopied(): Observable<PriceListCopyResponse> {
    return this.http.post<PriceListCopyResponse>('/api/price-list/copies', null);
  }
}
