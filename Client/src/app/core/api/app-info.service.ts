import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

/**
 * Mirrors Cuadre.Contracts.Responses.AppInfoResponse.
 * Later these types are GENERATED from /openapi/v1.json instead of written by hand.
 */
export interface AppInfoResponse {
  name: string;
  version: string;
  environment: string;
}

@Injectable({ providedIn: 'root' })
export class AppInfoService {
  private readonly http = inject(HttpClient);

  getInfo(): Observable<AppInfoResponse> {
    return this.http.get<AppInfoResponse>('/api/app/info');
  }
}
