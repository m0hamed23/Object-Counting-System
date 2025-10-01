import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators, FormArray } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { forkJoin, of, Observable } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { LocationService } from '../../core/services/location.service';
import { ZoneService } from '../../core/services/zone.service';
import { Zone } from '../../core/models/zone.model';
import { Location } from '../../core/models/location.model';

@Component({
  selector: 'app-location-form-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './location-form-page.component.html',
  styleUrls: ['./location-form-page.component.css']
})
export class LocationFormPageComponent implements OnInit {
  locationForm: FormGroup;
  isEditMode = false;
  private locationId: number | null = null;
  
  allZones: Zone[] = [];
  private originalZoneIds = new Set<number>();

  constructor(
    private fb: FormBuilder,
    private locationService: LocationService,
    private zoneService: ZoneService,
    private router: Router,
    private route: ActivatedRoute
  ) {
    this.locationForm = this.fb.group({
      name: ['', Validators.required],
      zones: this.fb.array([])
    });
  }

  ngOnInit(): void {
    this.loadInitialData();
  }

  get zonesArray(): FormArray {
    return this.locationForm.get('zones') as FormArray;
  }

  private loadInitialData(): void {
    this.zoneService.getZones().subscribe(zones => {
      this.allZones = zones;
      this.zonesArray.clear();
      this.allZones.forEach(() => this.zonesArray.push(this.fb.control(false)));

      const idParam = this.route.snapshot.paramMap.get('id');
      if (idParam) {
        this.locationId = Number(idParam);
        this.isEditMode = true;
        this.loadLocationForEditing();
      }
    });
  }

  private loadLocationForEditing(): void {
    this.locationService.getLocations().subscribe(locations => {
      const locationToEdit = locations.find(loc => loc.id === this.locationId);
      if (locationToEdit) {
        this.locationForm.patchValue({ name: locationToEdit.name });
        this.originalZoneIds = new Set(locationToEdit.zones.map(z => z.id));
        this.allZones.forEach((zone, i) => {
          this.zonesArray.at(i).setValue(this.originalZoneIds.has(zone.id));
        });
      }
    });
  }

  onSubmit(): void {
    if (this.locationForm.invalid) return;

    if (this.isEditMode) {
        this.handleUpdate();
    } else {
        this.handleCreate();
    }
  }

  private handleCreate(): void {
    const name = this.locationForm.value.name;
    this.locationService.createLocation({ name }).pipe(
      switchMap((newLocation: Location) => {
        const selectedZoneIds = this.getSelectedZoneIds();
        const associationOps = this.getAssociationObservables(newLocation.id, selectedZoneIds);
        return associationOps.length > 0 ? forkJoin(associationOps) : of(null);
      })
    ).subscribe({
      next: () => this.router.navigate(['/locations']),
      error: (err: any) => alert(`Failed to create location: ${err.error?.message}`)
    });
  }

  private handleUpdate(): void {
    const name = this.locationForm.value.name;
    const selectedZoneIds = this.getSelectedZoneIds();
    
    // First, update the name.
    this.locationService.updateLocation(this.locationId!, { name }).pipe(
        // Then, update the associations.
        switchMap(() => {
            const associationOps = this.getAssociationObservables(this.locationId!, selectedZoneIds);
            return associationOps.length > 0 ? forkJoin(associationOps) : of(null);
        })
    ).subscribe({
        next: () => this.router.navigate(['/locations']),
        error: (err: any) => alert(`Failed to update location: ${err.error?.message}`)
    });
  }

  private getSelectedZoneIds(): Set<number> {
    return new Set<number>(
        this.locationForm.value.zones
            .map((checked: boolean, i: number) => checked ? this.allZones[i].id : null)
            .filter((id: number | null): id is number => id !== null)
    );
  }

  private getAssociationObservables(locationId: number, selectedIds: Set<number>): Observable<any>[] {
    const ops: Observable<any>[] = [];
    const idsToAdd = [...selectedIds].filter(id => !this.originalZoneIds.has(id));
    const idsToRemove = [...this.originalZoneIds].filter(id => !selectedIds.has(id));
    
    idsToAdd.forEach(zoneId => ops.push(this.locationService.addZoneToLocation(locationId, { id: zoneId })));
    idsToRemove.forEach(zoneId => ops.push(this.locationService.removeZoneFromLocation(locationId, zoneId)));
    
    return ops;
  }
}