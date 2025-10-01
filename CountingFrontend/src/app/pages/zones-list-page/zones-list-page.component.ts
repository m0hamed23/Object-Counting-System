import { Component, OnInit } from '@angular/core';
import { Observable } from 'rxjs';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

import { ZoneService } from '../../core/services/zone.service';
import { Zone } from '../../core/models/zone.model';

@Component({
  selector: 'app-zones-list-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './zones-list-page.component.html',
  styleUrl: './zones-list-page.component.css'
})
export class ZonesListPageComponent implements OnInit {
  zones$!: Observable<Zone[]>;

  constructor(private zoneService: ZoneService) {}

  ngOnInit(): void {
    this.loadZones();
  }

  loadZones(): void {
    this.zones$ = this.zoneService.getZones();
  }

  onDelete(id: number): void {
    if (confirm('Are you sure you want to delete this zone?')) {
      this.zoneService.deleteZone(id).subscribe({
        next: () => this.loadZones(),
        error: (err) => alert(`Failed to delete zone: ${err.error?.message || 'Unknown error'}`)
      });
    }
  }
}