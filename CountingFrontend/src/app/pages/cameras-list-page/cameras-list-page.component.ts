import { Component, OnInit } from '@angular/core';
import { Observable } from 'rxjs';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

import { CameraService } from '../../core/services/camera.service';
import { Camera } from '../../core/models/camera.model';

@Component({
  selector: 'app-cameras-list-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './cameras-list-page.component.html',
  styleUrl: './cameras-list-page.component.css'
})
export class CamerasListPageComponent implements OnInit {
  cameras$!: Observable<Camera[]>;

  constructor(private cameraService: CameraService) {}

  ngOnInit(): void {
    this.loadCameras();
  }

  loadCameras(): void {
    this.cameras$ = this.cameraService.getCameras();
  }

  onDelete(id: number): void {
    if (confirm('Are you sure you want to delete this camera? This will require an application restart to take full effect.')) {
      this.cameraService.deleteCamera(id).subscribe({
        next: () => this.loadCameras(),
        error: (err) => alert(`Failed to delete camera: ${err.error?.message || 'Unknown error'}`)
      });
    }
  }
}