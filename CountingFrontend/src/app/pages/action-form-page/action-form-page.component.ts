
import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';

import { ActionService } from '../../core/services/action.service';
import { ActionCreatePayload } from '../../core/models/action.model';

@Component({
  selector: 'app-action-form-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './action-form-page.component.html',
  styleUrl: './action-form-page.component.css'
})
export class ActionFormPageComponent implements OnInit {
  actionForm: FormGroup;
  isEditMode = false;
  private actionId: number | null = null;

  constructor(
    private fb: FormBuilder,
    private actionService: ActionService,
    private router: Router,
    private route: ActivatedRoute
  ) {
    this.actionForm = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(3)]],
      ipAddress: ['', [Validators.required, Validators.pattern('^(?:[0-9]{1,3}\\.){3}[0-9]{1,3}$')]],
      port: [null, [Validators.required, Validators.min(1), Validators.max(65535)]],
      intervalMilliseconds: [1000, [Validators.required, Validators.min(100)]],
      protocol: ['TCP', Validators.required],
      isEnabled: [true, Validators.required]
    });
  }

  ngOnInit(): void {
    this.actionId = Number(this.route.snapshot.paramMap.get('id'));
    if (this.actionId) {
      this.isEditMode = true;
      this.actionService.getActions().subscribe(actions => {
        const actionToEdit = actions.find(a => a.id === this.actionId);
        if (actionToEdit) {
          this.actionForm.patchValue(actionToEdit);
        }
      });
    }
  }

  get f() { return this.actionForm.controls; }

  onSubmit(): void {
    if (this.actionForm.invalid) {
      this.actionForm.markAllAsTouched();
      return;
    }

    const payload: ActionCreatePayload = this.actionForm.value;
    
    const saveOperation = this.isEditMode
      ? this.actionService.updateAction(this.actionId!, payload)
      : this.actionService.addAction(payload);

    saveOperation.subscribe({
      next: () => {
        alert('Action saved successfully.');
        this.router.navigate(['/actions']);
      },
      error: (err) => alert(`Failed to save action: ${err.error?.message || 'Unknown error'}`)
    });
  }
}