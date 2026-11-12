import { CategoryResponse, ProductResponse } from '../../core/api/models';

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
 * One pill per product, the most important first. Stock and colchón states arrive with slices 3/4.
 * "Precio nuevo" = changed since the price list was last copied to the shop sign.
 */
export function productState(product: ProductResponse): ProductState {
  if (!product.isActive) {
    return { label: 'Inactivo', tone: 'muted' };
  }
  if (product.salePriceCents === null) {
    return { label: 'Sin precio', tone: 'warn' };
  }
  if (product.isPriceChangedSinceListed) {
    return { label: 'Precio nuevo, falta el cartel', tone: 'accent' };
  }
  return { label: 'Bien', tone: 'ok' };
}
