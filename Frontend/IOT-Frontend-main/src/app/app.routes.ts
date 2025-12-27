import { Routes } from '@angular/router';

/**
 * Application Routes - Hydroponic Farm Dashboard
 * Using lazy loading for optimal bundle splitting
 */
export const routes: Routes = [
  // Default redirect to farm overview
  {
    path: '',
    redirectTo: '/overview',
    pathMatch: 'full'
  },

  // Farm Overview - Main dashboard with all coordinators and towers
  {
    path: 'overview',
    loadComponent: () =>
      import('./pages/farm-overview/farm-overview.component').then(
        m => m.FarmOverviewComponent
      ),
    title: 'Farm Overview | Hydroponic Dashboard'
  },

  // Coordinator Detail - Reservoir metrics and connected towers
  {
    path: 'coordinators/:id',
    loadComponent: () =>
      import('./pages/coordinator-detail/coordinator-detail.component').then(
        m => m.CoordinatorDetailComponent
      ),
    title: 'Coordinator Detail | Hydroponic Dashboard'
  },

  // Tower Detail - Plant metrics and 3D visualization
  {
    path: 'towers/:id',
    loadComponent: () =>
      import('./pages/tower-detail/tower-detail.component').then(
        m => m.TowerDetailComponent
      ),
    title: 'Tower Detail | Hydroponic Dashboard'
  },

  // ML Predictions - Growth predictions and analysis
  {
    path: 'predictions',
    loadComponent: () =>
      import('./pages/predictions/predictions.component').then(
        m => m.PredictionsComponent
      ),
    title: 'ML Predictions | Hydroponic Dashboard'
  },

  // Digital Twin - 3D farm topology and live state
  {
    path: 'digital-twin',
    loadComponent: () =>
      import('./pages/digital-twin/digital-twin.component').then(
        m => m.DigitalTwinComponent
      ),
    title: 'Digital Twin | Hydroponic Dashboard'
  },

  // OTA Dashboard - Firmware management
  {
    path: 'ota',
    loadComponent: () =>
      import('./pages/ota-dashboard/ota-dashboard.component').then(
        m => m.OtaDashboardComponent
      ),
    title: 'OTA Updates | Hydroponic Dashboard'
  },

  // Settings - System configuration
  {
    path: 'settings',
    loadComponent: () =>
      import('./pages/settings/settings.component').then(
        m => m.SettingsComponent
      ),
    title: 'Settings | Hydroponic Dashboard'
  },

  // Alerts - Alert history and management
  {
    path: 'alerts',
    loadComponent: () =>
      import('./pages/alerts/alerts.component').then(
        m => m.AlertsComponent
      ),
    title: 'Alerts | Hydroponic Dashboard'
  },

  // Wildcard redirect
  {
    path: '**',
    redirectTo: '/overview'
  }
];
