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
  /** All locations; can be negative (sold before the purchase was typed). */
  stockMilli: number;
  lastPurchaseDate: string | null;
  /** Null until the first purchase. */
  cost: ProductCostResponse | null;
  createdAt: string;
  updatedAt: string;
}

/** Unit costs in MILLIONTHS (`...Micros`): 0.62 USD/lb = 620000. Per STOCK unit. */
export interface ProductCostResponse {
  avgCostUsdMicros: number;
  avgCostCupMicros: number;
  averageRateCents: number;
  effectiveBufferCents: number;
  effectiveMarkupBps: number;
  suggestedPriceCents: number;
  marginBps: number | null;
  todayRateCents: number | null;
  isBufferExceeded: boolean;
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

// ---- Purchases (slice 3) ----
// Purchase money in cents of the PURCHASE currency; quantities in thousandths of the PURCHASE unit.

export type PurchaseState = 'Confirmed' | 'Voided';
export type LocationType = 'Storage' | 'Counter';

export interface LocationResponse {
  id: string;
  name: string;
  kind: LocationType;
  isActive: boolean;
}

export interface CreatePurchaseLineRequest {
  productId: string;
  quantityMilli: number;
  unitCostCents: number;
  /** Null = the factor of the product. */
  conversionFactorMilli: number | null;
}

export interface CreatePurchaseChargeRequest {
  description: string;
  currency: CurrencyCode;
  amountCents: number;
  cashBoxId: string;
}

export interface CreatePurchaseRequest {
  date: string;
  supplierId: string;
  currency: CurrencyCode;
  rateCupPerUsdCents: number;
  /** Null = Almacén. */
  locationId: string | null;
  cashBoxId: string;
  notes: string | null;
  lines: CreatePurchaseLineRequest[];
  charges: CreatePurchaseChargeRequest[];
}

export interface PurchaseLineResponse {
  id: string;
  lineNo: number;
  productId: string;
  productName: string;
  productCode: string;
  purchaseUnit: UnitCode;
  stockUnit: UnitCode;
  quantityMilli: number;
  conversionFactorMilli: number;
  stockQuantityMilli: number;
  unitCostCents: number;
  lineTotalCents: number;
  allocatedChargesUsdMicros: number;
  landedUnitCostUsdMicros: number;
  landedUnitCostCupMicros: number;
}

export interface PurchaseChargeResponse {
  id: string;
  description: string;
  currency: CurrencyCode;
  amountCents: number;
  cashBoxId: string;
}

export interface PriceSuggestionResponse {
  productId: string;
  productName: string;
  currentPriceCents: number | null;
  suggestedPriceCents: number;
}

export interface PurchaseResponse {
  id: string;
  number: number;
  date: string;
  supplierId: string;
  supplierName: string;
  currency: CurrencyCode;
  rateCupPerUsdCents: number;
  locationId: string;
  cashBoxId: string;
  goodsTotalCents: number;
  notes: string | null;
  status: PurchaseState;
  voidReason: string | null;
  voidedAt: string | null;
  lines: PurchaseLineResponse[];
  charges: PurchaseChargeResponse[];
  /** Only filled in the response of the creation. */
  priceSuggestions: PriceSuggestionResponse[];
  createdAt: string;
  updatedAt: string;
}

export interface PurchaseSummaryResponse {
  id: string;
  number: number;
  date: string;
  supplierId: string;
  supplierName: string;
  currency: CurrencyCode;
  goodsTotalCents: number;
  lineCount: number;
  productNames: string;
  status: PurchaseState;
}

export interface PurchaseQuery {
  from?: string;
  to?: string;
  supplierId?: string;
  productId?: string;
  categoryId?: string;
  includeVoided?: boolean;
  page?: number;
  pageSize?: number;
}

export interface ProductPurchaseResponse {
  purchaseId: string;
  purchaseNumber: number;
  date: string;
  supplierName: string;
  currency: CurrencyCode;
  rateCupPerUsdCents: number;
  quantityMilli: number;
  purchaseUnit: UnitCode;
  stockQuantityMilli: number;
  stockUnit: UnitCode;
  unitCostCents: number;
  landedUnitCostUsdMicros: number;
  landedUnitCostCupMicros: number;
  status: PurchaseState;
}

export interface SupplierProductResponse {
  productId: string;
  productName: string;
  productCode: string;
  stockUnit: UnitCode;
  lastPurchaseDate: string;
  lastLandedUnitCostUsdMicros: number;
  purchaseCount: number;
}

export interface SetSalePriceRequest {
  salePriceCents: number;
  /** Set when accepting a suggestion of that purchase: the history says "Por una compra". */
  purchaseId: string | null;
}

// ---- Inventory (slice 4) ----
// Quantities in thousandths of the product's STOCK unit; unit costs in millionths; money in CUP cents.

export type StockOperationType = 'Transfer' | 'Exit' | 'Count';
export type StockOperationState = 'Confirmed' | 'Voided';
export type ExitReasonCode = 'Shrinkage' | 'Expired' | 'Damaged' | 'HouseConsumption' | 'Adjustment';
export type StockMovementType =
  | 'Purchase'
  | 'PurchaseVoid'
  | 'TransferOut'
  | 'TransferIn'
  | 'Exit'
  | 'CountAdjustment'
  | 'OperationVoid';

export interface CreateLocationRequest {
  name: string;
  kind: LocationType;
}

/** The kind cannot change. */
export interface UpdateLocationRequest {
  name: string;
  isActive: boolean;
}

export interface StockOperationLineRequest {
  productId: string;
  /** Transfer/exit: what moves (> 0). Count: what was found (0 allowed). */
  quantityMilli: number;
  /** True = typed in purchase units (sacos); the server converts with the product's factor. */
  inPurchaseUnits: boolean;
}

export interface CreateTransferRequest {
  date: string;
  fromLocationId: string;
  toLocationId: string;
  notes: string | null;
  lines: StockOperationLineRequest[];
}

export interface CreateExitRequest {
  date: string;
  locationId: string;
  reason: ExitReasonCode;
  notes: string | null;
  lines: StockOperationLineRequest[];
}

export interface CreateCountRequest {
  date: string;
  locationId: string;
  notes: string | null;
  lines: StockOperationLineRequest[];
}

export interface StockOperationLineResponse {
  id: string;
  lineNo: number;
  productId: string;
  productName: string;
  productCode: string;
  purchaseUnit: UnitCode;
  stockUnit: UnitCode;
  enteredQuantityMilli: number;
  enteredInPurchaseUnits: boolean;
  conversionFactorMilli: number;
  /** Transfer/exit: moved. Count: counted − expected (negative = shortage). */
  quantityMilli: number;
  expectedMilli: number | null;
  countedMilli: number | null;
  unitCostUsdMicros: number | null;
  unitCostCupMicros: number | null;
  /** Historical CUP value of quantityMilli; null without cost. */
  valueCupCents: number | null;
}

export interface NegativeStockWarningResponse {
  productId: string;
  productName: string;
  stockUnit: UnitCode;
  locationId: string;
  locationName: string;
  balanceMilli: number;
}

export interface StockOperationResponse {
  id: string;
  number: number;
  kind: StockOperationType;
  date: string;
  locationId: string;
  locationName: string;
  toLocationId: string | null;
  toLocationName: string | null;
  exitReason: ExitReasonCode | null;
  notes: string | null;
  status: StockOperationState;
  voidReason: string | null;
  voidedAt: string | null;
  lines: StockOperationLineResponse[];
  /** Only right after creating it: locations left below zero. */
  warnings: NegativeStockWarningResponse[];
  createdAt: string;
  updatedAt: string;
}

export interface StockOperationSummaryResponse {
  id: string;
  number: number;
  kind: StockOperationType;
  date: string;
  locationId: string;
  locationName: string;
  toLocationId: string | null;
  toLocationName: string | null;
  exitReason: ExitReasonCode | null;
  lineCount: number;
  productNames: string;
  /** Exit: value of what left. Count: shortage minus surplus (positive = loss). Null for transfers. */
  valueCupCents: number | null;
  status: StockOperationState;
}

export interface StockOperationQuery {
  kind?: StockOperationType;
  from?: string;
  to?: string;
  locationId?: string;
  productId?: string;
  includeVoided?: boolean;
  page?: number;
  pageSize?: number;
}

export interface StockLocationResponse {
  id: string;
  name: string;
  kind: LocationType;
  lastCountDate: string | null;
}

export interface LocationQuantityResponse {
  locationId: string;
  quantityMilli: number;
}

export interface ProductStockResponse {
  productId: string;
  code: string;
  name: string;
  categoryId: string;
  purchaseUnit: UnitCode;
  stockUnit: UnitCode;
  conversionFactorMilli: number;
  minStockMilli: number;
  isActive: boolean;
  /** Only locations with a non-zero balance. */
  locations: LocationQuantityResponse[];
  totalMilli: number;
  isLow: boolean;
}

export interface StockResponse {
  /** The columns of the table, Counter first. */
  locations: StockLocationResponse[];
  items: ProductStockResponse[];
}

export interface StockQuery {
  search?: string;
  categoryId?: string;
  lowOnly?: boolean;
  includeInactive?: boolean;
}

export interface StockMovementResponse {
  id: string;
  productId: string;
  productName: string;
  stockUnit: UnitCode;
  locationId: string;
  locationName: string;
  date: string;
  kind: StockMovementType;
  quantityMilli: number;
  unitCostUsdMicros: number | null;
  unitCostCupMicros: number | null;
  purchaseId: string | null;
  purchaseNumber: number | null;
  operationId: string | null;
  operationNumber: number | null;
  exitReason: ExitReasonCode | null;
  /** Its document was voided later (the row and its reversal both stay). */
  isVoided: boolean;
  createdAt: string;
}

export interface StockMovementsResponse {
  date: string;
  items: StockMovementResponse[];
}

export interface KardexEntryResponse {
  movement: StockMovementResponse;
  locationBalanceMilli: number;
  totalBalanceMilli: number;
}

export interface KardexResponse {
  productId: string;
  productCode: string;
  productName: string;
  purchaseUnit: UnitCode;
  stockUnit: UnitCode;
  conversionFactorMilli: number;
  from: string | null;
  to: string | null;
  openingBalances: LocationQuantityResponse[];
  /** Oldest first. */
  entries: KardexEntryResponse[];
  closingBalances: LocationQuantityResponse[];
}

export interface KardexQuery {
  from?: string;
  to?: string;
  locationId?: string;
}

export interface ShoppingListItemResponse {
  productId: string;
  code: string;
  name: string;
  purchaseUnit: UnitCode;
  stockUnit: UnitCode;
  conversionFactorMilli: number;
  stockMilli: number;
  minStockMilli: number;
  missingMilli: number;
  /** In PURCHASE units, rounded up to whole ones. */
  suggestedPurchaseQuantityMilli: number;
  usualSupplierId: string | null;
  usualSupplierName: string | null;
  lastUnitCostUsdMicros: number | null;
  /** Rough estimate at the last landed cost, USD cents. */
  estimatedUsdCents: number | null;
}
