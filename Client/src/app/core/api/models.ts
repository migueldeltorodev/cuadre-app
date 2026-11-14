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
  /** Typical shelf life in days (RF-PRD-10); only perishables. */
  shelfLifeDays: number | null;
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

/**
 * Unit costs in MILLIONTHS (`...Micros`): 0.62 USD/lb = 620000. Per STOCK unit.
 * D-52: the suggested price, the margin and the colchón alert use the LAST purchase's cost and rate.
 * D-60: what leaves the shelf is costed FIFO; `currentCost...` is the batch being sold now.
 */
export interface ProductCostResponse {
  avgCostUsdMicros: number;
  avgCostCupMicros: number;
  lastCostUsdMicros: number;
  lastCostCupMicros: number;
  /** D-60: the batch being sold now (the oldest with stock; with an empty shelf, the newest). */
  currentCostUsdMicros: number;
  currentCostCupMicros: number;
  /** The last purchase's rate, CUP cents per USD. */
  lastRateCents: number;
  /** D-61: the rate of the batch being sold, base of the suggested price and the colchón alert. */
  currentRateCents: number;
  effectiveBufferCents: number;
  effectiveMarkupBps: number;
  /** D-61: from the batch being sold. */
  suggestedPriceCents: number;
  /** D-61: what the newest batch (bought, maybe still waiting) would need. */
  newestSuggestedPriceCents: number;
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
  shelfLifeDays: number | null;
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
  | 'OperationVoid'
  | 'Sale'
  | 'ClosingVoid';

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
  closingId: string | null;
  closingDate: string | null;
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

// ---- Profit First envelopes (slice 5) ----
// Money in cents of the CASH BOX's currency (never added across currencies); percentages in basis points.

export type EnvelopeType = 'Profit' | 'OwnerPay' | 'Tax' | 'Operations' | 'Replenishment' | 'Custom';
export type EnvelopeOperationType = 'Opening' | 'Transfer' | 'Withdrawal' | 'TaxPayment';
export type EnvelopeOperationState = 'Confirmed' | 'Voided';
export type CashMovementType =
  | 'Purchase'
  | 'PurchaseCharge'
  | 'PurchaseVoid'
  | 'Opening'
  | 'EnvelopeTransfer'
  | 'Withdrawal'
  | 'TaxPayment'
  | 'HouseConsumption'
  | 'EnvelopeOperationVoid'
  | 'StockOperationVoid'
  | 'Collection'
  | 'Allocation'
  | 'StaffPay'
  | 'ClosingVoid'
  | 'ShortageRecovery'
  | 'ShortageRecoveryVoid'
  | 'Expense'
  | 'ExpenseVoid';

export interface EnvelopeResponse {
  id: string;
  name: string;
  role: EnvelopeType;
  sortOrder: number;
  isActive: boolean;
  /** False only for Reposición: it receives A2, not a %. */
  takesPercent: boolean;
}

export interface CreateEnvelopeRequest {
  name: string;
}

export interface UpdateEnvelopeRequest {
  name: string;
  isActive: boolean;
}

export interface AllocationPlanItemResponse {
  envelopeId: string;
  envelopeName: string;
  percentBps: number;
}

export interface AllocationPlanResponse {
  id: string;
  /** '2000-01-01' = the first plan, "since always". */
  effectiveFrom: string;
  notes: string | null;
  items: AllocationPlanItemResponse[];
  createdAt: string;
  updatedAt: string;
}

export interface CreateAllocationPlanRequest {
  effectiveFrom: string;
  notes?: string | null;
  items: { envelopeId: string; percentBps: number }[];
}

export interface EnvelopeBoxBalanceResponse {
  cashBoxId: string;
  amountCents: number;
}

export interface EnvelopeCashBoxResponse {
  id: string;
  name: string;
  currency: CurrencyCode;
  /** Bank = "plata atrapada". */
  kind: CashBoxType;
  isActive: boolean;
  totalCents: number;
}

export interface EnvelopeBalanceResponse {
  envelope: EnvelopeResponse;
  /** Null for Reposición. */
  percentBps: number | null;
  /** Only the boxes where it holds something. */
  balances: EnvelopeBoxBalanceResponse[];
}

export interface EnvelopeOverviewResponse {
  date: string;
  cashBoxes: EnvelopeCashBoxResponse[];
  envelopes: EnvelopeBalanceResponse[];
  currentPlan: AllocationPlanResponse | null;
  /** What Reposición still owes the other envelopes at the end of that day (RN-10). */
  internalDebtCents: number;
}

export interface CashMovementResponse {
  id: string;
  cashBoxId: string;
  cashBoxName: string;
  currency: CurrencyCode;
  envelopeId: string;
  envelopeName: string;
  date: string;
  kind: CashMovementType;
  amountCents: number;
  description: string;
  purchaseId: string | null;
  purchaseNumber: number | null;
  envelopeOperationId: string | null;
  envelopeOperationNumber: number | null;
  stockOperationId: string | null;
  stockOperationNumber: number | null;
  closingId: string | null;
  closingDate: string | null;
  /** A shortage recovery, and the closing where that shortage came out. */
  recoveryId: string | null;
  recoveryClosingId: string | null;
  /** A gasto (slice 7). */
  expenseId: string | null;
  expenseNumber: number | null;
  isVoided: boolean;
  createdAt: string;
}

export interface EnvelopeLedgerEntryResponse {
  movement: CashMovementResponse;
  boxBalanceCents: number;
}

export interface EnvelopeLedgerResponse {
  envelope: EnvelopeResponse;
  from: string | null;
  to: string | null;
  openingBalances: EnvelopeBoxBalanceResponse[];
  /** Oldest first. */
  entries: EnvelopeLedgerEntryResponse[];
  closingBalances: EnvelopeBoxBalanceResponse[];
}

export interface EnvelopeLedgerQuery {
  from?: string;
  to?: string;
  cashBoxId?: string;
}

export interface CreateEnvelopeOpeningRequest {
  date: string;
  notes?: string | null;
  targets: { cashBoxId: string; envelopeId: string; amountCents: number }[];
}

export interface CreateEnvelopeTransferRequest {
  date: string;
  cashBoxId: string;
  fromEnvelopeId: string;
  toEnvelopeId: string;
  amountCents: number;
  notes?: string | null;
}

export interface CreateEnvelopeWithdrawalRequest {
  date: string;
  cashBoxId: string;
  envelopeId: string;
  amountCents: number;
  notes?: string | null;
}

export interface CreateTaxPaymentRequest {
  date: string;
  cashBoxId: string;
  amountCents: number;
  concept: string;
  receipt?: string | null;
  notes?: string | null;
}

export interface EnvelopeOperationLineResponse {
  id: string;
  lineNo: number;
  cashBoxId: string;
  cashBoxName: string;
  currency: CurrencyCode;
  envelopeId: string;
  envelopeName: string;
  /** Signed. Opening: target − balance it had. */
  amountCents: number;
  targetCents: number | null;
}

export interface NegativeEnvelopeWarningResponse {
  envelopeId: string;
  envelopeName: string;
  cashBoxId: string;
  cashBoxName: string;
  currency: CurrencyCode;
  balanceCents: number;
}

export interface EnvelopeOperationResponse {
  id: string;
  number: number;
  kind: EnvelopeOperationType;
  date: string;
  cashBoxId: string | null;
  cashBoxName: string | null;
  currency: CurrencyCode | null;
  envelopeId: string | null;
  envelopeName: string | null;
  toEnvelopeId: string | null;
  toEnvelopeName: string | null;
  amountCents: number | null;
  concept: string | null;
  receipt: string | null;
  notes: string | null;
  status: EnvelopeOperationState;
  voidReason: string | null;
  voidedAt: string | null;
  lines: EnvelopeOperationLineResponse[];
  /** Only right after creating it. */
  warnings: NegativeEnvelopeWarningResponse[];
  createdAt: string;
  updatedAt: string;
}

export interface EnvelopeOperationSummaryResponse {
  id: string;
  number: number;
  kind: EnvelopeOperationType;
  date: string;
  cashBoxId: string | null;
  cashBoxName: string | null;
  currency: CurrencyCode | null;
  envelopeId: string | null;
  envelopeName: string | null;
  toEnvelopeId: string | null;
  toEnvelopeName: string | null;
  amountCents: number | null;
  concept: string | null;
  lineCount: number;
  status: EnvelopeOperationState;
}

export interface EnvelopeOperationQuery {
  kind?: EnvelopeOperationType;
  from?: string;
  to?: string;
  envelopeId?: string;
  includeVoided?: boolean;
  page?: number;
  pageSize?: number;
}

// ---- Staff (slice 6) ----

export interface EmployeeResponse {
  id: string;
  name: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateEmployeeRequest {
  name: string;
}

export interface UpdateEmployeeRequest {
  name: string;
  isActive: boolean;
}

export interface StaffPaySchemeResponse {
  id: string;
  effectiveFrom: string;
  dailyFixedCents: number;
  /** % of A1 in basis points (1% = 100). */
  percentOfSalesBps: number;
  notes: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateStaffPaySchemeRequest {
  effectiveFrom: string;
  dailyFixedCents: number;
  percentOfSalesBps: number;
  notes?: string | null;
}

// ---- Daily closing (slice 6) ----
// Money in CUP cents; quantities in thousandths of the stock unit.

export type DailyClosingState = 'Confirmed' | 'Voided';
export type CashDiscrepancyType = 'Shortage' | 'Surplus';
export type CashDiscrepancyState = 'Pending' | 'Recorded' | 'Voided' | 'Recovered' | 'Lost';
export type ShortageRecoveryState = 'Confirmed' | 'Voided';

export interface ClosingCounterLineResponse {
  productId: string;
  code: string;
  name: string;
  stockUnit: UnitCode;
  allowsDecimals: boolean;
  locationId: string;
  locationName: string;
  openingMilli: number;
  inMilli: number;
  outMilli: number;
  /** Opening + in − out. */
  expectedMilli: number;
  /** Null = never priced. */
  unitPriceCents: number | null;
  toleranceBps: number;
}

export interface ClosingPaymentMethodResponse {
  id: string;
  name: string;
  cashBoxId: string;
  cashBoxName: string;
  cashBoxKind: CashBoxType;
  isTransfer: boolean;
}

export interface DailyClosingDraftResponse {
  date: string;
  /** A day the business usually does not open (warning only). */
  isClosedDay: boolean;
  lastClosedDate: string | null;
  existingClosingId: string | null;
  isExchangeRateMissing: boolean;
  /** Why it cannot be closed. Null = it can. */
  problem: string | null;
  canClose: boolean;
  lines: ClosingCounterLineResponse[];
  paymentMethods: ClosingPaymentMethodResponse[];
  payScheme: StaffPaySchemeResponse;
  debtBeforeCents: number;
  thresholdCents: number;
}

export interface ClosingCountRequest {
  productId: string;
  locationId: string;
  countedMilli: number;
}

export interface ClosingCollectionRequest {
  paymentMethodId: string;
  amountCents: number;
}

/** Used by both the preview and the close. */
export interface CreateDailyClosingRequest {
  date: string;
  employeeId?: string | null;
  notes?: string | null;
  counts: ClosingCountRequest[];
  collections: ClosingCollectionRequest[];
}

export interface DailyClosingLineResponse {
  id: string;
  lineNo: number;
  productId: string;
  productCode: string;
  productName: string;
  locationId: string;
  locationName: string;
  stockUnit: UnitCode;
  openingMilli: number;
  inMilli: number;
  outMilli: number;
  expectedMilli: number;
  countedMilli: number;
  /** Negative = more was found than expected. */
  soldMilli: number;
  unitPriceCents: number | null;
  salesCents: number;
  toleranceBps: number;
  toleranceCents: number;
  /** A2 of the line: FIFO cost + the colchón. */
  costCents: number;
  /** The colchón part of costCents (D-60); 0 on closings made before it. */
  bufferCents: number;
}

export interface DailyClosingCollectionResponse {
  paymentMethodId: string;
  paymentMethodName: string;
  cashBoxId: string;
  cashBoxName: string;
  isTransfer: boolean;
  amountCents: number;
}

export interface DailyClosingAllocationResponse {
  envelopeId: string;
  envelopeName: string;
  role: EnvelopeType;
  /** Null for Reposición. */
  percentBps: number | null;
  amountCents: number;
  /** Cash to put in the envelope (Operación: minus the staff pay already handed over). */
  cashCents: number;
  /** Already in the bank (only Reposición). */
  bankCents: number;
}

export interface ShortageRecoveryResponse {
  id: string;
  date: string;
  cashBoxId: string;
  cashBoxName: string;
  amountCents: number;
  note: string | null;
  status: ShortageRecoveryState;
  voidReason: string | null;
  voidedAt: string | null;
  createdAt: string;
}

export interface CashDiscrepancyResponse {
  id: string;
  closingId: string;
  /** The day of the closing where it came out. */
  date: string;
  kind: CashDiscrepancyType;
  amountCents: number;
  employeeId: string | null;
  employeeName: string | null;
  note: string | null;
  status: CashDiscrepancyState;
  recoveredCents: number;
  /** What is still to claim (0 unless Pending). */
  pendingCents: number;
  /** Why it was written off. */
  resolutionNote: string | null;
  resolvedAt: string | null;
  /** Only in the detail of a shortage. */
  recoveries: ShortageRecoveryResponse[];
  createdAt: string;
  updatedAt: string;
}

export interface CashDiscrepancyQuery {
  kind?: CashDiscrepancyType;
  status?: CashDiscrepancyState;
  employeeId?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export interface RecoverShortageRequest {
  /** Default: today. */
  date?: string | null;
  cashBoxId: string;
  amountCents: number;
  note?: string | null;
}

export interface PendingShortagesResponse {
  count: number;
  pendingCents: number;
}

/** RF-CIE-09: business days already over and not closed (at most 31 listed; count has them all). */
export interface PendingClosingDaysResponse {
  lastClosedDate: string | null;
  count: number;
  days: string[];
}

export interface EnvelopeEvaluationResponse {
  envelopeId: string;
  name: string;
  role: EnvelopeType;
  /** Null for Reposición. */
  targetPercentBps: number | null;
  receivedCents: number;
  /** Over what the % envelopes received together. Null for Reposición or with nothing received. */
  realPercentBps: number | null;
}

/** RF-PF-10. */
export interface ProfitFirstEvaluationResponse {
  from: string;
  to: string;
  closedDays: number;
  cashCollectedCents: number;
  transferCollectedCents: number;
  /** A1. */
  collectedCents: number;
  /** A2. */
  costOfSalesCents: number;
  /** A3. */
  realIncomeCents: number;
  costRatioBps: number | null;
  realIncomeRatioBps: number | null;
  theoreticalSalesCents: number;
  staffPayCents: number;
  shortageCents: number;
  surplusCents: number;
  recoveredCents: number;
  internalDebtCents: number;
  envelopes: EnvelopeEvaluationResponse[];
  /** The confirmed expenses of the period, per currency (never added up). */
  expenses: ExpenseTotalResponse[];
}

export interface DailyClosingResponse {
  id: string;
  date: string;
  status: DailyClosingState;
  employeeId: string | null;
  employeeName: string | null;
  notes: string | null;
  theoreticalSalesCents: number;
  shrinkageToleranceCents: number;
  cashCollectedCents: number;
  transferCollectedCents: number;
  /** A1. */
  collectedCents: number;
  /** A2. */
  costOfSalesCents: number;
  /** A3 = A1 − A2. */
  realIncomeCents: number;
  /** A1 − theoretical sales. Negative = shortage. */
  cashDifferenceCents: number;
  /** After the accepted shrinkage. */
  adjustedDifferenceCents: number;
  thresholdCents: number;
  requiresNote: boolean;
  staffPayCents: number;
  payFixedCents: number;
  payPercentBps: number;
  debtBeforeCents: number;
  debtCreatedCents: number;
  debtRepaidCents: number;
  debtAfterCents: number;
  voidReason: string | null;
  voidedAt: string | null;
  lines: DailyClosingLineResponse[];
  collections: DailyClosingCollectionResponse[];
  allocations: DailyClosingAllocationResponse[];
  discrepancy: CashDiscrepancyResponse | null;
  /** Preview and create only: another closed day has exactly the same numbers (RF-CIE-11). */
  possibleDuplicateOf: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface DailyClosingSummaryResponse {
  id: string;
  date: string;
  status: DailyClosingState;
  employeeName: string | null;
  collectedCents: number;
  transferCollectedCents: number;
  costOfSalesCents: number;
  realIncomeCents: number;
  adjustedDifferenceCents: number;
  staffPayCents: number;
  debtAfterCents: number;
}

export interface DailyClosingQuery {
  from?: string;
  to?: string;
  includeVoided?: boolean;
  page?: number;
  pageSize?: number;
}

// ---- Gastos (slice 7) ----
// Money in cents of the CASH BOX's currency. Never edited: voided with a reason.

export type ExpenseState = 'Confirmed' | 'Voided';
export type ExpenseFrequency = 'Monthly' | 'Weekly';

export interface ExpenseCategoryResponse {
  id: string;
  name: string;
  isActive: boolean;
  /** Confirmed expenses in it. */
  expenseCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreateExpenseCategoryRequest {
  name: string;
}

export interface UpdateExpenseCategoryRequest {
  name: string;
  isActive: boolean;
}

export interface ExpenseResponse {
  id: string;
  number: number;
  date: string;
  categoryId: string;
  categoryName: string;
  description: string;
  amountCents: number;
  cashBoxId: string;
  cashBoxName: string;
  currency: CurrencyCode;
  envelopeId: string;
  envelopeName: string;
  notes: string | null;
  /** A confirmed occurrence of a recurring expense. */
  recurringExpenseId: string | null;
  recurringExpenseName: string | null;
  periodStart: string | null;
  status: ExpenseState;
  voidReason: string | null;
  voidedAt: string | null;
  /** Only right after creating it: the envelope it left below zero (allowed). */
  warning: NegativeEnvelopeWarningResponse | null;
  createdAt: string;
  updatedAt: string;
}

export interface ExpenseTotalResponse {
  currency: CurrencyCode;
  amountCents: number;
}

export interface ExpensesResponse extends PagedResponse<ExpenseResponse> {
  /** Confirmed expenses of the whole filter (not only the page), per currency. */
  totals: ExpenseTotalResponse[];
}

export interface CreateExpenseRequest {
  date: string;
  categoryId: string;
  description: string;
  amountCents: number;
  cashBoxId: string;
  /** Null = Gastos de operación. Never Reposición. */
  envelopeId?: string | null;
  notes?: string | null;
}

export interface ExpenseQuery {
  from?: string;
  to?: string;
  categoryId?: string;
  cashBoxId?: string;
  envelopeId?: string;
  recurringExpenseId?: string;
  includeVoided?: boolean;
  page?: number;
  pageSize?: number;
}

export interface RecurringExpenseResponse {
  id: string;
  name: string;
  categoryId: string;
  categoryName: string;
  amountCents: number;
  cashBoxId: string;
  cashBoxName: string;
  currency: CurrencyCode;
  envelopeId: string;
  envelopeName: string;
  frequency: ExpenseFrequency;
  /** Monthly: day of the month (1-28). Weekly: 0 (Sunday) to 6 (Saturday). */
  dueDay: number;
  startsOn: string;
  notes: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateRecurringExpenseRequest {
  name: string;
  categoryId: string;
  amountCents: number;
  cashBoxId: string;
  envelopeId?: string | null;
  frequency: ExpenseFrequency;
  dueDay: number;
  startsOn: string;
  notes?: string | null;
}

export interface UpdateRecurringExpenseRequest extends CreateRecurringExpenseRequest {
  isActive: boolean;
}

/** An occurrence waiting to be paid or skipped. */
export interface DueExpenseResponse {
  recurring: RecurringExpenseResponse;
  /** The 1st of the month, or the Monday of the week. */
  periodStart: string;
  dueDate: string;
}

/** Null = what the recurring expense says; the date defaults to today. */
export interface ConfirmRecurringExpenseRequest {
  periodStart: string;
  date?: string | null;
  amountCents?: number | null;
  cashBoxId?: string | null;
  notes?: string | null;
}

/** D-61: why a price needs a look. */
export type PriceNoticeKind = 'BatchChanged' | 'NewBatchCostlier';

/** D-61: a price to look at. The app never changes a price by itself. */
export interface PriceNoticeResponse {
  productId: string;
  productCode: string;
  productName: string;
  kind: PriceNoticeKind;
  salePriceCents: number | null;
  suggestedPriceCents: number;
  /** The batch the notice is about: CUP cost per stock unit, in millionths. */
  batchCostCupMicros: number;
  batchRateCents: number;
  stockUnit: UnitCode;
}

export interface ResolvePriceNoticeRequest {
  kind: PriceNoticeKind;
  /** true = use the suggested price from today; false = keep the price. */
  acceptSuggested: boolean;
}
