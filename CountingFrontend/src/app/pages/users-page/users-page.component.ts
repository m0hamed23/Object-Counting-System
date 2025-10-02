import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { UserService } from '../../core/services/user.service';
import { User, UserCreate } from '../../core/models/user.model';

@Component({
  selector: 'app-users-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './users-page.component.html',
  styleUrl: './users-page.component.css'
})
export class UsersPageComponent implements OnInit {
  @ViewChild('userDialog') userDialog!: ElementRef<HTMLDialogElement>;

  users: User[] = [];
  userForm: FormGroup;
  editingUser: User | null = null;
  passwordPlaceholder = '';

  constructor(
    private fb: FormBuilder,
    private userService: UserService
  ) {
    this.userForm = this.fb.group({
      username: ['', Validators.required],
      password: ['']
    });
  }

  ngOnInit(): void {
    this.loadUsers();
  }

  loadUsers(): void {
    this.userService.getUsers().subscribe(users => this.users = users);
  }

  get f() { return this.userForm.controls; }

  showAddUserDialog(): void {
    this.editingUser = null;
    this.userForm.reset();
    this.passwordPlaceholder = 'Password (min 3 chars)';
    this.f['password'].setValidators([Validators.required, Validators.minLength(3)]);
    this.f['password'].updateValueAndValidity();
    this.userDialog.nativeElement.showModal();
  }

  editUser(user: User): void {
    this.editingUser = user;
    this.userForm.patchValue({ username: user.username, password: '' });
    this.passwordPlaceholder = 'Leave blank to keep current password';
    this.f['password'].setValidators([Validators.minLength(3)]);
    this.f['password'].updateValueAndValidity();
    this.userDialog.nativeElement.showModal();
  }

  saveUser(): void {
    if (this.userForm.invalid) {
      this.userForm.markAllAsTouched();
      return;
    }

    const userData: UserCreate = {
      username: this.f['username'].value,
      password: this.f['password'].value || undefined
    };

    const saveOp = this.editingUser
      ? this.userService.updateUser(this.editingUser.id, userData)
      : this.userService.createUser(userData);

    saveOp.subscribe({
      next: () => {
        this.loadUsers();
        this.closeDialog();
      },
      error: (err) => alert(`Failed to save user: ${err.error?.message || 'Unknown error'}`)
    });
  }

  deleteUser(id: number): void {
    if (confirm('Are you sure you want to delete this user?')) {
      this.userService.deleteUser(id).subscribe({
        next: () => this.loadUsers(),
        error: (err) => alert(`Failed to delete user: ${err.error?.message || 'Unknown error'}`)
      });
    }
  }

  closeDialog(): void {
    this.userDialog.nativeElement.close();
  }
}