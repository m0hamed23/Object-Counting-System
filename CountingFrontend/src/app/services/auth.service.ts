import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface User {
  id: number;
  username: string;
}

// Interface for the login response from the backend
export interface LoginResponse {
  message: string;
  token: string;
  user: User;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private currentUserSubject = new BehaviorSubject<User | null>(null);
  public currentUser$ = this.currentUserSubject.asObservable();
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {
    // Initialize current user from localStorage if token exists
    const token = localStorage.getItem('jwt_token');
    const userJson = localStorage.getItem('currentUserDetails'); // New key for user details
    if (token && userJson) {
      try {
        this.currentUserSubject.next(JSON.parse(userJson));
      } catch (e) {
        console.error("Error parsing stored user details", e);
        localStorage.removeItem('jwt_token');
        localStorage.removeItem('currentUserDetails');
      }
    }
  }

  login(username: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/auth/login`, { username, password }).pipe(
      tap(response => {
        if (response && response.token && response.user) {
          localStorage.setItem('jwt_token', response.token); // Store only the token
          localStorage.setItem('currentUserDetails', JSON.stringify(response.user)); // Store user details separately
          this.currentUserSubject.next(response.user);
        } else {
          // Handle cases where token or user might be missing in response
          console.error('Login response missing token or user data.');
        }
      })
    );
  }

  logout(): void {
    localStorage.removeItem('jwt_token');
    localStorage.removeItem('currentUserDetails');
    this.currentUserSubject.next(null);
  }

  get currentUser(): User | null {
    return this.currentUserSubject.value;
  }

  isAuthenticated(): boolean {
    // Check for token existence and potentially its validity (e.g., not expired if you store expiration)
    return !!localStorage.getItem('jwt_token'); 
  }

  // Helper to get the current token, used by interceptor
  getToken(): string | null {
    return localStorage.getItem('jwt_token');
  }
}