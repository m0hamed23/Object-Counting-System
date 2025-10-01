import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Routes } from '@angular/router';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

import { DashboardComponent } from './components/dashboard/dashboard.component';
import { CrowdMonitorComponent } from './components/crowd-monitor/crowd-monitor.component';
import { BarrierControlComponent } from './components/barrier-control/barrier-control.component';
import { BarrierLogsComponent } from './components/barrier-logs/barrier-logs.component';
import { UserManagementComponent } from './components/user-management/user-management.component';
import { SettingsComponent } from './components/settings/settings.component';

const routes: Routes = [
  {
    path: '',
    component: DashboardComponent,
    children: [
      { path: '', redirectTo: 'crowd-monitor', pathMatch: 'full' },
      { path: 'crowd-monitor', component: CrowdMonitorComponent },
      { path: 'barrier-control', component: BarrierControlComponent },
      { path: 'barrier-logs', component: BarrierLogsComponent },
      { path: 'user-management', component: UserManagementComponent },
      { path: 'settings', component: SettingsComponent }
    ]
  }
];

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule.forChild(routes),
    DashboardComponent,
    CrowdMonitorComponent,
    BarrierControlComponent,
    BarrierLogsComponent,
    UserManagementComponent,
    SettingsComponent
  ]
})
export class DashboardModule { }