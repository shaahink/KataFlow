import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./pages/dashboard/dashboard.component').then(m => m.DashboardComponent) },
  { path: 'workflows', loadComponent: () => import('./pages/workflows/workflow-list.component').then(m => m.WorkflowListComponent) },
  { path: 'workflows/:name', loadComponent: () => import('./pages/workflows/workflow-editor.component').then(m => m.WorkflowEditorComponent) },
  { path: 'templates', loadComponent: () => import('./pages/templates/template-list.component').then(m => m.TemplateListComponent) },
  { path: 'templates/:path', loadComponent: () => import('./pages/templates/template-editor.component').then(m => m.TemplateEditorComponent) },
  { path: 'sessions/:id', loadComponent: () => import('./pages/sessions/session-detail.component').then(m => m.SessionDetailComponent) },
  { path: 'settings', loadComponent: () => import('./pages/settings/settings.component').then(m => m.SettingsComponent) },
  { path: '**', redirectTo: '' }
];
