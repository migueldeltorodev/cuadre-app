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
