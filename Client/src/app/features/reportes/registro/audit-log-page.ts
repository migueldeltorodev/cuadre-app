import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { AuditApi } from '../../../core/api/audit-api';
import { AuditEventKindCode, AuditEventResponse, AuditSubjectCode } from '../../../core/api/audit-models';
import { PagedResponse } from '../../../core/api/models';
import { addDays } from '../../sobres/envelope-format';
import { Period, PeriodFilter } from '../period-filter';

const PAGE_SIZE = 50;

const DAY = new Intl.DateTimeFormat('es', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
const TIME = new Intl.DateTimeFormat('es', { hour: '2-digit', minute: '2-digit' });
const ISO_DAY = new Intl.DateTimeFormat('en-CA', { year: 'numeric', month: '2-digit', day: '2-digit' });

/** The kinds, in the order of the filter, with their words and tone. */
const KINDS: { code: AuditEventKindCode; label: string; tone: 'bad' | 'warn' | 'muted' }[] = [
  { code: 'Voided', label: 'Anulación', tone: 'bad' },
  { code: 'DayReopened', label: 'Día reabierto', tone: 'warn' },
  { code: 'PriceChanged', label: 'Precio', tone: 'muted' },
  { code: 'ProfitFirstPlan', label: 'Plan Profit First', tone: 'muted' },
  { code: 'PayScheme', label: 'Pago del personal', tone: 'muted' },
  { code: 'ConfigChanged', label: 'Configuración', tone: 'muted' },
];

const FILTER_LABELS: Record<AuditEventKindCode, string> = {
  Voided: 'Anulaciones',
  DayReopened: 'Días reabiertos',
  PriceChanged: 'Precios',
  ProfitFirstPlan: 'Profit First',
  PayScheme: 'Pago del personal',
  ConfigChanged: 'Configuración',
};

interface AuditEntryView extends AuditEventResponse {
  time: string;
  kindLabel: string;
  tone: 'bad' | 'warn' | 'muted';
  link: string[] | null;
}

/** Where each entry takes you. Those without an id go to their Configuración section. */
function linkFor(entry: AuditEventResponse): string[] | null {
  const id = entry.subjectId;
  const withId = (base: string) => (id ? [base, id] : null);
  const subjects: Record<AuditSubjectCode, () => string[] | null> = {
    Purchase: () => withId('/compras'),
    StockOperation: () => withId('/inventario/operaciones'),
    EnvelopeOperation: () => withId('/sobres/operaciones'),
    Expense: () => withId('/gastos'),
    ShortageRecovery: () => withId('/cierre/faltantes'),
    DailyClosing: () => withId('/cierre'),
    Product: () => withId('/productos'),
    AllocationPlan: () => ['/configuracion/profit-first'],
    StaffPayScheme: () => ['/configuracion/personal'],
    Settings: () => ['/configuracion/negocio'],
    CashBox: () => ['/configuracion/cobros-y-cajas'],
    PaymentMethod: () => ['/configuracion/cobros-y-cajas'],
    ExchangeRate: () => ['/configuracion/precios-y-moneda'],
    Envelope: () => ['/configuracion/profit-first'],
    Employee: () => ['/configuracion/personal'],
    ExpenseCategory: () => ['/configuracion/gastos'],
    Tax: () => ['/configuracion/impuestos'],
  };
  return subjects[entry.subject]();
}

/**
 * RF-DAT-08 (D-59): what was voided, which days were reopened, which prices and which settings changed.
 * Grouped by the PC's day, the newest first. Read-only: the actions themselves write it.
 */
@Component({
  imports: [RouterLink, PeriodFilter],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './audit-log-page.html',
  styleUrls: ['../../../shared/ui/list-page.css', '../report-page.css', './audit-log-page.css'],
})
export default class AuditLogPage implements OnInit {
  private readonly api = inject(AuditApi);

  protected readonly kinds = KINDS;
  protected readonly filterLabels = FILTER_LABELS;

  /** This PC's today: the server runs on the same PC. */
  protected readonly today = ISO_DAY.format(new Date());
  protected readonly period = signal<Period>({ from: addDays(this.today, -29), to: this.today });
  protected readonly kind = signal<AuditEventKindCode | null>(null);
  protected readonly page = signal(1);

  protected readonly result = signal<PagedResponse<AuditEventResponse> | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly loading = signal(false);

  /** The page's entries grouped by day, each with its time, kind and link. */
  protected readonly days = computed(() => {
    const groups: { key: string; label: string; entries: AuditEntryView[] }[] = [];
    for (const entry of this.result()?.items ?? []) {
      const at = new Date(entry.at);
      const key = ISO_DAY.format(at);
      let group = groups.at(-1);
      if (group?.key !== key) {
        const label = DAY.format(at);
        group = { key, label: label.charAt(0).toUpperCase() + label.slice(1), entries: [] };
        groups.push(group);
      }
      group.entries.push(this.decorate(entry));
    }
    return groups;
  });

  ngOnInit(): void {
    this.load();
  }

  protected setPeriod(period: Period): void {
    this.period.set(period);
    this.page.set(1);
    this.load();
  }

  protected setKind(kind: AuditEventKindCode | null): void {
    this.kind.set(kind);
    this.page.set(1);
    this.load();
  }

  protected goTo(page: number): void {
    this.page.set(page);
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    const { from, to } = this.period();
    this.api
      .events({ from, to, kind: this.kind() ?? undefined, page: this.page(), pageSize: PAGE_SIZE })
      .subscribe({
        next: (result) => {
          this.result.set(result);
          this.loadError.set(null);
          this.loading.set(false);
        },
        error: (err: unknown) => {
          this.loadError.set(toApiError(err));
          this.loading.set(false);
        },
      });
  }

  private decorate(entry: AuditEventResponse): AuditEntryView {
    const kind = KINDS.find((k) => k.code === entry.kind)!;
    return {
      ...entry,
      time: TIME.format(new Date(entry.at)),
      kindLabel: kind.label,
      tone: kind.tone,
      link: linkFor(entry),
    };
  }
}
