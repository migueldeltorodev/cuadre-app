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
  PriceNoticeResponse,
  ProductPurchaseResponse,
  ProductQuery,
  ProductResponse,
  ResolvePriceNoticeRequest,
  SetSalePriceRequest,
  SupplierProductResponse,
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

  get(id: string): Observable<SupplierResponse> {
    return this.http.get<SupplierResponse>(`/api/suppliers/${id}`);
  }

  /** What we buy from this supplier, with the last landed cost (RF-PRV-02). */
  products(id: string): Observable<ItemsResponse<SupplierProductResponse>> {
    return this.http.get<ItemsResponse<SupplierProductResponse>>(`/api/suppliers/${id}/products`);
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

  purchases(id: string): Observable<ItemsResponse<ProductPurchaseResponse>> {
    return this.http.get<ItemsResponse<ProductPurchaseResponse>>(`/api/products/${id}/purchases`);
  }

  /** Only the price: accepting a suggestion (with purchaseId) or a quick manual change. */
  setSalePrice(id: string, request: SetSalePriceRequest): Observable<ProductResponse> {
    return this.http.put<ProductResponse>(`/api/products/${id}/sale-price`, request);
  }

  /** D-61: prices to look at (a new batch on sale, or the next one will not fit in the price). */
  priceNotices(): Observable<ItemsResponse<PriceNoticeResponse>> {
    return this.http.get<ItemsResponse<PriceNoticeResponse>>('/api/products/price-notices');
  }

  /** D-61: take the suggested price (from today) or keep the price. */
  resolvePriceNotice(id: string, request: ResolvePriceNoticeRequest): Observable<ProductResponse> {
    return this.http.post<ProductResponse>(`/api/products/${id}/price-notice`, request);
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
