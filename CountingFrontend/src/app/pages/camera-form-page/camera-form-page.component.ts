import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';

import { CameraService } from '../../core/services/camera.service';
import { CameraCreatePayload } from '../../core/models/camera.model';

@Component({
  selector: 'app-camera-form-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './camera-form-page.component.html',
  styleUrl: './camera-form-page.component.css'
})
export class CameraFormPageComponent implements OnInit {
  cameraForm: FormGroup;
  isEditMode = false;
  private cameraId: number | null = null;

  constructor(
    private fb: FormBuilder,
    private cameraService: CameraService,
    private router: Router,
    private route: ActivatedRoute
  ) {
    this.cameraForm = this.fb.group({
      name: ['', Validators.required],
      rtspUrl: ['', Validators.required],
      isEnabled: [true, Validators.required]
    });
  }

  ngOnInit(): void {
    this.cameraId = Number(this.route.snapshot.paramMap.get('id'));
    if (this.cameraId) {
      this.isEditMode = true;
      this.cameraService.getCameras().subscribe(cameras => {
        const cameraToEdit = cameras.find(c => c.id === this.cameraId);
        if (cameraToEdit) {
          this.cameraForm.patchValue(cameraToEdit);
        }
      });
    }
  }

  get f() { return this.cameraForm.controls; }

  onSubmit(): void {
    if (this.cameraForm.invalid) {
      this.cameraForm.markAllAsTouched();
      return;
    }

    const payload: CameraCreatePayload = this.cameraForm.value;
    
    const saveOperation = this.isEditMode
      ? this.cameraService.updateCamera(this.cameraId!, payload)
      : this.cameraService.addCamera(payload);

    saveOperation.subscribe({
      next: () => {
        alert('Camera saved. Please restart the backend application for changes to take effect.');
        this.router.navigate(['/cameras']);
      },
      error: (err) => alert(`Failed to save camera: ${err.error?.message || 'Unknown error'}`)
    });
  }
}