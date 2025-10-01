import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { LoginComponent } from './pages/login/login.component';
import { LayoutComponent } from './core/components/layout/layout.component';
import { DashboardPageComponent } from './pages/dashboard-page/dashboard-page.component';
import { ZonesListPageComponent } from './pages/zones-list-page/zones-list-page.component';
import { ZoneFormPageComponent } from './pages/zone-form-page/zone-form-page.component';
import { CamerasListPageComponent } from './pages/cameras-list-page/cameras-list-page.component';
import { CameraFormPageComponent } from './pages/camera-form-page/camera-form-page.component';
import { LogsPageComponent } from './pages/logs-page/logs-page.component';
import { SettingsPageComponent } from './pages/settings-page/settings-page.component';
import { UsersPageComponent } from './pages/users-page/users-page.component';
import { ActionsListPageComponent } from './pages/actions-list-page/actions-list-page.component';
import { ActionFormPageComponent } from './pages/action-form-page/action-form-page.component';
import { LocationsListPageComponent } from './pages/locations-list-page/locations-list-page.component';
import { LocationFormPageComponent } from './pages/location-form-page/location-form-page.component';


export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  {
    path: '',
    component: LayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: DashboardPageComponent, title: 'Dashboard' },

      { path: 'locations', component: LocationsListPageComponent, title: 'Locations' },
      { path: 'locations/new', component: LocationFormPageComponent, title: 'New Location' },
      { path: 'locations/edit/:id', component: LocationFormPageComponent, title: 'Edit Location' },

      { path: 'zones', component: ZonesListPageComponent, title: 'Zones' },
      { path: 'zones/new', component: ZoneFormPageComponent, title: 'New Zone' },
      { path: 'zones/edit/:id', component: ZoneFormPageComponent, title: 'Edit Zone' },
      
      { path: 'cameras', component: CamerasListPageComponent, title: 'Cameras' },
      { path: 'cameras/new', component: CameraFormPageComponent, title: 'New Camera' },
      { path: 'cameras/edit/:id', component: CameraFormPageComponent, title: 'Edit Camera' },

      { path: 'actions', component: ActionsListPageComponent, title: 'Actions' },
      { path: 'actions/new', component: ActionFormPageComponent, title: 'New Action' },
      { path: 'actions/edit/:id', component: ActionFormPageComponent, title: 'Edit Action' },
      
      { path: 'logs', component: LogsPageComponent, title: 'Event Logs' },
      { path: 'settings', component: SettingsPageComponent, title: 'Settings' },
      { path: 'users', component: UsersPageComponent, title: 'User Management' },
    ]
  },
  { path: '**', redirectTo: 'dashboard' } // Wildcard route for a 404 page or redirect
];