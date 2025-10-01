
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LogEntry } from '../models/log.model';

@Injectable({
  providedIn: 'root'
})
export class LogService {
  private readonly apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getLogs(fromDate: string, toDate: string, eventText?: string): Observable<LogEntry[]> {
    let params = new HttpParams()
      .set('from_date', fromDate)
      .set('to_date', toDate);
    
    if (eventText) {
      params = params.set('event_text', eventText);
    }

    // --- FIX IS HERE: Changed 'barrier-logs' to 'logs' to match the correct backend endpoint ---
    return this.http.get<LogEntry[]>(`${this.apiUrl}/logs`, { params });
  }

  getImage(imageName: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/images/${imageName}`, {
      responseType: 'blob'
    });
  }
}