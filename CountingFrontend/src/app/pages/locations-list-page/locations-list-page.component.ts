import { Component, OnInit } from '@angular/core';
import { Observable } from 'rxjs';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LocationService } from '../../core/services/location.service';
import { Location } from '../../core/models/location.model';

@Component({
  selector: 'app-locations-list-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './locations-list-page.component.html',
  styleUrls: ['./locations-list-page.component.css']
})
export class LocationsListPageComponent implements OnInit {
  locations$!: Observable<Location[]>;

  constructor(private locationService: LocationService) {}

  ngOnInit(): void {
    this.loadLocations();
  }

  loadLocations(): void {
    this.locations$ = this.locationService.getLocations();
  }

  onDelete(id: number): void {
    if (confirm('Are you sure you want to delete this location?')) {
      this.locationService.deleteLocation(id).subscribe({
        next: () => this.loadLocations(),
        error: (err) => alert(`Failed to delete location: ${err.error?.message || 'Unknown error'}`)
      });
    }
  }
}