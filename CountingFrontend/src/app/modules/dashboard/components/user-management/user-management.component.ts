import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { UserService, User, UserCreate } from '../../services/user.service';

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="user-management-container">
      <h2>User Management</h2>
      
      <div class="table-container">
        <table>
          <thead>
            <tr>
              <th>ID</th>
              <th>Username</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let user of users">
              <td>{{user.id}}</td>
              <td>{{user.username}}</td>
              <td class="actions">
                <button (click)="editUser(user)">Edit</button>
                <button (click)="deleteUser(user.id)">Delete</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="button-row">
        <button (click)="showAddUserDialog()">Add User</button>
      </div>

      <!-- Add/Edit User Dialog -->
      <dialog #userDialog class="dialog">
        <h3>{{editingUser ? 'Edit' : 'Add'}} User</h3>
        <form [formGroup]="userForm" (ngSubmit)="saveUser()">
          <div class="form-group">
            <label for="username">Username:</label>
            <input id="username" type="text" formControlName="username">
          </div>
          <div class="form-group">
            <label for="password">Password:</label>
            <input id="password" type="password" formControlName="password">
          </div>
          <div class="dialog-buttons">
            <button type="submit" [disabled]="userForm.invalid">Save</button>
            <button type="button" (click)="closeDialog()">Cancel</button>
          </div>
        </form>
      </dialog>
    </div>
  `,
  styles: [`
    .user-management-container {
      padding: 1rem;
    }
    .table-container {
      margin: 1rem 0;
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
    .actions {
      white-space: nowrap;
    }
    .actions button {
      margin-right: 0.5rem;
    }
    .button-row {
      margin-top: 1rem;
    }
    .dialog {
      padding: 1.5rem;
      border: none;
      border-radius: 8px;
      box-shadow: 0 2px 8px rgba(0,0,0,0.1);
    }
    .dialog::backdrop {
      background: rgba(0,0,0,0.5);
    }
    .form-group {
      margin-bottom: 1rem;
    }
    .form-group label {
      display: block;
      margin-bottom: 0.5rem;
    }
    .form-group input {
      width: 100%;
      padding: 0.5rem;
      border: 1px solid #ddd;
      border-radius: 4px;
    }
    .dialog-buttons {
      margin-top: 1.5rem;
      text-align: right;
    }
    .dialog-buttons button {
      margin-left: 1rem;
    }
    button {
      padding: 0.5rem 1rem;
      background-color: #e0e0e0;
      border: 1px solid #adadad;
      border-radius: 4px;
      cursor: pointer;
    }
    button:hover {
      background-color: #d0d0d0;
    }
    button:disabled {
      opacity: 0.7;
      cursor: not-allowed;
    }
  `]
})
export class UserManagementComponent implements OnInit {
  @ViewChild('userDialog') userDialog!: ElementRef<HTMLDialogElement>;

  users: User[] = [];
  userForm: FormGroup;
  editingUser: User | null = null;

  constructor(
    private fb: FormBuilder,
    private userService: UserService
  ) {
    this.userForm = this.fb.group({
      username: ['', Validators.required],
      password: ['', Validators.required] // Password is required for add, and for edit if changing
    });
  }

  ngOnInit() {
    this.loadUsers();
  }

  loadUsers() {
    this.userService.getUsers().subscribe(users => {
      this.users = users;
    });
  }

  showAddUserDialog() {
    this.editingUser = null;
    this.userForm.reset();
    this.userForm.get('password')?.setValidators([Validators.required]);
    this.userForm.get('password')?.updateValueAndValidity();
    this.userDialog.nativeElement.showModal();
  }

  editUser(user: User) {
    this.editingUser = user;
    this.userForm.patchValue({
      username: user.username,
      password: '' // Clear password field for editing, make it optional unless changed
    });
    this.userForm.get('password')?.clearValidators();
    this.userForm.get('password')?.updateValueAndValidity();
    this.userDialog.nativeElement.showModal();
  }

  saveUser() {
    if (this.userForm.invalid) {
      return;
    }

    const userData: UserCreate = {
      username: this.userForm.value.username,
      password: this.userForm.value.password
    };

    if (this.editingUser) {
      this.userService.updateUser(this.editingUser.id, userData).subscribe(() => {
        this.loadUsers();
        this.closeDialog();
      });
    } else {
      this.userService.createUser(userData).subscribe(() => {
        this.loadUsers();
        this.closeDialog();
      });
    }
  }

  deleteUser(id: number) {
    if (confirm('Are you sure you want to delete this user?')) {
      this.userService.deleteUser(id).subscribe(() => {
        this.loadUsers();
      });
    }
  }

  closeDialog() {
    this.userDialog.nativeElement.close();
    this.editingUser = null;
    this.userForm.reset();
  }
}