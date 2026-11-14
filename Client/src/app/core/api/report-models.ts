import {
  CurrencyCode,
  ExpenseTotalResponse,
  PriceChangeResponse,
  ProfitFirstEvaluationResponse,
  ShoppingListItemResponse,
  UnitCode,
} from './models';

/**
 * Slice 8: the dashboard and the reports (mirror of ReportResponses.cs). Money in CUP cents unless a currency
 * says otherwise; quantities in thousandths of the stock unit; unit costs in millionths; % in basis points.
 */

/** One confirmed closing. */
export interface DailyFiguresResponse {
  closingId: string;
  date: string;
  /** A1, A2, A3. */
  collectedCents: number;
  costOfSalesCents: number;
  realIncomeCents: number;
  theoreticalSalesCents: number;
  /** After the tolerance: negative = shortage. */
  differenceCents: number;
  staffPayCents: number;
}

export interface PeriodSummaryResponse {
  from: string;
  to: string;
  closedDays: number;
  collectedCents: number;
  costOfSalesCents: number;
  realIncomeCents: number;
  theoreticalSalesCents: number;
  staffPayCents: number;
  shortageCents: number;
  surplusCents: number;
  expenses: ExpenseTotalResponse[];
}

export interface BufferExceededProductResponse {
  productId: string;
  code: string;
  name: string;
  salePriceCents: number | null;
  suggestedPriceCents: number;
  /** The last purchase's rate, CUP cents per USD. */
  lastRateCents: number;
  bufferCents: number;
  todayRateCents: number;
}

export interface DashboardResponse {
  today: string;
  lastClosing: DailyFiguresResponse | null;
  /** From the 1st to today. */
  month: PeriodSummaryResponse;
  /** The same days of the previous month. */
  previousMonth: PeriodSummaryResponse;
  /** Closings of the last 30 days, oldest first. */
  days: DailyFiguresResponse[];
  lowStockCount: number;
  lowStock: ShoppingListItemResponse[];
  bufferExceededCount: number;
  bufferExceeded: BufferExceededProductResponse[];
}

// ---------- Monthly report ----------

export interface ExpenseCategoryTotalResponse {
  categoryId: string;
  categoryName: string;
  currency: CurrencyCode;
  count: number;
  amountCents: number;
}

export interface DiscrepancySummaryResponse {
  shortageCount: number;
  shortageCents: number;
  surplusCount: number;
  surplusCents: number;
  recoveredCents: number;
  lostCents: number;
  pendingCents: number;
}

export interface MonthTotalsResponse {
  year: number;
  month: number;
  closedDays: number;
  collectedCents: number;
  costOfSalesCents: number;
  realIncomeCents: number;
  staffPayCents: number;
  expensesCupCents: number;
  expensesUsdCents: number;
}

export interface MonthlyReportResponse {
  year: number;
  month: number;
  evaluation: ProfitFirstEvaluationResponse;
  expensesByCategory: ExpenseCategoryTotalResponse[];
  discrepancies: DiscrepancySummaryResponse;
  merchandiseLossCents: number;
  days: DailyFiguresResponse[];
  /** The last 12 months, this one last. */
  trend: MonthTotalsResponse[];
}

// ---------- Losses ----------

export type LossType = 'Shrinkage' | 'Expired' | 'Damaged' | 'CountDifference';

export interface MerchandiseLossResponse {
  operationId: string;
  operationNumber: number;
  date: string;
  kind: LossType;
  productId: string;
  productCode: string;
  productName: string;
  stockUnit: UnitCode;
  quantityMilli: number;
  unitCostCupMicros: number | null;
  costCents: number;
}

export interface LostShortageResponse {
  discrepancyId: string;
  date: string;
  amountCents: number;
  recoveredCents: number;
  lostCents: number;
  employeeName: string | null;
  note: string | null;
}

export interface LossReportResponse {
  from: string;
  to: string;
  merchandise: MerchandiseLossResponse[];
  byKind: { kind: LossType; costCents: number }[];
  merchandiseCents: number;
  shortages: LostShortageResponse[];
  lostShortageCents: number;
  totalCents: number;
  /** Counts that found more: informative, it does not offset the losses. */
  countGainCents: number;
}

// ---------- Product ranking ----------

export interface ProductRankingResponse {
  productId: string;
  code: string;
  name: string;
  stockUnit: UnitCode;
  soldMilli: number;
  /** Theoretical sales of the closings. */
  salesCents: number;
  costCents: number;
  marginCents: number;
  marginBps: number | null;
  lossMilli: number;
  lossCents: number;
}

export interface ProductRankingReportResponse {
  from: string;
  to: string;
  items: ProductRankingResponse[];
}

// ---------- Costs and prices ----------

export interface PurchasePointResponse {
  purchaseId: string;
  purchaseNumber: number;
  date: string;
  supplierId: string;
  supplierName: string;
  currency: CurrencyCode;
  rateCupPerUsdCents: number;
  stockQuantityMilli: number;
  /** Landed, per stock unit. */
  unitCostUsdMicros: number;
  unitCostCupMicros: number;
}

export interface SupplierCostResponse {
  supplierId: string;
  supplierName: string;
  purchaseCount: number;
  lastDate: string;
  lastUnitCostUsdMicros: number;
  lastUnitCostCupMicros: number;
  minUnitCostUsdMicros: number;
  maxUnitCostUsdMicros: number;
  /** Weighted by quantity. */
  averageUnitCostUsdMicros: number;
  averageUnitCostCupMicros: number;
  totalQuantityMilli: number;
}

export interface ProductCostHistoryResponse {
  productId: string;
  code: string;
  name: string;
  stockUnit: UnitCode;
  salePriceCents: number | null;
  from: string | null;
  to: string | null;
  purchases: PurchasePointResponse[];
  priceChanges: PriceChangeResponse[];
  /** Cheapest on average first; never filtered by supplier. */
  suppliers: SupplierCostResponse[];
}

export interface ProductCostChangeResponse {
  productId: string;
  code: string;
  name: string;
  stockUnit: UnitCode;
  purchaseCount: number;
  firstDate: string;
  firstUnitCostUsdMicros: number;
  firstUnitCostCupMicros: number;
  lastDate: string;
  lastUnitCostUsdMicros: number;
  lastUnitCostCupMicros: number;
  usdChangeBps: number | null;
  cupChangeBps: number | null;
  salePriceCents: number | null;
}

export interface CostChangesReportResponse {
  from: string;
  to: string;
  items: ProductCostChangeResponse[];
}
