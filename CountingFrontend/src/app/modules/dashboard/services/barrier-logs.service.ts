import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface BarrierLog {
  id: number;
  timestamp: string;
  event: string;
  image_name: string | null;
  camera_index: number;
}

@Injectable({
  providedIn: 'root'
})
export class BarrierLogsService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getLogs(fromDate: string, toDate: string, eventText?: string): Observable<BarrierLog[]> {
    let params = new HttpParams()
      .set('from_date', fromDate)
      .set('to_date', toDate);
    
    if (eventText) {
      params = params.set('event_text', eventText);
    }

    return this.http.get<BarrierLog[]>(`${this.apiUrl}/barrier-logs`, { params });
  }

  getImage(imageName: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/images/${imageName}`, {
      responseType: 'blob'
    });
  }
}