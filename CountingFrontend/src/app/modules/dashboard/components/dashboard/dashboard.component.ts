import { Component } from '@angular/core';
import { Router, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../../services/auth.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="dashboard-container">
      <nav class="sidebar">
        <div class="user-info">
          <span>Welcome, {{ username }}</span>
        </div>
        <ul class="nav-items">
          <li>
            <a routerLink="crowd-monitor" routerLinkActive="active">
              Crowd Monitor
            </a>
          </li>
          <li>
            <a routerLink="barrier-control" routerLinkActive="active">
              Barrier Control
            </a>
          </li>
          <li>
            <a routerLink="barrier-logs" routerLinkActive="active">
              Barrier Logs
            </a>
          </li>
          <li>
            <a routerLink="user-management" routerLinkActive="active">
              User Management
            </a>
          </li>
          <li>
            <a routerLink="settings" routerLinkActive="active">
              Settings
            </a>
          </li>
        </ul>
        <button class="logout-btn" (click)="logout()">Logout</button>
      </nav>
      <main class="content">
        <router-outlet></router-outlet>
      </main>
    </div>
  `,
  styles: [`
    .dashboard-container {
      display: flex;
      height: 100vh;
    }
    .sidebar {
      width: 250px;
      background-color: #f0f0f0;
      border-right: 1px solid #ddd;
      padding: 1rem;
      display: flex;
      flex-direction: column;
    }
    .user-info {
      padding: 1rem;
      border-bottom: 1px solid #ddd;
      margin-bottom: 1rem;
    }
    .nav-items {
      list-style: none;
      padding: 0;
      margin: 0;
      flex-grow: 1;
    }
    .nav-items li a {
      display: block;
      padding: 0.75rem 1rem;
      color: #333;
      text-decoration: none;
      border-radius: 4px;
      margin-bottom: 0.25rem;
    }
    .nav-items li a:hover {
      background-color: #e0e0e0;
    }
    .nav-items li a.active {
      background-color: #d0d0d0;
      font-weight: bold;
    }
    .logout-btn {
      padding: 0.75rem;
      background-color: #e0e0e0;
      border: 1px solid #adadad;
      border-radius: 4px;
      cursor: pointer;
      margin-top: auto;
    }
    .logout-btn:hover {
      background-color: #d0d0d0;
    }
    .content {
      flex-grow: 1;
      padding: 1rem;
      overflow-y: auto;
    }
  `]
})
export class DashboardComponent {
  username: string = '';

  constructor(
    private authService: AuthService,
    private router: Router
  ) {
    const user = this.authService.currentUser;
    if (user) {
      this.username = user.username;
    }
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}