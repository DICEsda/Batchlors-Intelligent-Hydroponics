/**
 * ML Prediction Models for Hydroponic Farm System
 * Height predictions, growth analysis, and anomaly detection
 */

// ============================================================================
// Height Prediction
// ============================================================================

export interface HeightPrediction {
  _id: string;
  predictionId: string;
  
  // Target
  towerId: string;
  towerName: string;
  slotIndex: number;
  plantType: string;
  
  // Prediction data
  currentHeightCm: number;
  predictedHeightCm: number;
  predictionHorizonDays: number;
  confidence: number;       // 0-1
  
  // Growth trajectory
  trajectory: GrowthDataPoint[];
  
  // Model info
  modelVersion: string;
  
  // Timestamps
  predictedAt: Date;
  validUntil: Date;
}

export interface GrowthDataPoint {
  dayOffset: number;        // Days from prediction date
  heightCm: number;
  isActual: boolean;        // true = historical, false = predicted
  confidence?: number;
}

// ============================================================================
// Growth Analysis
// ============================================================================

export interface GrowthAnalysis {
  towerId: string;
  slotIndex: number;
  plantType: string;
  
  // Current state
  currentHeightCm: number;
  daysSincePlanting: number;
  
  // Growth metrics
  averageGrowthRate: number;    // cm/day
  expectedGrowthRate: number;   // Based on plant type
  growthDeviation: number;      // % from expected
  
  // Health assessment
  healthScore: number;          // 0-100
  healthFactors: HealthFactor[];
  
  // Recommendations
  recommendations: GrowthRecommendation[];
}

export interface HealthFactor {
  factor: string;
  status: 'optimal' | 'suboptimal' | 'critical';
  value: number;
  optimalRange: { min: number; max: number };
  impact: number;               // Impact on health score
}

export interface GrowthRecommendation {
  priority: 'low' | 'medium' | 'high';
  category: 'nutrients' | 'lighting' | 'temperature' | 'humidity' | 'ph';
  message: string;
  action?: string;
}

// ============================================================================
// Batch Predictions
// ============================================================================

export interface TowerPredictions {
  towerId: string;
  towerName: string;
  predictions: SlotPrediction[];
  averageHealthScore: number;
  timestamp: Date;
}

export interface SlotPrediction {
  slotIndex: number;
  plantType: string;
  currentHeightCm: number;
  predicted7DayHeightCm: number;
  predicted14DayHeightCm: number;
  healthScore: number;
  confidence: number;
}

export interface FarmPredictionSummary {
  timestamp: Date;
  
  // Overall metrics
  totalPlants: number;
  averageHealthScore: number;
  plantsNeedingAttention: number;
  
  // Growth summary
  averageGrowthRate: number;
  expectedHarvestCount7Days: number;
  expectedHarvestCount14Days: number;
  
  // Alerts
  growthAnomalies: GrowthAnomaly[];
}

// ============================================================================
// Anomaly Detection
// ============================================================================

export type AnomalyType = 
  | 'growth_stall'
  | 'growth_spike'
  | 'sensor_drift'
  | 'environmental_outlier'
  | 'pattern_deviation';

export interface GrowthAnomaly {
  _id: string;
  anomalyId: string;
  
  // Location
  towerId: string;
  towerName: string;
  slotIndex?: number;
  
  // Anomaly details
  type: AnomalyType;
  severity: 'low' | 'medium' | 'high';
  description: string;
  
  // Data
  expectedValue: number;
  actualValue: number;
  deviationPercent: number;
  
  // Status
  status: 'new' | 'acknowledged' | 'resolved' | 'ignored';
  acknowledgedBy?: string;
  resolvedAt?: Date;
  
  // Timestamps
  detectedAt: Date;
  createdAt: Date;
}

// ============================================================================
// Prediction Requests
// ============================================================================

export interface PredictionRequest {
  towerId: string;
  slotIndex?: number;       // If omitted, predict all slots
  horizonDays: number;      // 7, 14, 30
}

export interface BatchPredictionRequest {
  towerIds?: string[];      // If omitted, predict all towers
  horizonDays: number;
}

// ============================================================================
// Model Information
// ============================================================================

export interface MLModelInfo {
  modelId: string;
  modelType: 'height_prediction' | 'anomaly_detection' | 'health_scoring';
  version: string;
  accuracy: number;
  trainedAt: Date;
  sampleCount: number;
  features: string[];
  isActive: boolean;
}
