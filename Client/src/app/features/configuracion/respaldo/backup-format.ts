import {
  BackupAlertResponse,
  BackupLocationCode,
  BackupProblemResponse,
  BackupReasonCode,
} from '../../../core/api/backup-models';

/** Words for the owner, shared by the backup screen and the home alert (RF-DAT-06). */

const DATE_TIME = new Intl.DateTimeFormat('es', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
});

/** ISO UTC → "14 nov 2026, 21:30" in the PC's time. */
export function formatBackupMoment(isoUtc: string): string {
  return DATE_TIME.format(new Date(isoUtc)).replace('.', '');
}

export const REASON_LABELS: Record<BackupReasonCode, string> = {
  Closing: 'Al cerrar el día',
  AppExit: 'Al cerrar la app',
  Manual: 'A mano',
  BeforeUpdate: 'Antes de actualizar',
  BeforeRestore: 'Antes de restaurar',
};

export const LOCATION_LABELS: Record<BackupLocationCode, string> = {
  Local: 'PC',
  External: 'Pendrive',
};

/** 1536 → "1.5 KB", 2_400_000 → "2.3 MB". */
export function formatSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  const kb = bytes / 1024;
  if (kb < 1024) {
    return `${kb.toFixed(kb < 10 ? 1 : 0)} KB`;
  }
  const mb = kb / 1024;
  return `${mb.toFixed(mb < 10 ? 1 : 0)} MB`;
}

export interface BackupAlertText {
  title: string;
  detail: string;
  /** 'bad' only for a real failure; the rest are reminders. */
  tone: 'bad' | 'warn';
}

export function backupAlertText(alert: BackupAlertResponse, problem: BackupProblemResponse | null): BackupAlertText {
  switch (alert.kind) {
    case 'Failed':
      return {
        title: 'El último respaldo falló',
        detail: problem?.message ?? 'Mirá qué pasó en Configuración › Respaldo.',
        tone: 'bad',
      };
    case 'NotConfigured':
      return {
        title: 'Elegí un pendrive para los respaldos',
        detail: 'Por ahora están solo en esta PC: si se rompe, se pierde todo.',
        tone: 'warn',
      };
    case 'NeverCopied':
      return {
        title: 'Todavía no hay respaldos en el pendrive',
        detail: 'Conectalo y tocá «Respaldar ahora».',
        tone: 'warn',
      };
    case 'Overdue':
      return {
        title: `Hace ${alert.daysSinceExternal} días que no respaldás en el pendrive`,
        detail: 'Conectalo y tocá «Respaldar ahora».',
        tone: 'warn',
      };
  }
}
