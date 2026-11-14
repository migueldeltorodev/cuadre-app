/**
 * Backups (mirror of BackupResponses.cs / BackupRequests.cs, RF-DAT-01..06). Moments are ISO UTC timestamps.
 */

export type BackupReasonCode = 'Closing' | 'AppExit' | 'Manual' | 'BeforeUpdate' | 'BeforeRestore';
export type BackupLocationCode = 'Local' | 'External';

/** The most urgent first. */
export type BackupAlertCode = 'Failed' | 'NotConfigured' | 'NeverCopied' | 'Overdue';

/** Unavailable = the pendrive is not plugged in: not a failure. */
export type ExternalCopyCode = 'Copied' | 'NotConfigured' | 'Unavailable' | 'Failed';

export interface BackupEntryResponse {
  fileName: string;
  location: BackupLocationCode;
  createdAt: string;
  reason: BackupReasonCode;
  sizeBytes: number;
}

export interface BackupMarkResponse {
  at: string;
  fileName: string;
}

export interface BackupProblemResponse {
  at: string;
  message: string;
}

export interface BackupAlertResponse {
  kind: BackupAlertCode;
  /** Null when nothing ever reached the pendrive. */
  daysSinceExternal: number | null;
}

export interface BackupStatusResponse {
  externalDirectory: string | null;
  alertAfterDays: number;
  /** The pendrive folder is there right now. */
  externalAvailable: boolean;
  lastLocal: BackupMarkResponse | null;
  lastExternal: BackupMarkResponse | null;
  lastProblem: BackupProblemResponse | null;
  /** Null = all good. */
  alert: BackupAlertResponse | null;
  /** PC and pendrive, the newest first. The pendrive's only while it is plugged in. */
  backups: BackupEntryResponse[];
}

export interface BackupResultResponse {
  backup: BackupEntryResponse;
  external: ExternalCopyCode;
  externalMessage: string | null;
}

export interface RestoreResultResponse {
  restored: BackupEntryResponse;
  restoredCreatedAt: string;
  safetyCopy: BackupEntryResponse;
}

export interface RemovableDriveResponse {
  rootPath: string;
  label: string;
  totalBytes: number;
  freeBytes: number;
}

export interface UpdateBackupSettingsRequest {
  externalDirectory: string | null;
  alertAfterDays: number;
}

export interface RestoreBackupRequest {
  location: BackupLocationCode;
  fileName: string;
}
