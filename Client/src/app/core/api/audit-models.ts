/** RF-DAT-08: the audit log (mirror of AuditResponses.cs). Moments are ISO UTC timestamps. */

export type AuditEventKindCode =
  | 'Voided'
  | 'DayReopened'
  | 'PriceChanged'
  | 'ProfitFirstPlan'
  | 'PayScheme'
  | 'ConfigChanged';

export type AuditSubjectCode =
  | 'Purchase'
  | 'StockOperation'
  | 'EnvelopeOperation'
  | 'Expense'
  | 'ShortageRecovery'
  | 'DailyClosing'
  | 'Product'
  | 'AllocationPlan'
  | 'StaffPayScheme'
  | 'Settings'
  | 'CashBox'
  | 'PaymentMethod'
  | 'ExchangeRate'
  | 'Envelope'
  | 'Employee'
  | 'ExpenseCategory';

/** Before and after, already written for people ("20 %", "Activo"). */
export interface FieldChangeResponse {
  field: string;
  before: string | null;
  after: string | null;
}

export interface AuditEventResponse {
  at: string;
  kind: AuditEventKindCode;
  subject: AuditSubjectCode;
  subjectId: string | null;
  /** Ready to show: "Compra #12", "Caja «Efectivo CUP»". */
  title: string;
  reason: string | null;
  changes: FieldChangeResponse[];
}

export interface AuditQuery {
  from?: string;
  to?: string;
  kind?: AuditEventKindCode;
  page?: number;
  pageSize?: number;
}
