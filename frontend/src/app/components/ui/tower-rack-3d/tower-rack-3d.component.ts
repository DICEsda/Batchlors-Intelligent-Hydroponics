import {
  Component,
  Input,
  Output,
  EventEmitter,
  ViewChild,
  ElementRef,
  AfterViewInit,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  NgZone,
  inject,
} from '@angular/core';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import {
  CSS2DRenderer,
  CSS2DObject,
} from 'three/examples/jsm/renderers/CSS2DRenderer.js';

/** Minimal shape we read from each tower input */
export interface TowerInput {
  towerId?: string;
  tower_id?: string;
  id?: string;
  status?: string;
  reported?: {
    airTempC?: number | null;
    humidityPct?: number | null;
    vbatMv?: number | null;
    lightLux?: number | null;
  };
  metadata?: {
    isConnected?: boolean;
    syncStatus?: string;
  };
}

export interface AlertInput {
  severity?: string;
  status?: string;
  source?: {
    type?: string;
    id?: string;
  };
}

// Colors
const COLOR_GREEN = 0x22c55e;
const COLOR_AMBER = 0xf59e0b;
const COLOR_RED = 0xef4444;
const COLOR_GRAY = 0x6b7280;
const GROUND_COLOR = 0x1a1a2e;

@Component({
  selector: 'app-tower-rack-3d',
  standalone: true,
  templateUrl: './tower-rack-3d.component.html',
  styleUrl: './tower-rack-3d.component.scss',
})
export class TowerRack3dComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input() towers: TowerInput[] = [];
  @Input() alerts: AlertInput[] = [];
  @Output() towerSelected = new EventEmitter<string>();

  @ViewChild('canvas', { static: true }) canvasRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('containerRef', { static: true }) containerRef!: ElementRef<HTMLDivElement>;
  @ViewChild('labelContainer', { static: true }) labelContainerRef!: ElementRef<HTMLDivElement>;

  private readonly zone = inject(NgZone);

  // Three.js objects
  private renderer!: THREE.WebGLRenderer;
  private labelRenderer!: CSS2DRenderer;
  private scene!: THREE.Scene;
  private camera!: THREE.PerspectiveCamera;
  private controls!: OrbitControls;
  private raycaster = new THREE.Raycaster();
  private pointer = new THREE.Vector2();

  // Tower mesh tracking
  private towerMeshes: THREE.Mesh[] = [];
  private towerLabels: CSS2DObject[] = [];
  private towerGroup = new THREE.Group();
  private groundMesh!: THREE.Mesh;
  private selectedMesh: THREE.Mesh | null = null;

  // Animation
  private animationFrameId = 0;
  private resizeObserver: ResizeObserver | null = null;
  private initialized = false;

  // Bound handler reference for proper cleanup
  private readonly onClickBound = (e: MouseEvent) => this.onClick(e);

  // ============================================================================
  // Lifecycle
  // ============================================================================

  ngAfterViewInit(): void {
    this.zone.runOutsideAngular(() => {
      this.initScene();
      this.buildTowers();
      this.startAnimationLoop();
      this.setupResizeObserver();
    });
    this.initialized = true;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!this.initialized) return;
    if (changes['towers'] || changes['alerts']) {
      this.zone.runOutsideAngular(() => {
        this.rebuildTowers();
      });
    }
  }

  ngOnDestroy(): void {
    // Cancel animation frame
    if (this.animationFrameId) {
      cancelAnimationFrame(this.animationFrameId);
    }

    // Remove resize observer
    this.resizeObserver?.disconnect();

    // Remove event listeners
    this.renderer?.domElement?.removeEventListener('click', this.onClickBound);

    // Dispose labels
    this.disposeTowerLabels();

    // Dispose tower meshes
    this.disposeTowerMeshes();

    // Dispose ground
    if (this.groundMesh) {
      this.groundMesh.geometry.dispose();
      (this.groundMesh.material as THREE.Material).dispose();
    }

    // Dispose renderers
    this.labelRenderer?.domElement?.remove();
    this.renderer?.dispose();

    // Dispose controls
    this.controls?.dispose();
  }

  // ============================================================================
  // Scene initialization
  // ============================================================================

  private initScene(): void {
    const canvas = this.canvasRef.nativeElement;
    const container = this.containerRef.nativeElement;
    const rect = container.getBoundingClientRect();
    const w = rect.width || 800;
    const h = rect.height || 500;

    // ---- Scene ----
    this.scene = new THREE.Scene();

    // ---- Camera ----
    this.camera = new THREE.PerspectiveCamera(50, w / h, 0.1, 1000);
    this.camera.position.set(8, 6, 8);
    this.camera.lookAt(0, 0, 0);

    // ---- WebGL Renderer ----
    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
    this.renderer.setSize(w, h);
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.setClearColor(0x000000, 0);

    // ---- CSS2D Renderer (labels) ----
    this.labelRenderer = new CSS2DRenderer({ element: this.labelContainerRef.nativeElement });
    this.labelRenderer.setSize(w, h);
    this.labelRenderer.domElement.style.position = 'absolute';
    this.labelRenderer.domElement.style.top = '0';
    this.labelRenderer.domElement.style.left = '0';
    this.labelRenderer.domElement.style.pointerEvents = 'none';

    // ---- Lights ----
    const ambient = new THREE.AmbientLight(0xffffff, 0.6);
    this.scene.add(ambient);

    const directional = new THREE.DirectionalLight(0xffffff, 0.8);
    directional.position.set(5, 10, 5);
    this.scene.add(directional);

    // ---- Ground plane ----
    const groundGeo = new THREE.PlaneGeometry(20, 20);
    const groundMat = new THREE.MeshStandardMaterial({
      color: GROUND_COLOR,
      roughness: 0.9,
      metalness: 0.1,
    });
    this.groundMesh = new THREE.Mesh(groundGeo, groundMat);
    this.groundMesh.rotation.x = -Math.PI / 2;
    this.groundMesh.position.y = 0;
    this.groundMesh.receiveShadow = true;
    this.scene.add(this.groundMesh);

    // ---- Tower group ----
    this.scene.add(this.towerGroup);

    // ---- Orbit controls ----
    this.controls = new OrbitControls(this.camera, this.renderer.domElement);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.08;
    this.controls.maxPolarAngle = Math.PI / 2.1;
    this.controls.minDistance = 3;
    this.controls.maxDistance = 25;

    // ---- Click interaction ----
    this.renderer.domElement.addEventListener('click', this.onClickBound);
  }

  // ============================================================================
  // Tower building
  // ============================================================================

  private buildTowers(): void {
    if (this.towers.length === 0) return;

    const cols = Math.ceil(Math.sqrt(this.towers.length));
    const spacing = 2.0;
    const towerHeight = 2.5;
    const towerRadius = 0.3;

    // Center offset
    const rows = Math.ceil(this.towers.length / cols);
    const offsetX = ((cols - 1) * spacing) / 2;
    const offsetZ = ((rows - 1) * spacing) / 2;

    for (let i = 0; i < this.towers.length; i++) {
      const tower = this.towers[i];
      const towerId = this.getTowerId(tower);
      const col = i % cols;
      const row = Math.floor(i / cols);

      const x = col * spacing - offsetX;
      const z = row * spacing - offsetZ;
      const y = towerHeight / 2;

      // Determine color from alerts & status
      const color = this.getTowerColor(tower, towerId);

      // Create mesh
      const geo = new THREE.CylinderGeometry(towerRadius, towerRadius, towerHeight, 16);
      const mat = new THREE.MeshStandardMaterial({
        color,
        roughness: 0.4,
        metalness: 0.3,
      });
      const mesh = new THREE.Mesh(geo, mat);
      mesh.position.set(x, y, z);
      mesh.userData['towerId'] = towerId;
      mesh.castShadow = true;

      this.towerGroup.add(mesh);
      this.towerMeshes.push(mesh);

      // Create label
      this.createTowerLabel(tower, towerId, x, towerHeight + 0.5, z);
    }
  }

  private rebuildTowers(): void {
    this.disposeTowerMeshes();
    this.disposeTowerLabels();
    this.selectedMesh = null;
    this.buildTowers();
  }

  // ============================================================================
  // Labels
  // ============================================================================

  private createTowerLabel(
    tower: TowerInput,
    towerId: string,
    x: number,
    y: number,
    z: number
  ): void {
    const div = document.createElement('div');
    div.className = 'tower-label';

    // Short display ID — last segment after '/'
    const shortId = towerId.includes('/') ? towerId.split('/').pop()! : towerId;

    // Key metric
    const temp = tower.reported?.airTempC;
    const metricText = temp != null ? `${temp.toFixed(1)}°C` : '';

    div.innerHTML =
      `<span class="label-id">${shortId}</span>` +
      (metricText ? `<span class="label-metric">${metricText}</span>` : '');

    const labelObj = new CSS2DObject(div);
    labelObj.position.set(x, y, z);
    this.towerGroup.add(labelObj);
    this.towerLabels.push(labelObj);
  }

  // ============================================================================
  // Interaction
  // ============================================================================

  private onClick(event: MouseEvent): void {
    const rect = this.renderer.domElement.getBoundingClientRect();
    this.pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    this.pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

    this.raycaster.setFromCamera(this.pointer, this.camera);
    const intersects = this.raycaster.intersectObjects(this.towerMeshes, false);

    if (intersects.length > 0) {
      const hit = intersects[0].object as THREE.Mesh;
      const towerId = hit.userData['towerId'] as string;
      if (towerId) {
        this.selectTower(hit);
        // Emit inside Angular zone so change detection picks it up
        this.zone.run(() => {
          this.towerSelected.emit(towerId);
        });
      }
    }
  }

  private selectTower(mesh: THREE.Mesh): void {
    // Clear previous selection
    if (this.selectedMesh && this.selectedMesh !== mesh) {
      const prevMat = this.selectedMesh.material as THREE.MeshStandardMaterial;
      prevMat.emissive.setHex(0x000000);
      prevMat.emissiveIntensity = 0;
    }

    // Highlight new selection
    const mat = mesh.material as THREE.MeshStandardMaterial;
    mat.emissive.setHex(0x4488ff);
    mat.emissiveIntensity = 0.4;
    this.selectedMesh = mesh;
  }

  // ============================================================================
  // Color determination
  // ============================================================================

  private getTowerColor(tower: TowerInput, towerId: string): number {
    // Offline / no data → gray
    if (tower.metadata && !tower.metadata.isConnected) return COLOR_GRAY;
    if (tower.status === 'offline') return COLOR_GRAY;

    // Collect alerts for this tower
    const towerAlerts = this.alerts.filter(
      a => a.source?.id === towerId && a.status === 'active'
    );
    const hasCritical = towerAlerts.some(a => a.severity === 'critical');
    const hasWarning = towerAlerts.some(a => a.severity === 'warning');

    if (hasCritical || tower.status === 'error') return COLOR_RED;
    if (hasWarning) return COLOR_AMBER;

    return COLOR_GREEN;
  }

  // ============================================================================
  // Helpers
  // ============================================================================

  private getTowerId(tower: TowerInput): string {
    return tower.towerId ?? tower.tower_id ?? tower.id ?? 'unknown';
  }

  // ============================================================================
  // Animation loop
  // ============================================================================

  private startAnimationLoop(): void {
    const animate = () => {
      this.animationFrameId = requestAnimationFrame(animate);
      this.controls.update();
      this.renderer.render(this.scene, this.camera);
      this.labelRenderer.render(this.scene, this.camera);
    };
    animate();
  }

  // ============================================================================
  // Resize handling
  // ============================================================================

  private setupResizeObserver(): void {
    this.resizeObserver = new ResizeObserver(entries => {
      for (const entry of entries) {
        const { width, height } = entry.contentRect;
        if (width === 0 || height === 0) continue;
        this.camera.aspect = width / height;
        this.camera.updateProjectionMatrix();
        this.renderer.setSize(width, height);
        this.labelRenderer.setSize(width, height);
      }
    });
    this.resizeObserver.observe(this.containerRef.nativeElement);
  }

  // ============================================================================
  // Dispose helpers
  // ============================================================================

  private disposeTowerMeshes(): void {
    for (const mesh of this.towerMeshes) {
      mesh.geometry.dispose();
      (mesh.material as THREE.Material).dispose();
      this.towerGroup.remove(mesh);
    }
    this.towerMeshes = [];
  }

  private disposeTowerLabels(): void {
    for (const label of this.towerLabels) {
      // Remove DOM element
      if (label.element && label.element.parentNode) {
        label.element.parentNode.removeChild(label.element);
      }
      this.towerGroup.remove(label);
    }
    this.towerLabels = [];
  }
}
