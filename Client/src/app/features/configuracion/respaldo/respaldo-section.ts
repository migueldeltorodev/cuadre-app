import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { BackupApi } from '../../../core/api/backup-api';
import {
  BackupEntryResponse,
  BackupResultResponse,
  BackupStatusResponse,
  RemovableDriveResponse,
} from '../../../core/api/backup-models';
import { createSaveFeedback } from '../save-feedback';
import {
  LOCATION_LABELS,
  REASON_LABELS,
  backupAlertText,
  formatBackupMoment,
  formatSize,
} from './backup-format';

/** Survives the reload after a restore, so the owner sees what happened. */
const RESTORED_KEY = 'cuadre.backup.restored';

/** The folder suggested when a pendrive is picked: E:\ → E:\Cuadre respaldos. */
const FOLDER_NAME = 'Cuadre respaldos';

/**
 * RF-DAT-01..06: the backups. Make one now, choose the pendrive folder and when to warn, see the copies and restore
 * one. Restoring replaces ALL the data, so it asks first, in the page, and reloads the whole app after.
 */
@Component({
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './respaldo-section.html',
  styleUrl: './respaldo-section.css',
})
export default class RespaldoSection implements OnInit {
  private readonly api = inject(BackupApi);

  protected readonly formatBackupMoment = formatBackupMoment;
  protected readonly formatSize = formatSize;
  protected readonly reasonLabels = REASON_LABELS;
  protected readonly locationLabels = LOCATION_LABELS;

  protected readonly status = signal<BackupStatusResponse | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);

  protected readonly alert = computed(() => {
    const status = this.status();
    return status?.alert ? backupAlertText(status.alert, status.lastProblem) : null;
  });

  // ---------- Respaldar ahora ----------

  protected readonly creating = signal(false);
  protected readonly created = signal<BackupResultResponse | null>(null);
  protected readonly createError = signal<ApiError | null>(null);

  // ---------- Pendrive ----------

  protected readonly feedback = createSaveFeedback();
  private readonly submitted = signal(false);
  protected readonly drives = signal<RemovableDriveResponse[] | null>(null);
  protected readonly searchingDrives = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    externalDirectory: ['', Validators.maxLength(240)],
    alertAfterDays: [7, [Validators.required, Validators.min(1), Validators.max(60), Validators.pattern(/^\d+$/)]],
  });

  // ---------- Restore ----------

  protected readonly confirming = signal<BackupEntryResponse | null>(null);
  protected readonly restoring = signal(false);
  protected readonly restoreError = signal<ApiError | null>(null);
  protected readonly restoredNotice = signal<string | null>(null);

  ngOnInit(): void {
    this.readRestoredNotice();
    this.load();
    this.searchDrives();
  }

  protected load(): void {
    this.api.status().subscribe({
      next: (status) => this.show(status),
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }

  protected backUpNow(): void {
    this.creating.set(true);
    this.created.set(null);
    this.createError.set(null);
    this.api.create().subscribe({
      next: (result) => {
        this.creating.set(false);
        this.created.set(result);
        this.load();
      },
      error: (err: unknown) => {
        this.creating.set(false);
        this.createError.set(toApiError(err));
        this.load(); // the failure is now in the status (the home alert too)
      },
    });
  }

  // ---------- Pendrive ----------

  protected searchDrives(): void {
    this.searchingDrives.set(true);
    this.api.drives().subscribe({
      next: (drives) => {
        this.searchingDrives.set(false);
        this.drives.set(drives.items);
      },
      error: () => {
        this.searchingDrives.set(false);
        this.drives.set([]);
      },
    });
  }

  protected useDrive(drive: RemovableDriveResponse): void {
    const control = this.form.controls.externalDirectory;
    control.setValue(`${drive.rootPath}${FOLDER_NAME}`);
    control.markAsDirty();
  }

  protected driveLabel(drive: RemovableDriveResponse): string {
    const name = drive.label ? `${drive.rootPath} · ${drive.label}` : drive.rootPath;
    return `${name} (${formatSize(drive.freeBytes)} libres)`;
  }

  protected invalidDays(): boolean {
    const control = this.form.controls.alertAfterDays;
    return control.invalid && (control.touched || this.submitted());
  }

  protected fieldError(name: 'externalDirectory' | 'alertAfterDays'): string | null {
    return this.feedback.error()?.fields[name] ?? null;
  }

  protected save(): void {
    this.submitted.set(true);
    if (this.form.invalid) {
      return;
    }

    const value = this.form.getRawValue();
    const folder = value.externalDirectory.trim();
    this.feedback.start();
    this.api
      .updateSettings({ externalDirectory: folder === '' ? null : folder, alertAfterDays: Number(value.alertAfterDays) })
      .subscribe({
        next: (status) => {
          this.form.markAsPristine();
          this.submitted.set(false);
          this.show(status);
          this.feedback.succeed();
        },
        error: (err: unknown) => this.feedback.fail(toApiError(err)),
      });
  }

  // ---------- Restore ----------

  protected askRestore(entry: BackupEntryResponse): void {
    this.restoreError.set(null);
    this.confirming.set(entry);
  }

  protected cancelRestore(): void {
    this.confirming.set(null);
    this.restoreError.set(null);
  }

  protected isConfirming(entry: BackupEntryResponse): boolean {
    const current = this.confirming();
    return current?.fileName === entry.fileName && current.location === entry.location;
  }

  protected confirmRestore(): void {
    const entry = this.confirming();
    if (!entry) {
      return;
    }

    this.restoring.set(true);
    this.restoreError.set(null);
    this.api.restore({ location: entry.location, fileName: entry.fileName }).subscribe({
      next: (result) => {
        // Every screen and store holds data that no longer exists: start the app over.
        this.saveRestoredNotice(
          `Listo: los datos volvieron al ${formatBackupMoment(result.restoredCreatedAt)}. ` +
            `Lo que había antes quedó en el respaldo «antes de restaurar» del ` +
            `${formatBackupMoment(result.safetyCopy.createdAt)}, por si te arrepentís.`,
        );
        window.location.reload();
      },
      error: (err: unknown) => {
        this.restoring.set(false);
        this.restoreError.set(toApiError(err));
      },
    });
  }

  // ---------- Helpers ----------

  private show(status: BackupStatusResponse): void {
    this.status.set(status);
    this.loadError.set(null);
    if (this.form.pristine) {
      this.form.reset({
        externalDirectory: status.externalDirectory ?? '',
        alertAfterDays: status.alertAfterDays,
      });
    }
  }

  private saveRestoredNotice(text: string): void {
    try {
      sessionStorage.setItem(RESTORED_KEY, text);
    } catch {
      // Without storage the reload simply shows no notice.
    }
  }

  private readRestoredNotice(): void {
    try {
      const text = sessionStorage.getItem(RESTORED_KEY);
      if (text) {
        sessionStorage.removeItem(RESTORED_KEY);
        this.restoredNotice.set(text);
      }
    } catch {
      // Ignored: see saveRestoredNotice.
    }
  }
}
