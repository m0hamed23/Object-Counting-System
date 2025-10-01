
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Action, ActionCreatePayload } from '../models/action.model';

@Injectable({
  providedIn: 'root'
})
export class ActionService {
  private readonly apiUrl = `${environment.apiUrl}/actions`;

  constructor(private http: HttpClient) {}

  getActions(): Observable<Action[]> {
    return this.http.get<Action[]>(this.apiUrl);
  }

  addAction(payload: ActionCreatePayload): Observable<Action> {
    return this.http.post<Action>(this.apiUrl, payload);
  }

  updateAction(id: number, payload: ActionCreatePayload): Observable<any> {
    return this.http.put<any>(`${this.apiUrl}/${id}`, payload);
  }

  deleteAction(id: number): Observable<any> {
    return this.http.delete<any>(`${this.apiUrl}/${id}`);
  }
}