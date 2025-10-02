
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SettingService } from '../../core/services/setting.service';
import { DetectionService } from '../../core/services/detection.service';
import { Setting } from '../../core/models/setting.model';

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './settings-page.component.html',
  styleUrl: './settings-page.component.css'
})
export class SettingsPageComponent implements OnInit {
  settings: Setting[] = [];
  private originalSettings = '';
  hasChanges = false;

  availableClasses: string[] = [];
  selectedClasses: string[] = [];
  
  availableFilter: string = '';
  selectedFilter: string = '';

  constructor(
    private settingService: SettingService,
    private detectionService: DetectionService
  ) {}

  ngOnInit(): void {
    this.loadAvailableClasses();
    this.loadSettings();
  }

  loadSettings(): void {
    this.settingService.getSettings().subscribe(settings => {
      this.settings = JSON.parse(JSON.stringify(settings)); 
      this.originalSettings = JSON.stringify(this.settings);
      this.hasChanges = false;
      this.initializeClassLists();
    });
  }

  loadAvailableClasses(): void {
    this.detectionService.getAvailableClasses().subscribe(classes => {
      this.availableClasses = classes.sort();
      this.initializeClassLists();
    });
  }

  private initializeClassLists(): void {
    const targetClassesSetting = this.settings.find(s => s.name === 'target_classes');
    if (this.availableClasses.length > 0 && targetClassesSetting) {
        const selected = new Set((targetClassesSetting.value || '').split(',').map(c => c.trim()).filter(Boolean));
        this.selectedClasses = this.availableClasses.filter(c => selected.has(c)).sort();
    }
  }
  
  get filteredAvailableClasses(): string[] {
    const selectedSet = new Set(this.selectedClasses);
    return this.availableClasses.filter(c => 
        !selectedSet.has(c) && c.toLowerCase().includes(this.availableFilter.toLowerCase())
    );
  }

  get filteredSelectedClasses(): string[] {
    return this.selectedClasses.filter(c => c.toLowerCase().includes(this.selectedFilter.toLowerCase()));
  }

  onSettingChange(): void {
    this.hasChanges = JSON.stringify(this.settings) !== this.originalSettings;
  }
  
  moveToSelected(className: string): void {
    if (!this.selectedClasses.includes(className)) {
        this.selectedClasses.push(className);
        this.selectedClasses.sort();
        this.updateTargetClassesSetting();
    }
  }

  moveToAvailable(className: string): void {
    this.selectedClasses = this.selectedClasses.filter(c => c !== className);
    this.updateTargetClassesSetting();
  }

  moveAllToSelected(): void {
    this.selectedClasses = [...this.availableClasses].sort();
    this.updateTargetClassesSetting();
  }

  moveAllToAvailable(): void {
    this.selectedClasses = [];
    this.updateTargetClassesSetting();
  }
  
  private updateTargetClassesSetting(): void {
    const targetClassesSetting = this.settings.find(s => s.name === 'target_classes');
    if (targetClassesSetting) {
      targetClassesSetting.value = this.selectedClasses.join(',');
      this.onSettingChange(); 
    }
  }

  saveSettings(): void {
    if (this.hasChanges) {
      this.settingService.updateSettings(this.settings).subscribe({
        next: () => {
          alert('Settings updated successfully.');
          this.loadSettings(); 
        },
        error: (err) => alert(`Failed to update settings: ${err.error?.message || 'Unknown error'}`)
      });
    }
  }
}