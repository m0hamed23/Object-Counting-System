import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Location, LocationCreatePayload, LocationAssociationPayload } from '../models/location.model';

@Injectable({
  providedIn: 'root'
})
export class LocationService {
  private readonly apiUrl = `${environment.apiUrl}/locations`;

  constructor(private http: HttpClient) {}

  getLocations(): Observable<Location[]> {
    return this.http.get<Location[]>(this.apiUrl);
  }

  createLocation(payload: LocationCreatePayload): Observable<Location> {
    return this.http.post<Location>(this.apiUrl, payload);
  }

  updateLocation(id: number, payload: LocationCreatePayload): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, payload);
  }

  deleteLocation(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  addZoneToLocation(locationId: number, payload: LocationAssociationPayload): Observable<any> {
    return this.http.post(`${this.apiUrl}/${locationId}/zones`, payload);
  }

  removeZoneFromLocation(locationId: number, zoneId: number): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${locationId}/zones/${zoneId}`);
  }
}