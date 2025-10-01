import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Zone, ZoneCreatePayload, ZoneAssociationPayload } from '../models/zone.model';

@Injectable({
  providedIn: 'root'
})
export class ZoneService {
  private readonly apiUrl = `${environment.apiUrl}/zones`;

  constructor(private http: HttpClient) {}

  getZones(): Observable<Zone[]> {
    return this.http.get<Zone[]>(this.apiUrl);
  }

  createZone(payload: ZoneCreatePayload): Observable<Zone> {
    return this.http.post<Zone>(this.apiUrl, payload);
  }

  updateZone(id: number, payload: ZoneCreatePayload): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, payload);
  }

  deleteZone(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  addCameraToZone(zoneId: number, payload: ZoneAssociationPayload): Observable<any> {
    return this.http.post(`${this.apiUrl}/${zoneId}/cameras`, payload);
  }

  removeCameraFromZone(zoneId: number, cameraId: number): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${zoneId}/cameras/${cameraId}`);
  }
}