/**
 * Mirrors Cuadre.Contracts (requests/responses). Later these are GENERATED from /openapi/v1.json.
 * Money is ALWAYS integer cents (`...Cents`), percentages integer basis points (`...Bps`, 30% = 3000).
 * Dates are 'yyyy-MM-dd' strings (business days); timestamps are ISO UTC strings.
 */

export type CurrencyCode = 'CUP' | 'USD';
export type CashBoxType = 'Cash' | 'Bank';
export type Weekday = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday';

// ---- Business settings ----

export interface BusinessSettingsResponse {
  name: string;
  address: string | null;
  closedDays: Weekday[];
  cashDifferenceThresholdCents: number;
  exchangeBufferCents: number;
  defaultMarkupBps: number;
  priceRoundingStepCents: number;
  updatedAt: string;
}

export type UpdateBusinessSettingsRequest = Omit<BusinessSettingsResponse, 'updatedAt'>;

// ---- Cash boxes ----

export interface CashBoxResponse {
  id: string;
  name: string;
  currency: CurrencyCode;
  kind: CashBoxType;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateCashBoxRequest {
  name: string;
  currency: CurrencyCode;
  kind: CashBoxType;
}

export interface UpdateCashBoxRequest {
  name: string;
  isActive: boolean;
}

// ---- Payment methods ----

export interface PaymentMethodResponse {
  id: string;
  name: string;
  cashBoxId: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreatePaymentMethodRequest {
  name: string;
  cashBoxId: string;
}

export interface UpdatePaymentMethodRequest extends CreatePaymentMethodRequest {
  isActive: boolean;
}

// ---- Exchange rates ----

export interface ExchangeRateResponse {
  date: string;
  cupPerUsdCents: number;
  updatedAt: string;
}

export interface CurrentExchangeRateResponse {
  today: string;
  isMissing: boolean;
  latest: ExchangeRateResponse | null;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
  hasNextPage: boolean;
}

export interface ItemsResponse<T> {
  items: T[];
}

// ---- Catalog ----
// Quantities in thousandths of the stock unit (`...Milli`: 2.5 lb = 2500).

export type UnitCode = 'Each' | 'Pound' | 'Kilogram' | 'Liter' | 'Sack' | 'Box' | 'Pack' | 'Dozen' | 'Bag';
export type PriceChangeKind = 'Manual' | 'Purchase';

export interface UnitResponse {
  code: UnitCode;
  allowsDecimals: boolean;
}

export interface CategoryResponse {
  id: string;
  parentId: string | null;
  name: string;
  codePrefix: string | null;
  productCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreateCategoryRequest {
  name: string;
  parentId: string | null;
  codePrefix: string | null;
}

export interface UpdateCategoryRequest {
  name: string;
  codePrefix: string | null;
}

export interface SupplierResponse {
  id: string;
  name: string;
  contactName: string | null;
  phone: string | null;
  notes: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateSupplierRequest {
  name: string;
  contactName: string | null;
  phone: string | null;
  notes: string | null;
}

export interface UpdateSupplierRequest extends CreateSupplierRequest {
  isActive: boolean;
}

export interface ProductResponse {
  id: string;
  code: string;
  barcode: string | null;
  name: string;
  brand: string | null;
  presentation: string | null;
  categoryId: string;
  subcategoryId: string | null;
  usualSupplierId: string | null;
  purchaseUnit: UnitCode;
  stockUnit: UnitCode;
  conversionFactorMilli: number;
  allowsDecimals: boolean;
  salePriceCents: number | null;
  isPriceChangedSinceListed: boolean;
  minStockMilli: number;
  shrinkageToleranceBps: number;
  exchangeBufferCents: number | null;
  markupBps: number | null;
  isPerishable: boolean;
  isActive: boolean;
  notes: string | null;
  createdAt: string;
  updatedAt: string;
}

interface ProductRequestBase {
  barcode: string | null;
  name: string;
  brand: string | null;
  presentation: string | null;
  categoryId: string;
  subcategoryId: string | null;
  usualSupplierId: string | null;
  purchaseUnit: UnitCode;
  stockUnit: UnitCode;
  conversionFactorMilli: number;
  salePriceCents: number | null;
  minStockMilli: number;
  shrinkageToleranceBps: number;
  exchangeBufferCents: number | null;
  markupBps: number | null;
  isPerishable: boolean;
  notes: string | null;
}

export interface CreateProductRequest extends ProductRequestBase {
  /** Null/blank = generated from the category prefix. */
  code: string | null;
}

export interface UpdateProductRequest extends ProductRequestBase {
  code: string;
  isActive: boolean;
}

export interface ProductQuery {
  search?: string;
  categoryId?: string;
  includeInactive?: boolean;
  page?: number;
  pageSize?: number;
}

export interface PriceChangeResponse {
  id: string;
  priceCents: number;
  previousPriceCents: number | null;
  effectiveDate: string;
  reason: PriceChangeKind;
  createdAt: string;
}

export interface PriceListEntryResponse {
  productId: string;
  code: string;
  name: string;
  brand: string | null;
  presentation: string | null;
  stockUnit: UnitCode;
  subcategoryName: string | null;
  salePriceCents: number | null;
  listedPriceCents: number | null;
  isChanged: boolean;
}

export interface PriceListGroupResponse {
  categoryId: string;
  categoryName: string;
  entries: PriceListEntryResponse[];
}

export interface PriceListCopyResponse {
  id: string;
  copiedAt: string;
  productCount: number;
  changedCount: number;
}

export interface PriceListResponse {
  lastCopy: PriceListCopyResponse | null;
  changedCount: number;
  groups: PriceListGroupResponse[];
}
