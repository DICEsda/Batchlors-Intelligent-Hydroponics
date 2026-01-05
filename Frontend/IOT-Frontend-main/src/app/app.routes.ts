import { Routes } from '@angular/router';

/**
 * Application Routes - Smart Tile IoT Dashboard
 * Using lazy loading for optimal bundle splitting
 */
export const routes: Routes = [
  // Default redirect to farm overview
  {
    path: '',
    redirectTo: '/overview',
    pathMatch: 'full'
  },

  // Overview - Main dashboard with all coordinators and nodes
  {
    path: 'overview',
    loadComponent: () =>
      import('./pages/farm-overview/farm-overview.component').then(
        m => m.FarmOverviewComponent
      ),
    title: 'Overview | Smart Tile Dashboard'
  },

  // Coordinators List - All coordinator controllers
  {
    path: 'reservoirs',
    loadComponent: () =>
      import('./pages/reservoirs-list/reservoirs-list.component').then(
        m => m.ReservoirsListComponent
      ),
    title: 'Coordinators | Smart Tile Dashboard'
  },

  // Coordinator Detail - Coordinator metrics and connected nodes
  {
    path: 'reservoirs/:id',
    loadComponent: () =>
      import('./pages/coordinator-detail/coordinator-detail.component').then(
        m => m.CoordinatorDetailComponent
      ),
    title: 'Coordinator Detail | Smart Tile Dashboard'
  },

  // Legacy route redirect for coordinators
  {
    path: 'coordinators',
    redirectTo: '/reservoirs',
    pathMatch: 'full'
  },
  {
    path: 'coordinators/:id',
    redirectTo: '/reservoirs/:id',
    pathMatch: 'prefix'
  },

  // Nodes List - All node units
  {
    path: 'towers',
    loadComponent: () =>
      import('./pages/towers-list/towers-list.component').then(
        m => m.TowersListComponent
      ),
    title: 'Nodes | Smart Tile Dashboard'
  },

  // Node Detail - Node metrics and LED control
  {
    path: 'towers/:id',
    loadComponent: () =>
      import('./pages/node-detail/node-detail.component').then(
        m => m.NodeDetailComponent
      ),
    title: 'Node Detail | Smart Tile Dashboard'
  },

  // Legacy route redirect for nodes
  {
    path: 'nodes',
    redirectTo: '/towers',
    pathMatch: 'full'
  },
  {
    path: 'nodes/:id',
    loadComponent: () =>
      import('./pages/node-detail/node-detail.component').then(
        m => m.NodeDetailComponent
      ),
    title: 'Node Detail | Smart Tile Dashboard'
  },

  // Zones - Lighting zone management
  {
    path: 'zones',
    loadComponent: () =>
      import('./pages/zones-list/zones-list.component').then(
        m => m.ZonesListComponent
      ),
    title: 'Zones | Smart Tile Dashboard'
  },

  // Radar View - Presence detection visualization
  {
    path: 'radar',
    loadComponent: () =>
      import('./pages/radar-view/radar-view.component').then(
        m => m.RadarViewComponent
      ),
    title: 'Radar View | Smart Tile Dashboard'
  },

  // ML Predictions - Growth predictions and analysis
  {
    path: 'predictions',
    loadComponent: () =>
      import('./pages/predictions/predictions.component').then(
        m => m.PredictionsComponent
      ),
    title: 'ML Predictions | Smart Tile Dashboard'
  },

  // Machine Learning - ML models and training
  {
    path: 'machine-learning',
    loadComponent: () =>
      import('./pages/machine-learning/machine-learning.component').then(
        m => m.MachineLearningComponent
      ),
    title: 'Machine Learning | Smart Tile Dashboard'
  },

  // Incubators - Incubator management
  {
    path: 'incubators',
    loadComponent: () =>
      import('./pages/incubators/incubators.component').then(
        m => m.IncubatorsComponent
      ),
    title: 'Incubators | Smart Tile Dashboard'
  },

  // Greenhouses - Greenhouse management
  {
    path: 'greenhouses',
    loadComponent: () =>
      import('./pages/greenhouses/greenhouses.component').then(
        m => m.GreenhousesComponent
      ),
    title: 'Greenhouses | Smart Tile Dashboard'
  },

  // Digital Twin - 3D topology and live state (Simulations Lab)
  {
    path: 'digital-twin',
    loadComponent: () =>
      import('./pages/digital-twin/digital-twin.component').then(
        m => m.DigitalTwinComponent
      ),
    title: 'Simulations Lab | Smart Tile Dashboard'
  },

  // OTA Dashboard - Firmware management
  {
    path: 'ota',
    loadComponent: () =>
      import('./pages/ota-dashboard/ota-dashboard.component').then(
        m => m.OtaDashboardComponent
      ),
    title: 'OTA Updates | Smart Tile Dashboard'
  },

  // Settings - System configuration
  {
    path: 'settings',
    loadComponent: () =>
      import('./pages/settings/settings.component').then(
        m => m.SettingsComponent
      ),
    title: 'Settings | Smart Tile Dashboard'
  },

  // Alerts - Alert history and management
  {
    path: 'alerts',
    loadComponent: () =>
      import('./pages/alerts/alerts.component').then(
        m => m.AlertsComponent
      ),
    title: 'Alerts | Smart Tile Dashboard'
  },

  // Wildcard redirect
  {
    path: '**',
    redirectTo: '/overview'
  }
];
