import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable, forkJoin, of } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { CategoriesApi, ProductsApi, SuppliersApi, UnitsApi } from '../../../core/api/catalog-api';
import {
  CreateProductRequest,
  ProductResponse,
  SupplierResponse,
  UnitCode,
  UnitResponse,
} from '../../../core/api/models';
import { BusinessSettingsStore } from '../../../core/state/business-settings-store';
import { amountValidator } from '../../../shared/format/money-validators';
import { formatMoney, formatPercent, parseHundredths, toInputText } from '../../../shared/format/money';
import {
  ONE_UNIT_MILLI,
  UNIT_LABELS,
  isWhole,
  optional,
  parseThousandths,
  quantityValidator,
  toQuantityInput,
  unitName,
} from '../../../shared/format/quantity';
import { CategoryIndex } from '../catalog-labels';
import { Breadcrumbs } from '../../../core/layout/breadcrumbs';

/**
 * Create or edit a product (RF-PRD-02). One page for both: `/productos/nuevo` and `/productos/:id/editar`.
 * Amounts and quantities are typed as text and converted to integers only here (money.ts / quantity.ts).
 */
@Component({
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './producto-form-page.html',
  styleUrl: './producto-form-page.css',
})
export default class ProductoFormPage implements OnInit {
  /** Route param; absent on "nuevo". */
  readonly id = input<string>();

  private readonly router = inject(Router);
  private readonly productsApi = inject(ProductsApi);
  private readonly categoriesApi = inject(CategoriesApi);
  private readonly suppliersApi = inject(SuppliersApi);
  private readonly unitsApi = inject(UnitsApi);
  protected readonly settings = inject(BusinessSettingsStore).settings;

  protected readonly unitName = unitName;

  protected readonly existing = signal<ProductResponse | null>(null);

  /** The trail names this page once it is loaded (the shell draws it). */
  private readonly crumbs = inject(Breadcrumbs);
  protected readonly nameInTrail = effect(() => {
    const product = this.existing();
    if (product) {
      this.crumbs.set('Editar', [{ label: product.name, link: ['/productos', product.id] }]);
    }
  });
  protected readonly categories = signal(new CategoryIndex([]));
  protected readonly suppliers = signal<SupplierResponse[]>([]);
  protected readonly units = signal<UnitResponse[]>([]);
  protected readonly loaded = signal(false);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  private readonly submitted = signal(false);

