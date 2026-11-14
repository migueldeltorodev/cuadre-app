import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AuditEventResponse, AuditQuery } from './audit-models';
import { PagedResponse } from './models';
import { toParams } from './query-params';

/** RF-DAT-08: the audit log, read-only. */
@Injectable({ providedIn: 'root' })
export class AuditApi {
  private readonly http = inject(HttpClient);

  events(query: AuditQuery = {}): Observable<PagedResponse<AuditEventResponse>> {
    return this.http.get<PagedResponse<AuditEventResponse>>('/api/audit', { params: toParams(query) });
  }
}
