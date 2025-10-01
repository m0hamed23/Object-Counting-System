import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, tap, shareReplay } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Setting } from '../models/setting.model';

@Injectable({
  providedIn: 'root'
})
export class SettingService {
  private readonly apiUrl = `${environment.apiUrl}/settings`;
  private settingsCache$: Observable<Setting[]> | null = null;

  constructor(private http: HttpClient) {}

  getSettings(): Observable<Setting[]> {
    if (!this.settingsCache$) {
      this.settingsCache$ = this.http.get<Setting[]>(this.apiUrl).pipe(
        shareReplay(1)
      );
    }
    return this.settingsCache$;
  }

  updateSettings(settings: Setting[]): Observable<any> {
    return this.http.put<any>(this.apiUrl, settings).pipe(
      tap(() => {
        // Invalidate cache on update
        this.settingsCache$ = null;
      })
    );
  }

  clearCache() {
    this.settingsCache$ = null;
  }
}