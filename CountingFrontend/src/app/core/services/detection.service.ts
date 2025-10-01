
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class DetectionService {
  private readonly apiUrl = `${environment.apiUrl}/detection`;

  constructor(private http: HttpClient) {}

  getAvailableClasses(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/classes`);
  }
}