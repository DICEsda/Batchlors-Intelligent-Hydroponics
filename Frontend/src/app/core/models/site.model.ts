/**
 * Site Models for Smart Tile IoT System
 * Sites are physical locations containing coordinators and nodes
 */

// ============================================================================
// Site
// ============================================================================

export interface Site {
  _id: string;
  name: string;
  location: string;
  config: string;
  created_at: Date;
  updated_at: Date;
}

// ============================================================================
// Site Summary (for list views)
// ============================================================================

export interface SiteSummary {
  _id: string;
  name: string;
  location: string;
  coordinatorCount?: number;
  nodeCount?: number;
  created_at: Date;
}

// ============================================================================
// Site Configuration
// ============================================================================

export interface SiteConfig {
  siteId: string;
  name?: string;
  location?: string;
  config?: string;
}
