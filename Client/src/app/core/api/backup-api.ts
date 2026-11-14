import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  BackupResultResponse,
  BackupStatusResponse,
  RemovableDriveResponse,
  RestoreBackupRequest,
  RestoreResultResponse,
  UpdateBackupSettingsRequest,
} from './backup-models';
import { ItemsResponse } from './models';

/** Thin HTTP wrappers for the backups (RF-DAT-01..06). Routes mirror ApiEndpoints.Backups. */
@Injectable({ providedIn: 'root' })
export class BackupApi {
  private readonly http = inject(HttpClient);

  status(): Observable<BackupStatusResponse> {
    return this.http.get<BackupStatusResponse>('/api/backups');
  }

  /** "Respaldar ahora": answers when the copy is done (seconds). */
  create(): Observable<BackupResultResponse> {
    return this.http.post<BackupResultResponse>('/api/backups', null);
  }

  /** Answers the whole status, so the screen redraws from it. */
  updateSettings(request: UpdateBackupSettingsRequest): Observable<BackupStatusResponse> {
    return this.http.put<BackupStatusResponse>('/api/backups/settings', request);
  }

  drives(): Observable<ItemsResponse<RemovableDriveResponse>> {
    return this.http.get<ItemsResponse<RemovableDriveResponse>>('/api/backups/drives');
  }

  restore(request: RestoreBackupRequest): Observable<RestoreResultResponse> {
    return this.http.post<RestoreResultResponse>('/api/backups/restore', request);
  }
}
