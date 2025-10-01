import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators, FormArray } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { forkJoin, of, Observable } from 'rxjs';
import { switchMap } from 'rxjs/operators';

import { ZoneService } from '../../core/services/zone.service';
import { CameraService } from '../../core/services/camera.service';
import { Camera } from '../../core/models/camera.model';
import { Zone } from '../../core/models/zone.model';

@Component({
  selector: 'app-zone-form-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './zone-form-page.component.html',
  styleUrl: './zone-form-page.component.css'
})
export class ZoneFormPageComponent implements OnInit {
  zoneForm: FormGroup;
  isEditMode = false;
  private zoneId: number | null = null;
  
  allCameras: Camera[] = [];

  // Store the initial state to detect changes
  private originalZoneName: string = '';
  private originalCameraIds = new Set<number>();

  constructor(
    private fb: FormBuilder,
    private zoneService: ZoneService,
    private cameraService: CameraService,
    private router: Router,
    private route: ActivatedRoute
  ) {
    this.zoneForm = this.fb.group({
      name: ['', Validators.required],
      cameras: this.fb.array([])
    });
  }

  ngOnInit(): void {
    this.loadInitialData();
  }

  get camerasArray(): FormArray {
    return this.zoneForm.get('cameras') as FormArray;
  }

  private loadInitialData(): void {
    this.cameraService.getCameras().subscribe(cameras => {
      this.allCameras = cameras;

      // Initialize form array with a control for each available camera
      this.camerasArray.clear();
      this.allCameras.forEach(() => this.camerasArray.push(this.fb.control(false)));

      const idParam = this.route.snapshot.paramMap.get('id');
      this.zoneId = idParam ? Number(idParam) : null;

      if (this.zoneId) {
        this.isEditMode = true;
        this.loadZoneForEditing();
      }
    });
  }

  private loadZoneForEditing(): void {
    this.zoneService.getZones().subscribe(zones => {
      const zoneToEdit = zones.find(z => z.id === this.zoneId);
      if (zoneToEdit) {
        // Store original state for comparison on save
        this.originalZoneName = zoneToEdit.name;
        this.originalCameraIds = new Set(zoneToEdit.cameras.map(c => c.id));

        // Patch the form with current values
        this.zoneForm.patchValue({ name: zoneToEdit.name });
        
        this.allCameras.forEach((cam, i) => {
          this.camerasArray.at(i).setValue(this.originalCameraIds.has(cam.id));
        });
      }
    });
  }

  onSubmit(): void {
    if (this.zoneForm.invalid) {
      this.zoneForm.markAllAsTouched();
      return;
    }

    if (this.isEditMode) {
      this.handleUpdate();
    } else {
      this.handleCreate();
    }
  }

  private handleCreate(): void {
    const name = this.zoneForm.value.name;
    this.zoneService.createZone({ name }).pipe(
      switchMap(newZone => {
        const associationOps = this.getAssociationObservables(newZone.id);
        return associationOps.length > 0 ? forkJoin(associationOps) : of(null);
      })
    ).subscribe({
      next: () => this.router.navigate(['/zones']),
      error: (err) => alert(`Failed to create zone: ${err.error?.message}`)
    });
  }

  private handleUpdate(): void {
    const formValue = this.zoneForm.value;
    const updateObservables: Observable<any>[] = [];

    // 1. Check if the name has changed
    if (formValue.name !== this.originalZoneName) {
      updateObservables.push(this.zoneService.updateZone(this.zoneId!, { name: formValue.name }));
    }

    // 2. Check if camera associations have changed
    const currentSelectedIds = new Set<number>( // Explicitly type the Set
      this.zoneForm.value.cameras
        .map((checked: boolean, i: number) => checked ? this.allCameras[i].id : null)
        .filter((id: number | null): id is number => id !== null)
    );

    if (this.haveSetsChanged(this.originalCameraIds, currentSelectedIds)) {
      const idsToRemove = [...this.originalCameraIds].filter(id => !currentSelectedIds.has(id));
      const idsToAdd = [...currentSelectedIds].filter(id => !this.originalCameraIds.has(id));

      idsToRemove.forEach(camId => {
        updateObservables.push(this.zoneService.removeCameraFromZone(this.zoneId!, camId));
      });
      idsToAdd.forEach(camId => {
        updateObservables.push(this.zoneService.addCameraToZone(this.zoneId!, { id: camId }));
      });
    }

    // 3. Execute updates if any are needed
    if (updateObservables.length > 0) {
      forkJoin(updateObservables).subscribe({
        next: () => this.router.navigate(['/zones']),
        error: (err) => alert(`Failed to update zone: ${err.error?.message}`)
      });
    } else {
      // No changes were made, just navigate back
      this.router.navigate(['/zones']);
    }
  }

  // This function is only used for the CREATE path now
  private getAssociationObservables(zoneId: number): Observable<any>[] {
    const selectedCameraIds = this.zoneForm.value.cameras
      .map((checked: boolean, i: number) => checked ? this.allCameras[i].id : null)
      .filter((id: number | null): id is number => id !== null);
      
    return selectedCameraIds.map((id: number) => this.zoneService.addCameraToZone(zoneId, { id }));
  }

  // Helper to compare two sets of numbers
  private haveSetsChanged(setA: Set<number>, setB: Set<number>): boolean {
    if (setA.size !== setB.size) {
      return true;
    }
    for (const item of setA) {
      if (!setB.has(item)) {
        return true;
      }
    }
    return false;
  }
}