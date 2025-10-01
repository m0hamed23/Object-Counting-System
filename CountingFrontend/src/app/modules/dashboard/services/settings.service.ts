import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subject, BehaviorSubject } from 'rxjs';
import { map, tap } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

export interface Setting {
  name: string;
  displayName: string; // CHANGED
  value: string;
  description: string;
  isVisible: boolean;  // CHANGED
}

@Injectable({
  providedIn: 'root'
})
export class SettingsService {
  private apiUrl = environment.apiUrl;
  private settingsSubject = new BehaviorSubject<Setting[]>([]);
  public settings$ = this.settingsSubject.asObservable();

  constructor(private http: HttpClient) {
    this.loadSettings(); // Load settings initially
  }

  private loadSettings(): void {
    this.http.get<Setting[]>(`${this.apiUrl}/settings`)
      .pipe(map(settings => settings.filter(setting => setting.isVisible)))
      .subscribe(settings => {
        this.settingsSubject.next(settings);
      });
  }

  getSettings(): Observable<Setting[]> {
    return this.settings$;
  }

  updateSettings(settings: Setting[]): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/settings`, settings).pipe(
      tap(() => {
        // Update the local settings and notify subscribers
        const currentSettings = this.settingsSubject.value;
        const updatedSettings = currentSettings.map(setting => {
          const updatedSetting = settings.find(s => s.name === setting.name);
          return updatedSetting || setting;
        });
        this.settingsSubject.next(updatedSettings);
      })
    );
  }

  // Get a specific setting value by name
  getSettingValue(name: string): Observable<string | null> {
    return this.settings$.pipe(
      map(settings => {
        const setting = settings.find(s => s.name === name);
        return setting ? setting.value : null;
      })
    );
  }
}