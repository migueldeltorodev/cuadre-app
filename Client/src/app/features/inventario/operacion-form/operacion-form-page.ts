import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { KeyValuePipe } from '@angular/common';
import {
  FormArray,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable, forkJoin } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { UnitsApi } from '../../../core/api/catalog-api';
import { InventoryApi, StockOperationsApi } from '../../../core/api/inventory-api';
import {
  ExitReasonCode,
  LocationResponse,
  ProductStockResponse,
  StockOperationLineRequest,
  StockOperationResponse,
  StockOperationType,
  UnitCode,
  UnitResponse,
} from '../../../core/api/models';
import { LocationsApi } from '../../../core/api/purchases-api';
import { ExchangeRateStore } from '../../../core/state/exchange-rate-store';
import { formatMoney } from '../../../shared/format/money';
import {
  UNIT_LABELS,
  formatQuantityWithUnit,
  isWhole,
  optional,
  parseThousandths,
  quantityValidator,
} from '../../../shared/format/quantity';
import { localToday, stockQuantityMilli } from '../../compras/purchase-format';
import { EXIT_REASONS, exitReasonLabel, formatSignedQuantity, inPurchaseUnits, operationKindLabel } from '../inventory-format';

/** "stock" = typed in the stock unit (libras); "purchase" = in the purchase unit (sacos). */
type EntryUnit = 'stock' | 'purchase';

type LineForm = FormGroup<{
  productId: FormControl<string>;
  quantity: FormControl<string>;
  unit: FormControl<EntryUnit>;
}>;

/** One line as the template reads it. Display only: the server converts and checks again. */
interface LineView {
  product: ProductStockResponse | null;
  units: { value: EntryUnit; label: string }[];
  /** What is at the origin / counted location right now (all dates). */
  availableMilli: number;
  /** The typed quantity in stock units; null while blank or invalid. */
  stockMilli: number | null;
  /** "= 100 lb" when typed in sacks. */
  convertedText: string;
  /** Transfer/exit: what stays at the origin. Count: counted − expected. */
  resultMilli: number | null;
  problem: string | null;
}

interface Copy {
  title: string;
  lead: string;
  locationLabel: string;
  quantityHeader: string;
  confirm: string;
  confirming: string;
  backLink: string;
  backLabel: string;
}

const COPY: Record<StockOperationType, Copy> = {
  Transfer: {
    title: 'Llevar mercadería',
    lead: 'Baja en el origen y sube en el destino. El costo no cambia: es la misma mercadería en otro lugar.',
    locationLabel: 'Desde',
    quantityHeader: 'Cuánto se lleva',
    confirm: 'Confirmar traslado',
    confirming: 'Confirmando…',
    backLink: '/inventario/traslados',
    backLabel: 'Traslados',
  },
  Exit: {
    title: 'Registrar salida',
    lead: 'Lo que sale sin venderse. Se valora al costo promedio de hoy.',
    locationLabel: 'Sale de',
    quantityHeader: 'Cuánto sale',
    confirm: 'Confirmar salida',
    confirming: 'Confirmando…',
    backLink: '/inventario/salidas',
    backLabel: 'Salidas',
  },
  Count: {
    title: 'Conteo físico',
    lead: 'Escribí lo que encontrás. La app compara con lo que debería haber a esa fecha y ajusta la diferencia. Lo que dejes en blanco no se cuenta.',
    locationLabel: 'Qué se cuenta',
    quantityHeader: 'Contado',
    confirm: 'Confirmar conteo',
    confirming: 'Confirmando…',
    backLink: '/inventario/conteos',
    backLabel: 'Conteos',
  },
};

/**
 * New transfer, exit or count (RF-INV-03/04/08), chosen by route data "kind". Confirming moves the stock at once.
 * A location left negative is allowed (decision 2026-11-12): it is warned here while typing and again in the result.
 */