  protected readonly editing = computed(() => this.id() !== undefined);

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    brand: ['', Validators.maxLength(60)],
    presentation: ['', Validators.maxLength(60)],
    categoryId: ['', Validators.required],
    subcategoryId: [''],
    code: ['', [Validators.maxLength(20), Validators.pattern(/^[A-Za-z0-9-]*$/)]],
    barcode: ['', [Validators.maxLength(50), Validators.pattern(/^[A-Za-z0-9]*$/)]],
    purchaseUnit: ['Each' as UnitCode],
    stockUnit: ['Each' as UnitCode],
    conversionFactor: ['1', quantityValidator({ min: 1 })],
    salePrice: ['', optional(amountValidator({ min: 1 }))],
    markup: ['', optional(amountValidator({ min: 0, max: 100_000 }))],
    buffer: ['', optional(amountValidator({ min: 0 }))],
    minStock: ['0', quantityValidator()],
    shrinkage: ['0', amountValidator({ min: 0, max: 10_000 })],
    usualSupplierId: [''],
    isPerishable: [false],
    notes: ['', Validators.maxLength(500)],
    isActive: [true],
  });

  // Signals over the controls the template reacts to.
  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  protected readonly subcategories = computed(() => {
    const categoryId = this.value().categoryId;
    return categoryId ? this.categories().childrenOf(categoryId) : [];
  });

  protected readonly sameUnit = computed(() => this.value().purchaseUnit === this.value().stockUnit);

  private readonly stockAllowsDecimals = computed(
    () => this.units().find((unit) => unit.code === this.value().stockUnit)?.allowsDecimals ?? false,
  );

  /** "1 saco = [ 100 ] libras". */
  protected readonly factorPrefix = computed(() => `1 ${UNIT_LABELS[this.value().purchaseUnit ?? 'Each'].one} =`);
  protected readonly stockUnitWord = computed(() => UNIT_LABELS[this.value().stockUnit ?? 'Each'].many);

  /** Countable stock units cannot hold fractions (a box of 24 holds 24, never 24.5). */
  protected readonly factorFraction = computed(() => this.hasFraction(this.value().conversionFactor));
  protected readonly minStockFraction = computed(() => this.hasFraction(this.value().minStock));

  /** Active suppliers, plus the current one even if it was deactivated later. */
  protected readonly selectableSuppliers = computed(() =>
    this.suppliers().filter((s) => s.isActive || s.id === this.existing()?.usualSupplierId),
  );

  protected readonly markupPlaceholder = computed(() => {
    const settings = this.settings();
    return settings ? `${formatPercent(settings.defaultMarkupBps)} del negocio` : '';
  });

  protected readonly bufferPlaceholder = computed(() => {
    const settings = this.settings();
    return settings ? `${formatMoney(settings.exchangeBufferCents)} del negocio` : '';
  });

  ngOnInit(): void {
    const id = this.id();
    const product$: Observable<ProductResponse | null> = id ? this.productsApi.get(id) : of(null);

    forkJoin({
      product: product$,
      categories: this.categoriesApi.getAll(),
      suppliers: this.suppliersApi.getAll(),
      units: this.unitsApi.getAll(),
    }).subscribe({
      next: ({ product, categories, suppliers, units }) => {
        this.categories.set(new CategoryIndex(categories.items));
        this.suppliers.set(suppliers.items);
        this.units.set(units.items);
        if (product) {
          this.existing.set(product);
          this.fill(product);
        }
        this.loaded.set(true);
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }

  /**
   * On a new product the code follows the category, until the user types their own.
   * The id comes from the DOM event: the form control may not have been updated yet when (change) fires.
   */
  protected onCategoryChange(categoryId: string): void {
    this.form.controls.subcategoryId.setValue('');
    if (this.editing() || this.form.controls.code.dirty || !categoryId) {
      return;
    }
    this.productsApi.nextCode(categoryId).subscribe({
      next: ({ code }) => this.form.controls.code.setValue(code),
      error: () => this.form.controls.code.setValue(''),
    });
  }

  protected invalid(name: string): boolean {
    const control = this.form.get(name)!;
    return control.invalid && (control.touched || this.submitted());
  }

  /** A per-field message from a 400 (server keys are the request's camelCase names). */
  protected serverError(field: string): string | null {
    return this.error()?.fields[field] ?? null;
  }

  protected save(): void {
    this.submitted.set(true);
    const request = this.toRequest();
    if (this.form.invalid || request === null || this.factorFraction() || this.minStockFraction()) {
      return;
    }

    const existing = this.existing();
    const save$: Observable<ProductResponse> = existing
      ? this.productsApi.update(existing.id, {
          ...request,
          code: request.code ?? existing.code,
          isActive: this.form.controls.isActive.value,
        })
      : this.productsApi.create(request);

    this.saving.set(true);
    this.error.set(null);
    save$.subscribe({
      next: (saved) => void this.router.navigate(['/productos', saved.id]),
      error: (err: unknown) => {
        this.error.set(toApiError(err));
        this.saving.set(false);
      },
    });
  }

  private toRequest(): CreateProductRequest | null {
    const v = this.form.getRawValue();
    const factor = this.sameUnit() ? ONE_UNIT_MILLI : parseThousandths(v.conversionFactor);
    const minStock = parseThousandths(v.minStock);
    const shrinkage = parseHundredths(v.shrinkage);
    if (factor === null || minStock === null || shrinkage === null) {
      return null;
    }

    return {
      code: blankToNull(v.code)?.toUpperCase() ?? null,
      barcode: blankToNull(v.barcode),
      name: v.name.trim(),
      brand: blankToNull(v.brand),
      presentation: blankToNull(v.presentation),
      categoryId: v.categoryId,
      subcategoryId: blankToNull(v.subcategoryId),
      usualSupplierId: blankToNull(v.usualSupplierId),
      purchaseUnit: v.purchaseUnit,
      stockUnit: v.stockUnit,
      conversionFactorMilli: factor,
      salePriceCents: blankToNull(v.salePrice) === null ? null : parseHundredths(v.salePrice),
      minStockMilli: minStock,
      shrinkageToleranceBps: shrinkage,
      exchangeBufferCents: blankToNull(v.buffer) === null ? null : parseHundredths(v.buffer),
      markupBps: blankToNull(v.markup) === null ? null : parseHundredths(v.markup),
      isPerishable: v.isPerishable,
      notes: blankToNull(v.notes),
    };
  }

  private fill(product: ProductResponse): void {
    this.form.reset({
      name: product.name,
      brand: product.brand ?? '',
      presentation: product.presentation ?? '',
      categoryId: product.categoryId,
      subcategoryId: product.subcategoryId ?? '',
      code: product.code,
      barcode: product.barcode ?? '',
      purchaseUnit: product.purchaseUnit,
      stockUnit: product.stockUnit,
      conversionFactor: toQuantityInput(product.conversionFactorMilli),
      salePrice: product.salePriceCents === null ? '' : toInputText(product.salePriceCents),
      markup: product.markupBps === null ? '' : toInputText(product.markupBps),
      buffer: product.exchangeBufferCents === null ? '' : toInputText(product.exchangeBufferCents),
      minStock: toQuantityInput(product.minStockMilli),
      shrinkage: toInputText(product.shrinkageToleranceBps),
      usualSupplierId: product.usualSupplierId ?? '',
      isPerishable: product.isPerishable,
      notes: product.notes ?? '',
      isActive: product.isActive,
    });
  }

  private hasFraction(text: string | undefined): boolean {
    const milli = parseThousandths(text);
    return milli !== null && !this.stockAllowsDecimals() && !isWhole(milli);
  }
}

function blankToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}
