import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Camera, CameraCreatePayload } from '../models/camera.model';

@Injectable({
  providedIn: 'root'
})
export class CameraService {
  private readonly apiUrl = `${environment.apiUrl}/cameras`;

  constructor(private http: HttpClient) {}

  getCameras(): Observable<Camera[]> {
    return this.http.get<Camera[]>(this.apiUrl);
  }

  addCamera(payload: CameraCreatePayload): Observable<Camera> {
    return this.http.post<Camera>(this.apiUrl, payload);
  }

  updateCamera(id: number, payload: CameraCreatePayload): Observable<any> {
    return this.http.put<any>(`${this.apiUrl}/${id}`, payload);
  }

  deleteCamera(id: number): Observable<any> {
    return this.http.delete<any>(`${this.apiUrl}/${id}`);
  }
}