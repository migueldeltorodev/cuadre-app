import { CashBoxType } from '../../../core/api/models';

/** API values are English (code); what the user reads is Spanish. */
export const CASH_BOX_TYPE_LABELS: Record<CashBoxType, string> = {
  Cash: 'Efectivo',
  Bank: 'Banco',
};
