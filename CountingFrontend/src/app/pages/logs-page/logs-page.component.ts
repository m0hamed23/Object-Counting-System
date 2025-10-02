import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { CommonModule, formatDate } from '@angular/common';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { LogService } from '../../core/services/log.service';
import { LogEntry } from '../../core/models/log.model';

@Component({
  selector: 'app-logs-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './logs-page.component.html',
  styleUrl: './logs-page.component.css'
})
export class LogsPageComponent implements OnInit {
  @ViewChild('imageDialog') imageDialog!: ElementRef<HTMLDialogElement>;

  logs: LogEntry[] = [];
  fromDate = new FormControl(this.getDefaultFromDate());
  toDate = new FormControl(this.getDefaultToDate());
  eventFilter = new FormControl('');
  currentImageUrl: SafeUrl | null = null;
  private currentObjectUrl: string | null = null;

  constructor(
    private logService: LogService,
    private sanitizer: DomSanitizer
  ) {}

  ngOnInit(): void {
    this.loadLogs();
  }

  private formatDateForInput(date: Date): string {
    return formatDate(date, 'yyyy-MM-ddTHH:mm', 'en-US');
  }

  private getDefaultFromDate(): string {
    const date = new Date();
    date.setHours(0, 0, 0, 0);
    return this.formatDateForInput(date);
  }

  private getDefaultToDate(): string {
    const date = new Date();
    date.setHours(23, 59, 59, 999); 
    return this.formatDateForInput(date);
  }

  loadLogs(): void {
    const fromLocalString = this.fromDate.value || '';
    const toLocalString = this.toDate.value || '';

    const fromUtc = fromLocalString ? new Date(fromLocalString).toISOString() : '';
    const toUtc = toLocalString ? new Date(toLocalString).toISOString() : '';
    
    const filter = this.eventFilter.value || undefined;
    this.logService.getLogs(fromUtc, toUtc, filter).subscribe(logs => this.logs = logs);
  }

  applyFilters(): void {
    this.loadLogs();
  }

  resetFilters(): void {
    this.fromDate.setValue(this.getDefaultFromDate());
    this.toDate.setValue(this.getDefaultToDate());
    this.eventFilter.setValue('');
    this.loadLogs();
  }

  showImage(imageName: string): void {
    this.logService.getImage(imageName).subscribe(blob => {
      this.revokeCurrentImageUrl();
      this.currentObjectUrl = URL.createObjectURL(blob);
      this.currentImageUrl = this.sanitizer.bypassSecurityTrustUrl(this.currentObjectUrl);
      this.imageDialog.nativeElement.showModal();
    });
  }

  closeImageDialog(): void {
    this.imageDialog.nativeElement.close();
    this.revokeCurrentImageUrl();
  }

  private revokeCurrentImageUrl(): void {
    if (this.currentObjectUrl) {
      URL.revokeObjectURL(this.currentObjectUrl);
      this.currentObjectUrl = null;
      this.currentImageUrl = null;
    }
  }

  /**
   * Converts a UTC timestamp string (which might be missing timezone info)
   * into a format the Angular date pipe can correctly interpret as UTC.
   * @param timestamp The UTC datetime string from the API.
   * @returns A string with 'Z' appended if it was missing, ensuring it's parsed as UTC.
   */
  toDisplayableUtc(timestamp: string): string {
    if (!timestamp) {
      return '';
    }
    // If the string doesn't end with Z and doesn't have a +/- timezone offset,
    // append 'Z' to ensure it's treated as UTC by the date pipe.
    if (!timestamp.endsWith('Z') && !/(\+|-)\d{2}:\d{2}$/.test(timestamp)) {
      return timestamp + 'Z';
    }
    return timestamp;
  }
}