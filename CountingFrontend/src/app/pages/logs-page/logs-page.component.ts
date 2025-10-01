
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
    // Format required for <input type="datetime-local"> is 'yyyy-MM-ddTHH:mm'
    return formatDate(date, 'yyyy-MM-ddTHH:mm', 'en-US');
  }

  private getDefaultFromDate(): string {
    const date = new Date();
    date.setHours(0, 0, 0, 0); // Start of today
    return this.formatDateForInput(date);
  }

  private getDefaultToDate(): string {
    const date = new Date();
    date.setHours(23, 59, 59, 999); // End of today
    return this.formatDateForInput(date);
  }

  loadLogs(): void {
    const from = this.fromDate.value || '';
    const to = this.toDate.value || '';
    const filter = this.eventFilter.value || undefined;
    this.logService.getLogs(from, to, filter).subscribe(logs => this.logs = logs);
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
}