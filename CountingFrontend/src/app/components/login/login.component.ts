import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="login-container">
      <form [formGroup]="loginForm" (ngSubmit)="onSubmit()" class="login-form">
        <h2>Login</h2>
        <div class="form-group">
          <label for="username">Username:</label>
          <input type="text" id="username" formControlName="username">
        </div>
        <div class="form-group">
          <label for="password">Password:</label>
          <input type="password" id="password" formControlName="password">
        </div>
        <button type="submit" [disabled]="loginForm.invalid">Login</button>
        <div *ngIf="error" class="error-message">{{ error }}</div>
      </form>
    </div>
  `,
  styles: [`
    .login-container {
      display: flex;
      justify-content: center;
      align-items: center;
      height: 100vh;
      background-color: #f0f0f0;
    }
    .login-form {
      background: white;
      padding: 2rem;
      border-radius: 8px;
      box-shadow: 0 2px 4px rgba(0,0,0,0.1);
      width: 300px;
    }
    .form-group {
      margin-bottom: 1rem;
    }
    label {
      display: block;
      margin-bottom: 0.5rem;
      font-size: 14px;
    }
    input {
      width: 100%;
      padding: 0.5rem;
      border: 1px solid #adadad;
      border-radius: 4px;
      font-size: 14px;
    }
    button {
      width: 100%;
      padding: 0.75rem;
      background-color: #e0e0e0;
      border: 1px solid #adadad;
      border-radius: 4px;
      font-size: 14px;
      cursor: pointer;
    }
    button:hover {
      background-color: #d0d0d0;
    }
    button:disabled {
      background-color: #f0f0f0;
      cursor: not-allowed;
    }
    .error-message {
      color: red;
      margin-top: 1rem;
      text-align: center;
    }
  `]
})
export class LoginComponent {
  loginForm: FormGroup;
  error = '';

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    this.loginForm = this.fb.group({
      username: ['', Validators.required],
      password: ['', Validators.required]
    });
  }

  onSubmit() {
    if (this.loginForm.valid) {
      const { username, password } = this.loginForm.value;
      this.authService.login(username, password).subscribe({
        next: () => this.router.navigate(['/dashboard']),
        error: (err) => this.error = 'Invalid username or password'
      });
    }
  }
}