@Component({
  imports: [ReactiveFormsModule, RouterLink, KeyValuePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './operacion-form-page.html',
  styleUrls: ['../inventory-shared.css', './operacion-form-page.css'],
})
export default class OperacionFormPage implements OnInit {
  /** Route data. */
  readonly kind = input.required<StockOperationType>();

  /** Query param "?ubicacion=…": the location to count (from the reminder). */
  readonly ubicacion = input<string | undefined>();

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly operationsApi = inject(StockOperationsApi);
  private readonly inventoryApi = inject(InventoryApi);
  private readonly locationsApi = inject(LocationsApi);
  private readonly unitsApi = inject(UnitsApi);
  private readonly rateStore = inject(ExchangeRateStore);

  protected readonly formatMoney = formatMoney;
  protected readonly formatQuantityWithUnit = formatQuantityWithUnit;
  protected readonly formatSignedQuantity = formatSignedQuantity;
  protected readonly exitReasonLabel = exitReasonLabel;
  protected readonly operationKindLabel = operationKindLabel;
  protected readonly reasons = EXIT_REASONS;

  protected readonly copy = computed(() => COPY[this.kind()]);

  protected readonly locations = signal<LocationResponse[]>([]);
  protected readonly products = signal<ProductStockResponse[]>([]);
  private readonly units = signal<UnitResponse[]>([]);
  protected readonly loaded = signal(false);
  protected readonly loadError = signal<ApiError | null>(null);

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly submitted = signal(false);
  protected readonly confirmed = signal<StockOperationResponse | null>(null);

  protected readonly form = this.fb.group({
    date: [localToday(), Validators.required],
    locationId: ['', Validators.required],
    toLocationId: [''],
    reason: this.fb.control<ExitReasonCode | ''>(''),
    notes: ['', Validators.maxLength(500)],
    lines: this.fb.array<LineForm>([]),
  });

  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  protected get lines(): FormArray<LineForm> {
    return this.form.controls.lines;
  }

  protected readonly activeLocations = computed(() => this.locations().filter((l) => l.isActive));

  protected readonly destinations = computed(() =>
    this.activeLocations().filter((l) => l.id !== this.value().locationId),
  );

  protected readonly isCount = computed(() => this.kind() === 'Count');

  private readonly productById = computed(() => new Map(this.products().map((p) => [p.productId, p])));

  protected readonly reasonHint = computed(
    () => EXIT_REASONS.find((r) => r.code === this.value().reason)?.hint ?? '',
  );

  protected readonly locationName = computed(
    () => this.locations().find((l) => l.id === this.value().locationId)?.name ?? '',
  );

  protected readonly lineViews = computed<LineView[]>(() => {
    const raw = this.value();
    const lines = raw.lines ?? [];
    const locationId = raw.locationId ?? '';
    const chosen = lines.map((line) => line.productId);
    const count = this.isCount();

    return lines.map((line, index) => {
      const product = line.productId ? (this.productById().get(line.productId) ?? null) : null;
      const view: LineView = {
        product,
        units: [],
        availableMilli: 0,
        stockMilli: null,
        convertedText: '',
        resultMilli: null,
        problem: null,
      };
      if (!product) {
        return view;
      }

      const stockLabel = UNIT_LABELS[product.stockUnit];
      view.units = [{ value: 'stock', label: stockLabel.short ?? stockLabel.many }];
      if (product.purchaseUnit !== product.stockUnit) {
        view.units.push({ value: 'purchase', label: UNIT_LABELS[product.purchaseUnit].many });
      }
      view.availableMilli = product.locations.find((l) => l.locationId === locationId)?.quantityMilli ?? 0;

      if (chosen.indexOf(line.productId) !== index) {
        view.problem = 'Este producto ya está en otra línea: juntá las cantidades.';
        return view;
      }

      const typed = (line.quantity ?? '').trim() ? parseThousandths(line.quantity) : null;
      if (typed === null) {
        return view;
      }

      const inPurchase = line.unit === 'purchase' && product.purchaseUnit !== product.stockUnit;
      if (inPurchase && !this.allowsDecimals(product.purchaseUnit) && !isWhole(typed)) {
        view.problem = `Por ${UNIT_LABELS[product.purchaseUnit].many} va sin decimales. Para una parte, escribila en ${stockLabel.many}.`;
        return view;
      }

      const stock = inPurchase ? stockQuantityMilli(typed, product.conversionFactorMilli) : typed;
      if (stock === null || (!this.allowsDecimals(product.stockUnit) && !isWhole(stock))) {
        view.problem = `No da un número exacto de ${stockLabel.many}.`;
        return view;
      }

      view.stockMilli = stock;
      if (inPurchase) {
        view.convertedText = `= ${formatQuantityWithUnit(stock, product.stockUnit)}`;
      }
      // Count: what it should be (all dates; the server uses the count's date). Others: what stays.
      view.resultMilli = count ? stock - view.availableMilli : view.availableMilli - stock;
      return view;
    });
  });

  /** Transfer/exit lines that would leave the origin below zero (allowed, warned). */
  protected readonly negativeLines = computed(() =>
    this.isCount() ? 0 : this.lineViews().filter((v) => v.resultMilli !== null && v.resultMilli < 0).length,
  );

  protected readonly countedLines = computed(() => this.lineViews().filter((v) => v.stockMilli !== null).length);

  protected readonly hasLineProblems = computed(() => this.lineViews().some((v) => v.problem !== null));

  /** The confirmed operation's lines that changed something (a count shows only the differences). */
  protected readonly changedLines = computed(() => {
    const operation = this.confirmed();
    if (!operation) {
      return [];
    }
    return operation.kind === 'Count' ? operation.lines.filter((l) => l.quantityMilli !== 0) : operation.lines;
  });

  /** Count: shortage minus surplus at cost (positive = loss). Exit: value of what left. */
  protected readonly confirmedValueCents = computed(() => {
    const operation = this.confirmed();
    if (!operation || operation.kind === 'Transfer') {
      return null;
    }
    const sum = operation.lines.reduce((total, line) => total + (line.valueCupCents ?? 0), 0);
    return operation.kind === 'Count' ? -sum : sum;
  });

  ngOnInit(): void {
    forkJoin({
      locations: this.locationsApi.getAll(),
      stock: this.inventoryApi.stock({ includeInactive: true }),
      units: this.unitsApi.getAll(),
    }).subscribe({
      next: ({ locations, stock, units }) => {
        this.locations.set(locations.items);
        this.products.set([...stock.items].sort((a, b) => a.name.localeCompare(b.name, 'es')));
        this.units.set(units.items);
        this.applyDefaults();
        this.loaded.set(true);
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }

  protected addLine(productId = ''): void {
    this.lines.push(this.newLine(productId));
  }

  protected removeLine(index: number): void {
    if (this.lines.length > 1 || this.isCount()) {
      this.lines.removeAt(index);
    }
  }

  /** Changing the origin of a transfer must not leave it equal to the destination. */
  protected onLocationChange(locationId: string): void {
    if (this.kind() === 'Transfer' && this.form.controls.toLocationId.value === locationId) {
      this.form.controls.toLocationId.setValue(this.destinations()[0]?.id ?? '');
    }
    if (this.isCount()) {
      this.fillCountLines(locationId);
    }
  }

  protected invalid(control: { invalid: boolean; touched: boolean }): boolean {
    return control.invalid && (control.touched || this.submitted());
  }

  protected confirm(): void {
    this.submitted.set(true);
    this.form.markAllAsTouched();
    const lines = this.toLines();
    if (this.form.invalid || lines === null || this.hasLineProblems() || !this.extraChecks()) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.send(lines).subscribe({
      next: (operation) => {
        this.confirmed.set(operation);
        this.saving.set(false);
      },
      error: (err: unknown) => {
        this.error.set(toApiError(err));
        this.saving.set(false);
      },
    });
  }

  /** "Registrar otro": same date and locations, fresh lines (and fresh stock to compare with). */
  protected startAnother(): void {
    this.confirmed.set(null);
    this.submitted.set(false);
    this.error.set(null);
    this.form.controls.notes.setValue('');
    this.lines.clear();
    this.form.markAsUntouched();
    this.inventoryApi.stock({ includeInactive: true }).subscribe({
      next: (stock) => {
        this.products.set([...stock.items].sort((a, b) => a.name.localeCompare(b.name, 'es')));
        if (this.isCount()) {
          this.fillCountLines(this.form.controls.locationId.value);
        } else {
          this.addLine();
        }
      },
      error: (err: unknown) => this.error.set(toApiError(err)),
    });
  }

  /** Active products not in a line yet: the count's "agregar producto" (one that is not where the ledger says). */
  protected productsNotInLines(): ProductStockResponse[] {
    const used = new Set(this.lines.controls.map((line) => line.controls.productId.value));
    return this.products().filter((p) => p.isActive && !used.has(p.productId));
  }

  protected altText(product: ProductStockResponse, milli: number): string {
    const sacks = inPurchaseUnits(milli, product.purchaseUnit, product.stockUnit, product.conversionFactorMilli);
    return sacks ? ` (${sacks})` : '';
  }

  private extraChecks(): boolean {
    if (this.kind() === 'Exit' && !this.form.controls.reason.value) {
      return false;
    }
    if (this.kind() === 'Transfer' && !this.form.controls.toLocationId.value) {
      return false;
    }
    return true;
  }

  private send(lines: StockOperationLineRequest[]): Observable<StockOperationResponse> {
    const v = this.form.getRawValue();
    const notes = v.notes.trim() || null;
    switch (this.kind()) {
      case 'Transfer':
        return this.operationsApi.createTransfer({
          date: v.date,
          fromLocationId: v.locationId,
          toLocationId: v.toLocationId,
          notes,
          lines,
        });
      case 'Exit':
        return this.operationsApi.createExit({
          date: v.date,
          locationId: v.locationId,
          reason: v.reason as ExitReasonCode,
          notes,
          lines,
        });
      case 'Count':
        return this.operationsApi.createCount({ date: v.date, locationId: v.locationId, notes, lines });
    }
  }

  /** Null when something does not parse. A count skips the blank lines (not counted). */
  private toLines(): StockOperationLineRequest[] | null {
    const lines: StockOperationLineRequest[] = [];
    for (const line of this.form.getRawValue().lines) {
      if (!line.productId) {
        if (this.isCount()) {
          continue;
        }
        return null;
      }
      if (this.isCount() && !line.quantity.trim()) {
        continue;
      }
      const quantity = parseThousandths(line.quantity);
      if (quantity === null) {
        return null;
      }
      lines.push({ productId: line.productId, quantityMilli: quantity, inPurchaseUnits: line.unit === 'purchase' });
    }
    return lines.length > 0 ? lines : null;
  }

  private allowsDecimals(unit: UnitCode): boolean {
    return this.units().find((u) => u.code === unit)?.allowsDecimals ?? false;
  }

  /**
   * Today from the server. Transfer: Almacén → Mostrador ("Llevar al mostrador"). Exit: the counter, where most
   * losses happen. Count: the location of the reminder, else the storage.
   */
  private applyDefaults(): void {
    const today = this.rateStore.current()?.today;
    if (today) {
      this.form.controls.date.setValue(today);
    }

    const active = this.activeLocations();
    const storage = active.find((l) => l.kind === 'Storage') ?? active[0];
    const counter = active.find((l) => l.kind === 'Counter') ?? active[0];

    switch (this.kind()) {
      case 'Transfer':
        this.form.controls.locationId.setValue(storage?.id ?? '');
        this.form.controls.toLocationId.setValue(counter && counter.id !== storage?.id ? counter.id : '');
        this.addLine();
        break;
      case 'Exit':
        this.form.controls.locationId.setValue(counter?.id ?? '');
        this.addLine();
        break;
      case 'Count': {
        const asked = active.find((l) => l.id === this.ubicacion());
        const location = asked ?? storage;
        this.form.controls.locationId.setValue(location?.id ?? '');
        this.fillCountLines(location?.id ?? '');
        break;
      }
    }
  }

  /** A count starts with every product that the ledger says is at that location. */
  private fillCountLines(locationId: string): void {
    this.lines.clear();
    for (const product of this.products()) {
      if (product.locations.some((l) => l.locationId === locationId && l.quantityMilli !== 0)) {
        this.lines.push(this.newLine(product.productId));
      }
    }
  }

  private newLine(productId = ''): LineForm {
    const count = this.isCount();
    return this.fb.group({
      productId: [productId, count ? [] : [Validators.required]],
      quantity: ['', count ? [optional(quantityValidator({ min: 0 }))] : [quantityValidator({ min: 1 })]],
      unit: this.fb.control<EntryUnit>('stock'),
    });
  }
}
