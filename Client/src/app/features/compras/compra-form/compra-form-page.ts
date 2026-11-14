import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  FormArray,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { KeyValuePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { ProductsApi, SuppliersApi, UnitsApi } from '../../../core/api/catalog-api';
import { CashBoxesApi } from '../../../core/api/configuration-api';
import {
  CashBoxResponse,
  CreatePurchaseRequest,
  CurrencyCode,
  LocationResponse,
  ProductResponse,
  PurchaseResponse,
  SupplierResponse,
  UnitResponse,
} from '../../../core/api/models';
import { LocationsApi, PurchasesApi } from '../../../core/api/purchases-api';
import { ExchangeRateStore } from '../../../core/state/exchange-rate-store';
import { amountValidator } from '../../../shared/format/money-validators';
import { formatHundredths, formatMoney, parseHundredths, toInputText } from '../../../shared/format/money';
import {
  UNIT_LABELS,
  formatQuantityWithUnit,
  isWhole,
  optional,
  parseThousandths,
  quantityValidator,
  toQuantityInput,
} from '../../../shared/format/quantity';
import { DIALOG_DEFAULTS } from '../../../shared/ui/dialog-config';
import { SupplierDialog, SupplierDialogData } from '../../productos/proveedores/supplier-dialog';
import { convertCents, lineSubtotalCents, localToday, stockQuantityMilli } from '../purchase-format';

/** What the person decided about each suggested price after confirming. */
type SuggestionDecision = 'pending' | 'saving' | 'used' | 'kept';

/** Everything is typed as text and parsed to integers only in toRequest (money.ts / quantity.ts). */
type LineForm = FormGroup<{
  productId: FormControl<string>;
  quantity: FormControl<string>;
  unitCost: FormControl<string>;
  /** Blank = the factor of the product; typed when this batch came different (a 95 lb sack). */
  factor: FormControl<string>;
}>;

type ChargeForm = FormGroup<{
  description: FormControl<string>;
  currency: FormControl<CurrencyCode>;
  amount: FormControl<string>;
  cashBoxId: FormControl<string>;
}>;

/** One line as the template reads it: the typed values already parsed, plus its hints and problems. */
interface LineView {
  product: ProductResponse | null;
  /** "saco = 100 lb" */
  conversionText: string;
  quantityUnit: string;
  costUnit: string;
  subtotalCents: number | null;
  /** "= 200 lb" */
  stockText: string;
  problem: string | null;
  showFactor: boolean;
  /** The product's factor, shown as the placeholder of the per-batch override. */
  factorPlaceholder: string;
  factorUnit: string;
}

/**
 * New purchase (design "Compra", RF-CMP-01/02/04). Confirming it moves stock, cash and cost at once
 * (one server transaction). Subtotals here are for reading only: the server recomputes everything.
 * The suggested prices come back WITH the confirmation, and each one is decided here: use it or keep the price.
 */
@Component({
  imports: [ReactiveFormsModule, RouterLink, KeyValuePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './compra-form-page.html',
  styleUrl: './compra-form-page.css',
})
export default class CompraFormPage implements OnInit {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly purchasesApi = inject(PurchasesApi);
  private readonly productsApi = inject(ProductsApi);
  private readonly suppliersApi = inject(SuppliersApi);
  private readonly unitsApi = inject(UnitsApi);
  private readonly cashBoxesApi = inject(CashBoxesApi);
  private readonly locationsApi = inject(LocationsApi);
  private readonly rateStore = inject(ExchangeRateStore);
  private readonly dialog = inject(Dialog);

  protected readonly formatHundredths = formatHundredths;
  protected readonly formatMoney = formatMoney;

  protected readonly suppliers = signal<SupplierResponse[]>([]);
  protected readonly products = signal<ProductResponse[]>([]);
  protected readonly cashBoxes = signal<CashBoxResponse[]>([]);
  protected readonly locations = signal<LocationResponse[]>([]);
  private readonly units = signal<UnitResponse[]>([]);
  protected readonly loaded = signal(false);
  protected readonly loadError = signal<ApiError | null>(null);

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly submitted = signal(false);

  /** Set once confirmed: the form turns into the result. */
  protected readonly confirmed = signal<PurchaseResponse | null>(null);
  protected readonly decisions = signal<Record<string, SuggestionDecision>>({});
  protected readonly decisionError = signal<string | null>(null);

  protected readonly form = this.fb.group({
    supplierId: ['', Validators.required],
    date: [localToday(), Validators.required],
    currency: ['USD' as CurrencyCode],
    rate: ['', amountValidator({ min: 1 })],
    locationId: [''],
    cashBoxId: ['', Validators.required],
    notes: ['', Validators.maxLength(500)],
    lines: this.fb.array<LineForm>([this.newLine()]),
    charges: this.fb.array<ChargeForm>([]),
  });

  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  protected get lines(): FormArray<LineForm> {
    return this.form.controls.lines;
  }

  protected get charges(): FormArray<ChargeForm> {
    return this.form.controls.charges;
  }

  protected readonly activeSuppliers = computed(() => this.suppliers().filter((s) => s.isActive));
  protected readonly activeLocations = computed(() => this.locations().filter((l) => l.isActive));

  protected readonly currency = computed(() => this.value().currency ?? 'USD');
  private readonly rateCents = computed(() => parseHundredths(this.value().rate) ?? 0);

  private readonly productById = computed(() => new Map(this.products().map((p) => [p.id, p])));

  /** Boxes that can pay in a currency: a CUP box never pays USD (the server says the same). */
  protected boxesFor(currency: CurrencyCode | undefined): CashBoxResponse[] {
    return this.cashBoxes().filter((box) => box.isActive && box.currency === currency);
  }

  protected readonly lineViews = computed<LineView[]>(() => {
    const raw = this.value();
    const lines = raw.lines ?? [];
    const currency = raw.currency ?? 'USD';
    const chosen = lines.map((line) => line.productId);

    return lines.map((line, index) => {
      const product = line.productId ? (this.productById().get(line.productId) ?? null) : null;
      const view: LineView = {
        product,
        conversionText: '',
        quantityUnit: '',
        costUnit: currency,
        subtotalCents: null,
        stockText: '',
        problem: null,
        showFactor: false,
        factorPlaceholder: '',
        factorUnit: '',
      };
      if (!product) {
        return view;
      }

      const purchaseLabel = UNIT_LABELS[product.purchaseUnit];
      const sameUnit = product.purchaseUnit === product.stockUnit;
      view.conversionText = sameUnit
        ? `por ${purchaseLabel.one}`
        : `${purchaseLabel.one} = ${formatQuantityWithUnit(product.conversionFactorMilli, product.stockUnit)}`;
      view.quantityUnit = purchaseLabel.many;
      view.costUnit = `${currency} / ${purchaseLabel.one}`;
      view.showFactor = !sameUnit;
      view.factorPlaceholder = toQuantityInput(product.conversionFactorMilli);
      view.factorUnit = UNIT_LABELS[product.stockUnit].short ?? UNIT_LABELS[product.stockUnit].many;

      const quantity = parseThousandths(line.quantity);
      const cost = parseHundredths(line.unitCost);
      const factor = line.factor?.trim() ? parseThousandths(line.factor) : product.conversionFactorMilli;

      if (chosen.indexOf(line.productId) !== index) {
        view.problem = 'Este producto ya está en otra línea: juntá las cantidades.';
      } else if (quantity !== null && !this.allowsDecimals(product.purchaseUnit) && !isWhole(quantity)) {
        view.problem = `Se compra por ${purchaseLabel.many}: la cantidad va sin decimales.`;
      }

      if (quantity !== null && quantity > 0 && cost !== null && cost > 0) {
        view.subtotalCents = lineSubtotalCents(quantity, cost);
      }
      if (quantity !== null && quantity > 0 && factor !== null && factor > 0 && !sameUnit) {
        const stock = stockQuantityMilli(quantity, factor);
        if (stock === null || (!product.allowsDecimals && !isWhole(stock))) {
          view.problem ??= 'Esa cantidad no da un número exacto de unidades de stock. Revisá el factor.';
        } else {
          view.stockText = `= ${formatQuantityWithUnit(stock, product.stockUnit)}`;
        }
      }
      return view;
    });
  });

  protected readonly goodsTotalCents = computed(() =>
    this.lineViews().reduce((sum, line) => sum + (line.subtotalCents ?? 0), 0),
  );

  /** Each charge in its own currency, plus "≈" in the purchase currency when they differ. */
  protected readonly chargeViews = computed(() => {
    const rate = this.rateCents();
    const currency = this.currency();
    return (this.value().charges ?? []).map((charge) => {
      const amount = parseHundredths(charge.amount);
      const chargeCurrency = charge.currency ?? 'CUP';
      return {
        amountCents: amount,
        approxCents: amount === null ? null : convertCents(amount, chargeCurrency, currency, rate),
        differs: chargeCurrency !== currency,
      };
    });
  });

  protected readonly totalCents = computed(
    () => this.goodsTotalCents() + this.chargeViews().reduce((sum, c) => sum + (c.approxCents ?? 0), 0),
  );

  /** The total read in the OTHER currency: "· 150,693 CUP". */
  protected readonly totalOther = computed(() => {
    const rate = this.rateCents();
    if (rate <= 0) {
      return null;
    }
    const other: CurrencyCode = this.currency() === 'USD' ? 'CUP' : 'USD';
    return formatMoney(convertCents(this.totalCents(), this.currency(), other, rate), other);
  });

  protected readonly hasLineProblems = computed(() => this.lineViews().some((line) => line.problem !== null));

  ngOnInit(): void {
    forkJoin({
      suppliers: this.suppliersApi.getAll(),
      products: this.productsApi.getAll({ pageSize: 200 }),
      units: this.unitsApi.getAll(),
      cashBoxes: this.cashBoxesApi.getAll(),
      locations: this.locationsApi.getAll(),
    }).subscribe({
      next: ({ suppliers, products, units, cashBoxes, locations }) => {
        this.suppliers.set(suppliers.items);
        this.products.set([...products.items].sort((a, b) => a.name.localeCompare(b.name, 'es')));
        this.units.set(units.items);
        this.cashBoxes.set(cashBoxes.items);
        this.locations.set(locations.items);
        this.applyDefaults();
        this.loaded.set(true);
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }

  protected addLine(): void {
    this.lines.push(this.newLine());
  }

  protected removeLine(index: number): void {
    if (this.lines.length > 1) {
      this.lines.removeAt(index);
    }
  }

  protected addCharge(): void {
    this.charges.push(this.newCharge());
  }

  protected removeCharge(index: number): void {
    this.charges.removeAt(index);
  }

  /** The value comes from the DOM event: the control may not be updated yet when (change) fires. */
  protected onCurrencyChange(currency: CurrencyCode): void {
    const current = this.form.controls.cashBoxId.value;
    if (!this.boxesFor(currency).some((box) => box.id === current)) {
      this.form.controls.cashBoxId.setValue(this.boxesFor(currency)[0]?.id ?? '');
    }
  }

  protected onChargeCurrencyChange(index: number, currency: CurrencyCode): void {
    const control = this.charges.at(index).controls.cashBoxId;
    if (!this.boxesFor(currency).some((box) => box.id === control.value)) {
      control.setValue(this.boxesFor(currency)[0]?.id ?? '');
    }
  }

  /** RF-PRD-11: a supplier that is not in the list yet is created here, without losing the purchase typed so far. */
  protected newSupplier(): void {
    this.dialog
      .open<SupplierResponse, SupplierDialogData>(SupplierDialog, { ...DIALOG_DEFAULTS, data: {} })
      .closed.subscribe((created) => {
        if (created) {
          this.suppliers.set([...this.suppliers(), created].sort((a, b) => a.name.localeCompare(b.name, 'es')));
          this.form.controls.supplierId.setValue(created.id);
        }
      });
  }

  protected invalid(control: { invalid: boolean; touched: boolean }): boolean {
    return control.invalid && (control.touched || this.submitted());
  }

  protected confirm(): void {
    this.submitted.set(true);
    const request = this.toRequest();
    if (this.form.invalid || request === null || this.hasLineProblems()) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.purchasesApi.create(request).subscribe({
      next: (purchase) => {
        this.confirmed.set(purchase);
        this.decisions.set(Object.fromEntries(purchase.priceSuggestions.map((s) => [s.productId, 'pending'])));
        this.saving.set(false);
      },
      error: (err: unknown) => {
        this.error.set(toApiError(err));
        this.saving.set(false);
      },
    });
  }

  protected usePrice(productId: string, priceCents: number): void {
    const purchase = this.confirmed();
    if (!purchase) {
      return;
    }
    this.setDecision(productId, 'saving');
    this.decisionError.set(null);
    this.productsApi.setSalePrice(productId, { salePriceCents: priceCents, purchaseId: purchase.id }).subscribe({
      next: () => this.setDecision(productId, 'used'),
      error: (err: unknown) => {
        this.setDecision(productId, 'pending');
        this.decisionError.set(toApiError(err).message);
      },
    });
  }

  protected keepPrice(productId: string): void {
    this.setDecision(productId, 'kept');
  }

  /** "Registrar otra": same supplier, date, rate and boxes (several purchases of one trip). */
  protected startAnother(): void {
    this.confirmed.set(null);
    this.decisions.set({});
    this.submitted.set(false);
    this.error.set(null);
    this.lines.clear();
    this.lines.push(this.newLine());
    this.charges.clear();
    this.form.controls.notes.setValue('');
    this.form.markAsUntouched();
  }

  private setDecision(productId: string, decision: SuggestionDecision): void {
    this.decisions.update((all) => ({ ...all, [productId]: decision }));
  }

  private allowsDecimals(unit: ProductResponse['purchaseUnit']): boolean {
    return this.units().find((u) => u.code === unit)?.allowsDecimals ?? false;
  }

  /** Today from the server (the PC clock), the last rate typed in, Almacén and the first box of the currency. */
  private applyDefaults(): void {
    const current = this.rateStore.current();
    if (current) {
      this.form.controls.date.setValue(current.today);
    }
    if (current?.latest) {
      this.form.controls.rate.setValue(toInputText(current.latest.cupPerUsdCents));
    }
    const storage = this.activeLocations().find((l) => l.kind === 'Storage') ?? this.activeLocations()[0];
    this.form.controls.locationId.setValue(storage?.id ?? '');
    this.form.controls.cashBoxId.setValue(this.boxesFor(this.form.controls.currency.value)[0]?.id ?? '');
  }

  private newLine(): LineForm {
    return this.fb.group({
      productId: ['', Validators.required],
      quantity: ['', quantityValidator({ min: 1 })],
      unitCost: ['', amountValidator({ min: 1 })],
      factor: ['', optional(quantityValidator({ min: 1 }))],
    });
  }

  /** Freight is the usual one, paid in CUP. */
  private newCharge(): ChargeForm {
    const currency: CurrencyCode = 'CUP';
    return this.fb.group({
      description: ['Flete', [Validators.required, Validators.maxLength(80)]],
      // Typed control: a plain [currency] would infer the literal 'CUP' and never accept 'USD'.
      currency: this.fb.control<CurrencyCode>(currency),
      amount: ['', amountValidator({ min: 1 })],
      cashBoxId: [this.boxesFor(currency)[0]?.id ?? '', Validators.required],
    });
  }

  private toRequest(): CreatePurchaseRequest | null {
    const v = this.form.getRawValue();
    const rate = parseHundredths(v.rate);
    if (rate === null) {
      return null;
    }

    const lines = [];
    for (const line of v.lines) {
      const quantity = parseThousandths(line.quantity);
      const cost = parseHundredths(line.unitCost);
      const factor = line.factor.trim() ? parseThousandths(line.factor) : null;
      if (quantity === null || cost === null || (line.factor.trim() && factor === null)) {
        return null;
      }
      lines.push({ productId: line.productId, quantityMilli: quantity, unitCostCents: cost, conversionFactorMilli: factor });
    }

    const charges = [];
    for (const charge of v.charges) {
      const amount = parseHundredths(charge.amount);
      if (amount === null) {
        return null;
      }
      charges.push({
        description: charge.description.trim(),
        currency: charge.currency,
        amountCents: amount,
        cashBoxId: charge.cashBoxId,
      });
    }

    return {
      date: v.date,
      supplierId: v.supplierId,
      currency: v.currency,
      rateCupPerUsdCents: rate,
      locationId: v.locationId || null,
      cashBoxId: v.cashBoxId,
      notes: v.notes.trim() || null,
      lines,
      charges,
    };
  }
}
