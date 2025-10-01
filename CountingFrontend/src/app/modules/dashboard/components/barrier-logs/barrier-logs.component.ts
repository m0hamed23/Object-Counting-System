import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { BarrierLogsService, BarrierLog } from '../../services/barrier-logs.service';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';

@Component({
  selector: 'app-barrier-logs',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="logs-container">
      <div class="filters">
        <div class="date-filters">
          <div class="form-group">
            <label>From Date:</label>
            <input type="date" [formControl]="fromDate">
          </div>
          <div class="form-group">
            <label>To Date:</label>
            <input type="date" [formControl]="toDate">
          </div>
        </div>
        <div class="text-filter form-group">
          <label>Event Text:</label>
          <input type="text" [formControl]="eventFilter" placeholder="Filter by event text...">
        </div>
        <button (click)="applyFilters()">Filter</button>
      </div>

      <div class="logs-table-container">
        <table>
          <thead>
            <tr>
              <th>Timestamp</th>
              <th>Event</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let log of logs">
              <td>{{log.timestamp}}</td>
              <td>{{log.event}}</td>
              <td>
                <button *ngIf="log.image_name" (click)="showImage(log.image_name)">
                  View Image
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Image Preview Dialog -->
      <dialog #imageDialog class="image-dialog">
        <div class="image-container">
          <img [src]="currentImageUrl" *ngIf="currentImageUrl">
          <div class="dialog-buttons">
            <button (click)="closeImageDialog()">Close</button>
          </div>
        </div>
      </dialog>
    </div>
  `,
  styles: [`
    .logs-container {
      padding: 1rem;
    }
    .filters {
      display: flex;
      gap: 1rem;
      margin-bottom: 1.5rem;
      align-items: flex-end;
    }
    .date-filters {
      display: flex;
      gap: 1rem;
    }
    .form-group {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .text-filter {
      flex: 1;
    }
    input {
      padding: 0.5rem;
      border: 1px solid #ddd;
      border-radius: 4px;
      font-size: 14px;
    }
    button {
      padding: 0.5rem 1rem;
      background-color: #e0e0e0;
      border: 1px solid #adadad;
      border-radius: 4px;
      cursor: pointer;
      height: fit-content;
    }
    button:hover {
      background-color: #d0d0d0;
    }
    .logs-table-container {
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
    .image-dialog {
      padding: 0;
      border: none;
      border-radius: 8px;
      overflow: hidden;
      max-width: 90vw;
      max-height: 90vh;
    }
    .image-dialog::backdrop {
      background: rgba(0,0,0,0.5);
    }
    .image-container {
      position: relative;
      display: flex;
      flex-direction: column;
    }
    .image-container img {
      max-width: 100%;
      max-height: 80vh;
      object-fit: contain;
    }
    .dialog-buttons {
      padding: 1rem;
      text-align: right;
      background: white;
    }
  `]
})
export class BarrierLogsComponent implements OnInit {
  @ViewChild('imageDialog') imageDialog!: ElementRef<HTMLDialogElement>;

  logs: BarrierLog[] = [];
  fromDate = new FormControl(this.getDefaultFromDate());
  toDate = new FormControl(new Date().toISOString().split('T')[0]);
  eventFilter = new FormControl('');
  currentImageUrl: SafeUrl | null = null;

  constructor(
    private barrierLogsService: BarrierLogsService,
    private sanitizer: DomSanitizer
  ) {}

  ngOnInit() {
    this.loadLogs();
  }

  private getDefaultFromDate(): string {
    const date = new Date();
    date.setDate(date.getDate() - 7); // Default to last 7 days
    return date.toISOString().split('T')[0];
  }

  loadLogs() {
    const from = this.fromDate.value || this.getDefaultFromDate();
    const to = this.toDate.value || new Date().toISOString().split('T')[0];
    const filter = this.eventFilter.value || undefined;

    this.barrierLogsService.getLogs(from, to, filter)
      .subscribe(logs => {
        this.logs = logs;
      });
  }

  applyFilters() {
    this.loadLogs();
  }

  showImage(imageName: string) {
    this.barrierLogsService.getImage(imageName).subscribe(blob => {
      const objectURL = URL.createObjectURL(blob);
      this.currentImageUrl = this.sanitizer.bypassSecurityTrustUrl(objectURL);
      this.imageDialog.nativeElement.showModal();
    });
  }

  closeImageDialog() {
    this.imageDialog.nativeElement.close();
    if (this.currentImageUrl) {
      const url = this.sanitizer.sanitize(4, this.currentImageUrl); // 4 for URL
      if (url && url.startsWith('blob:')) {
        URL.revokeObjectURL(url);
      }
    }
    this.currentImageUrl = null;
  }
}