import { CategoryResponse, ProductResponse } from '../../core/api/models';
import { formatHundredths } from '../../shared/format/money';

/** Lookups shared by the catalog screens (categories arrive as a flat list with parentId). */
export class CategoryIndex {
  private readonly byId: Map<string, CategoryResponse>;

  constructor(readonly all: CategoryResponse[]) {
    this.byId = new Map(all.map((category) => [category.id, category]));
  }

  get topLevel(): CategoryResponse[] {
    return this.all.filter((category) => category.parentId === null);
  }

  childrenOf(parentId: string): CategoryResponse[] {
    return this.all.filter((category) => category.parentId === parentId);
  }

  name(id: string | null): string {
    return id === null ? '' : (this.byId.get(id)?.name ?? '—');
  }

  /** "Comida › Arroz", or just "Comida". */
  pathOf(product: Pick<ProductResponse, 'categoryId' | 'subcategoryId'>): string {
    const category = this.name(product.categoryId);
    return product.subcategoryId ? `${category} › ${this.name(product.subcategoryId)}` : category;
  }
}

export type ProductState = { label: string; tone: 'ok' | 'warn' | 'accent' | 'muted' };

/**
 * One pill per product, the most important first:
 *  - "Colchón superado" (RN-04): today's rate ate the margin; the price needs a look NOW.
 *  - "Stock bajo": only once it was bought (a never-bought product is not "low", it is new).
 *  - "Precio nuevo" = changed since the price list was last copied to the shop sign.
 *  - "Precio sugerido": only when the suggestion is ABOVE the price (selling under the target markup).
 *    A price kept above the suggestion is a decision, not a problem, so it does not nag.
 */
export function productState(product: ProductResponse): ProductState {
  if (!product.isActive) {
    return { label: 'Inactivo', tone: 'muted' };
  }
  if (product.salePriceCents === null) {
    return { label: 'Sin precio', tone: 'warn' };
  }
  if (product.cost?.isBufferExceeded) {
    return { label: 'Colchón superado', tone: 'warn' };
  }
  if (isLowStock(product)) {
    return { label: 'Stock bajo', tone: 'warn' };
  }
  if (product.isPriceChangedSinceListed) {
    return { label: 'Precio nuevo, falta el cartel', tone: 'accent' };
  }
  if (product.cost && product.cost.suggestedPriceCents > product.salePriceCents) {
    return { label: `Precio sugerido: ${formatHundredths(product.cost.suggestedPriceCents)}`, tone: 'accent' };
  }
  return { label: 'Bien', tone: 'ok' };
}

export function isLowStock(product: ProductResponse): boolean {
  return product.lastPurchaseDate !== null && product.stockMilli < product.minStockMilli;
}

// ---- Shelf life (RF-PRD-10, D-53): typed as days, weeks or months, stored in days. A month counts 30 days. ----

export type ShelfLifeUnit = 'days' | 'weeks' | 'months';

export const SHELF_LIFE_UNITS: { value: ShelfLifeUnit; label: string; days: number }[] = [
  { value: 'days', label: 'días', days: 1 },
  { value: 'weeks', label: 'semanas', days: 7 },
  { value: 'months', label: 'meses', days: 30 },
];

export const MAX_SHELF_LIFE_DAYS = 3650;

export function shelfLifeToDays(amount: number, unit: ShelfLifeUnit): number {
  return amount * SHELF_LIFE_UNITS.find((u) => u.value === unit)!.days;
}

/** 90 → 3 months, 14 → 2 weeks, 45 → 45 days: the largest unit that divides it exactly. */
export function splitShelfLife(days: number): { amount: number; unit: ShelfLifeUnit } {
  for (const unit of [...SHELF_LIFE_UNITS].reverse()) {
    if (days % unit.days === 0) {
      return { amount: days / unit.days, unit: unit.value };
    }
  }
  return { amount: days, unit: 'days' };
}

const SHELF_LIFE_WORDS: Record<ShelfLifeUnit, [string, string]> = {
  days: ['día', 'días'],
  weeks: ['semana', 'semanas'],
  months: ['mes', 'meses'],
};

/** "3 meses", "1 semana", "45 días". */
export function shelfLifeText(days: number): string {
  const { amount, unit } = splitShelfLife(days);
  const [one, many] = SHELF_LIFE_WORDS[unit];
  return `${amount} ${amount === 1 ? one : many}`;
}
