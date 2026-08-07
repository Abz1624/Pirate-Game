import * as THREE from 'three';
import { ARENA_RADIUS } from './types';

export class SceneManager {
  scene: THREE.Scene;
  camera: THREE.PerspectiveCamera;
  renderer: THREE.WebGLRenderer;
  oceanMesh: THREE.Mesh;
  private shakeIntensity = 0;
  private shakeDecay = 5;
  private baseCameraPos = new THREE.Vector3(0, 45, 30);
  private cameraTarget = new THREE.Vector3();

  constructor(canvas: HTMLCanvasElement) {
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x1a3050);
    this.scene.fog = new THREE.Fog(0x1a3050, 80, 160);

    this.camera = new THREE.PerspectiveCamera(50, window.innerWidth / window.innerHeight, 0.5, 300);
    this.camera.position.copy(this.baseCameraPos);
    this.camera.lookAt(0, 0, 0);

    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
    this.renderer.setSize(window.innerWidth, window.innerHeight);
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;

    const ambient = new THREE.AmbientLight(0x6688aa, 0.6);
    this.scene.add(ambient);

    const sun = new THREE.DirectionalLight(0xffeedd, 1.2);
    sun.position.set(30, 50, 20);
    sun.castShadow = true;
    sun.shadow.mapSize.set(2048, 2048);
    sun.shadow.camera.near = 1;
    sun.shadow.camera.far = 120;
    sun.shadow.camera.left = -60;
    sun.shadow.camera.right = 60;
    sun.shadow.camera.top = 60;
    sun.shadow.camera.bottom = -60;
    this.scene.add(sun);

    const oceanGeo = new THREE.CircleGeometry(ARENA_RADIUS + 10, 64);
    const oceanMat = new THREE.MeshStandardMaterial({
      color: 0x1a4a6a,
      roughness: 0.3,
      metalness: 0.1,
    });
    this.oceanMesh = new THREE.Mesh(oceanGeo, oceanMat);
    this.oceanMesh.rotation.x = -Math.PI / 2;
    this.oceanMesh.receiveShadow = true;
    this.scene.add(this.oceanMesh);

    const ringGeo = new THREE.RingGeometry(ARENA_RADIUS - 0.5, ARENA_RADIUS, 64);
    const ringMat = new THREE.MeshBasicMaterial({ color: 0xff4444, transparent: true, opacity: 0.3, side: THREE.DoubleSide });
    const ring = new THREE.Mesh(ringGeo, ringMat);
    ring.rotation.x = -Math.PI / 2;
    ring.position.y = 0.05;
    this.scene.add(ring);

    window.addEventListener('resize', () => this.onResize());
  }

  setBiomeColor(color: number): void {
    (this.oceanMesh.material as THREE.MeshStandardMaterial).color.setHex(color);
  }

  updateCamera(targetX: number, targetZ: number, dt: number): void {
    this.cameraTarget.set(targetX, 0, targetZ);
    const desiredPos = new THREE.Vector3(
      targetX + this.baseCameraPos.x * 0.3,
      this.baseCameraPos.y,
      targetZ + this.baseCameraPos.z,
    );

    if (this.shakeIntensity > 0) {
      desiredPos.x += (Math.random() - 0.5) * this.shakeIntensity;
      desiredPos.z += (Math.random() - 0.5) * this.shakeIntensity;
      this.shakeIntensity = Math.max(0, this.shakeIntensity - this.shakeDecay * dt);
    }

    this.camera.position.lerp(desiredPos, 5 * dt);
    this.camera.lookAt(this.cameraTarget);
  }

  shake(intensity: number): void {
    this.shakeIntensity = Math.max(this.shakeIntensity, intensity);
  }

  render(): void {
    this.renderer.render(this.scene, this.camera);
  }

  private onResize(): void {
    this.camera.aspect = window.innerWidth / window.innerHeight;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(window.innerWidth, window.innerHeight);
  }
}

export class EffectsManager {
  private container: HTMLElement;

  constructor(container: HTMLElement) {
    this.container = container;
  }

  showDamageNumber(screenX: number, screenY: number, damage: number, isCrit: boolean, isWeak: boolean): void {
    const el = document.createElement('div');
    el.className = 'damage-number' + (isCrit ? ' crit' : isWeak ? ' weak' : '');
    el.textContent = Math.round(damage).toString();
    el.style.left = `${screenX}px`;
    el.style.top = `${screenY}px`;
    this.container.appendChild(el);
    setTimeout(() => el.remove(), 800);
  }

  showLevelFlash(): void {
    const el = document.createElement('div');
    el.className = 'level-flash';
    this.container.appendChild(el);
    setTimeout(() => el.remove(), 400);
  }

  worldToScreen(
    worldPos: THREE.Vector3,
    camera: THREE.Camera,
    renderer: THREE.WebGLRenderer,
  ): { x: number; y: number } {
    const projected = worldPos.clone().project(camera);
    const canvas = renderer.domElement;
    const rect = canvas.getBoundingClientRect();
    return {
      x: rect.left + (projected.x * 0.5 + 0.5) * rect.width,
      y: rect.top + (-projected.y * 0.5 + 0.5) * rect.height,
    };
  }
}
