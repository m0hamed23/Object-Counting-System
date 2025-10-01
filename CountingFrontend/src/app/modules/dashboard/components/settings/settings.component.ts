import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SettingsService, Setting } from '../../services/settings.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="settings-container">
      <h2>Settings</h2>
      
      <div class="table-container">
        <table>
          <thead>
            <tr>
              <th>Setting</th>
              <th>Value</th>
              <th>Description</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let setting of settings">
              <td>{{setting.displayName}}</td>
              <td>
                <input type="text" [(ngModel)]="setting.value" (ngModelChange)="onSettingChange()" [title]="setting.description">
              </td>
              <td>{{setting.description}}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="button-row">
        <button (click)="saveSettings()" [disabled]="!hasChanges">Save Changes</button>
      </div>
    </div>
  `,
  styles: [`
    .settings-container {
      padding: 1rem;
    }
    .table-container {
      margin: 1rem 0;
      overflow-x: auto;
    }
    table {
      width: 100%;
      border-collapse: collapse;
      background: white;
    }
    th, td {
      padding: 0.75rem;
      text-align: left;
      border: 1px solid #ddd;
    }
    th {
      background-color: #f0f0f0;
      font-weight: bold;
    }
    input {
      width: 100%;
      padding: 0.5rem;
      border: 1px solid #ddd;
      border-radius: 4px;
      font-size: 14px;
    }
    .button-row {
      margin-top: 1rem;
      text-align: right;
    }
    button {
      padding: 0.5rem 1rem;
      background-color: #e0e0e0;
      border: 1px solid #adadad;
      border-radius: 4px;
      cursor: pointer;
    }
    button:hover {
      background-color: #d0d0d0;
    }
    button:disabled {
      opacity: 0.7;
      cursor: not-allowed;
    }
  `]
})
export class SettingsComponent implements OnInit {
  settings: Setting[] = [];
  private _originalSettings: string = '';
  hasChanges = false;

  constructor(private settingsService: SettingsService) {}

  ngOnInit() {
    this.settingsService.settings$.subscribe(settings => {
      this.settings = settings;
      this._originalSettings = JSON.stringify(settings);
      this.hasChanges = false;
    });
  }

  onSettingChange() {
    this.hasChanges = JSON.stringify(this.settings) !== this._originalSettings;
  }

  saveSettings() {
    if (this.hasChanges) {
      this.settingsService.updateSettings(this.settings).subscribe(() => {
        // No need to manually update the view or reset hasChanges
        // The settings$ observable will handle that automatically
      });
    }
  }
}