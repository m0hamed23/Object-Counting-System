
import { Component, OnInit } from '@angular/core';
import { Observable } from 'rxjs';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

import { ActionService } from '../../core/services/action.service';
import { Action } from '../../core/models/action.model';

@Component({
  selector: 'app-actions-list-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './actions-list-page.component.html',
  styleUrl: './actions-list-page.component.css'
})
export class ActionsListPageComponent implements OnInit {
  actions$!: Observable<Action[]>;

  constructor(private actionService: ActionService) {}

  ngOnInit(): void {
    this.loadActions();
  }

  loadActions(): void {
    this.actions$ = this.actionService.getActions();
  }

  onDelete(id: number): void {
    if (confirm('Are you sure you want to delete this action?')) {
      this.actionService.deleteAction(id).subscribe({
        next: () => this.loadActions(),
        error: (err) => alert(`Failed to delete action: ${err.error?.message || 'Unknown error'}`)
      });
    }
  }
}