import { Component, OnInit, OnDestroy, ViewChildren, QueryList, ElementRef, NgZone, ChangeDetectorRef, Inject } from '@angular/core';
import { CommonModule, DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { Subscription, Observable, filter, take } from 'rxjs';
import { SignalrService, WsConnectionState } from '../../core/services/signalr.service';

interface Point { x: number; y: number; }

interface CameraState {
  id: number;
  name: string;
  displayUrl: SafeUrl | string;
  status: 'Normal' | 'Inactive' | 'Connecting' | 'Reconnecting' | 'Error' | 'Retrying';
  isActive: boolean;
  message?: string;
  roi: number[][]; 
  totalTrackedCount: number;
  zoneIds: number[];
}

interface ZoneState {
  id: number;
  name:string;
  totalCount: number;
  cameraIds: number[];
}

interface LocationState {
    id: number;
    name: string;
    totalCount: number;
    zoneIds: number[];
}

type SelectionType = 'all' | 'location' | 'zone';
interface SelectionContext {
    type: SelectionType;
    id: number;
    name: string;
}

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './dashboard-page.component.html',
  styleUrl: './dashboard-page.component.css'
})
export class DashboardPageComponent implements OnInit, OnDestroy {
  @ViewChildren('videoContainerRef') videoContainerRefs!: QueryList<ElementRef<HTMLDivElement>>;

  private subscriptions = new Subscription();
  public wsStatus$: Observable<WsConnectionState>;

  public cameras: { [id: number]: CameraState } = {};
  public zones: { [id: number]: ZoneState } = {};
  public locations: { [id: number]: LocationState } = {};
  public allCameraKeys: number[] = [];

  public filteredZones: ZoneState[] = [];
  public zoneFilter: string = '';
  public filteredLocations: LocationState[] = [];
  public locationFilter: string = '';
  public paginatedCameraKeys: number[] = [];
  public cameraKeys: number[] = [];
  
  public selectedContext: SelectionContext = { type: 'all', id: 0, name: 'All Cameras' };
  public definingRoiFor: number | null = null;
  public displayRois: { [id: number]: Point[] } = {};
  private readonly pageUrl: string;

  public currentPage = 1;
  public itemsPerPage = 6;
  public totalPages = 1;

  constructor(
    private signalrService: SignalrService,
    private sanitizer: DomSanitizer,
    private ngZone: NgZone,
    private cdr: ChangeDetectorRef,
    @Inject(DOCUMENT) private document: Document
  ) {
    this.pageUrl = this.document.location.href.split('#')[0];
    this.wsStatus$ = this.signalrService.connectionState$;
  }

  ngOnInit(): void {
    if (this.signalrService.connectionStateValue !== 'Connected') {
         this.signalrService.startConnection();
    }
    this.subscribeToEvents();

    this.wsStatus$.pipe(
      filter(state => state === 'Connected'),
      take(1)
    ).subscribe(() => {
      this.signalrService.invoke('RequestInitialState').catch(err => console.error("Initial state request failed:", err));
    });
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  public trackByZoneId = (index: number, zone: ZoneState): number => zone.id;
  public trackByLocationId = (index: number, loc: LocationState): number => loc.id;
  public trackByCameraId = (index: number, cameraId: number): number => cameraId;

  private subscribeToEvents(): void {
    const sub = new Subscription();
    sub.add(this.signalrService.initialState$.subscribe(state => this.handleInitialState(state)));
    sub.add(this.signalrService.cameraStatus$.subscribe(status => this.handleCameraStatus(status)));
    sub.add(this.signalrService.zoneStatus$.subscribe(status => this.handleZoneStatus(status)));
    sub.add(this.signalrService.locationStatus$.subscribe(status => this.handleLocationStatus(status)));
    sub.add(this.signalrService.errorMsg$.subscribe(err => alert(`Server Error: ${err.message}`)));
    this.subscriptions.add(sub);
  }

  private handleInitialState(state: any): void {
    this.ngZone.run(() => {
      console.log("Dashboard received initial state:", state);
      
      const newCameras: { [id: number]: CameraState } = {};
      (state.cameras || []).forEach((cam: any) => {
        newCameras[cam.cameraIndex] = this.mapCameraData(cam);
      });
      this.cameras = newCameras;

      const newZones: { [id: number]: ZoneState } = {};
      (state.zones || []).forEach((zone: any) => {
        newZones[zone.id] = {
          id: zone.id,
          name: zone.name,
          totalCount: zone.totalTrackedCount || 0,
          cameraIds: (zone.cameras || []).map((c: any) => c.id)
        };
      });
      this.zones = newZones;
      this.filterZones();

      const newLocations: { [id: number]: LocationState } = {};
      (state.locations || []).forEach((loc: any) => {
          newLocations[loc.id] = {
            id: loc.id,
            name: loc.name,
            totalCount: loc.totalTrackedCount || 0,
            zoneIds: (loc.zones || []).map((z: any) => z.id)
          };
      });
      this.locations = newLocations;
      this.filterLocations();
      
      this.allCameraKeys = Object.keys(this.cameras).map(Number).sort((a,b) => a - b);
      this.updateVisibleCameras();
      this.cdr.detectChanges();
    });
  }

  private handleCameraStatus(status: any): void {
    this.ngZone.run(() => {
      if (this.cameras[status.cameraIndex]) {
        this.cameras[status.cameraIndex] = this.mapCameraData(status);
      } else {
        this.cameras[status.cameraIndex] = this.mapCameraData(status);
        this.allCameraKeys = Object.keys(this.cameras).map(Number).sort((a, b) => a - b);
        this.updateVisibleCameras();
      }
      this.cdr.detectChanges();
    });
  }
  
  private mapCameraData(camData: any): CameraState {
    const id = camData.cameraIndex;
    const isActive = !(camData.status === 'Inactive' || camData.status === 'Error' || camData.status === 'Retrying');
    
    return {
      id,
      name: camData.name || (this.cameras[id] ? this.cameras[id].name : `Camera ${id}`),
      displayUrl: camData.frameUrl ? this.sanitizer.bypassSecurityTrustUrl(camData.frameUrl) : (this.cameras[id] ? this.cameras[id].displayUrl : ''),
      status: camData.status,
      isActive,
      message: camData.message,
      roi: camData.roi || [],
      totalTrackedCount: camData.totalTrackedCount || 0,
      zoneIds: []
    };
  }

  private handleZoneStatus(status: any): void {
    this.ngZone.run(() => {
      if (this.zones[status.id]) {
        this.zones[status.id].totalCount = status.totalTrackedCount;
      }
      this.filterZones();
      this.cdr.detectChanges();
    });
  }

  private handleLocationStatus(status: any): void {
    this.ngZone.run(() => {
        if (this.locations[status.id]) {
            this.locations[status.id].totalCount = status.totalTrackedCount;
        }
        this.filterLocations();
        this.cdr.detectChanges();
    });
  }

  filterLocations(): void {
    const filter = this.locationFilter.toLowerCase();
    this.filteredLocations = Object.values(this.locations).filter(loc => loc.name.toLowerCase().includes(filter));
  }

  filterZones(): void {
    const filter = this.zoneFilter.toLowerCase();
    this.filteredZones = Object.values(this.zones).filter(zone => zone.name.toLowerCase().includes(filter));
  }
  
  selectContext(type: SelectionType, id: number): void {
    if (this.selectedContext.type === type && this.selectedContext.id === id) {
        this.selectedContext = { type: 'all', id: 0, name: 'All Cameras'};
    } else if (type === 'all') {
        this.selectedContext = { type: 'all', id: 0, name: 'All Cameras'};
    } else if (type === 'location') {
        const loc = this.locations[id];
        if (loc) this.selectedContext = { type: 'location', id: id, name: `Cameras in ${loc.name}`};
    } else if (type === 'zone') {
        const zone = this.zones[id];
        if (zone) this.selectedContext = { type: 'zone', id: id, name: `Cameras in ${zone.name}`};
    }
    this.updateVisibleCameras();
  }

  updateVisibleCameras(): void {
    let visibleCameraIds = new Set<number>();
    if (this.selectedContext.type === 'all') {
        visibleCameraIds = new Set(this.allCameraKeys);
    } else if (this.selectedContext.type === 'location') {
        const loc = this.locations[this.selectedContext.id];
        if (loc) {
            loc.zoneIds.forEach(zoneId => {
                const zone = this.zones[zoneId];
                if (zone) zone.cameraIds.forEach(camId => visibleCameraIds.add(camId));
            });
        }
    } else if (this.selectedContext.type === 'zone') {
        const zone = this.zones[this.selectedContext.id];
        if (zone) zone.cameraIds.forEach(camId => visibleCameraIds.add(camId));
    }
    this.cameraKeys = [...visibleCameraIds].sort((a,b) => a - b);
    this.currentPage = 1;
    this.updatePagination();
  }

  updatePagination(): void {
    this.totalPages = Math.ceil(this.cameraKeys.length / this.itemsPerPage);
    if (this.currentPage > this.totalPages) {
        this.currentPage = this.totalPages || 1;
    }
    const startIndex = (this.currentPage - 1) * this.itemsPerPage;
    this.paginatedCameraKeys = this.cameraKeys.slice(startIndex, startIndex + this.itemsPerPage);
  }

  changePage(direction: number): void {
      const newPage = this.currentPage + direction;
      if (newPage >= 1 && newPage <= this.totalPages) {
          this.currentPage = newPage;
          this.updatePagination();
      }
  }

  toggleDefineRoi(cameraId: number): void {
    this.definingRoiFor = this.definingRoiFor === cameraId ? null : cameraId;
    if (this.definingRoiFor === cameraId) this.displayRois[cameraId] = [];
    else this.updateDisplayRoi(cameraId);
  }

  addRoiPoint(event: MouseEvent, cameraId: number): void {
    if (this.definingRoiFor !== cameraId) return;
    const svgEl = event.currentTarget as SVGElement;
    const svgRect = svgEl.getBoundingClientRect();
    const point: Point = { x: event.clientX - svgRect.left, y: event.clientY - svgRect.top };
    this.displayRois[cameraId] = [...(this.displayRois[cameraId] || []), point];
  }

  clearCurrentRoi(cameraId: number): void { this.displayRois[cameraId] = []; }

  sendRoi(cameraId: number): void {
    const videoContainer = this.videoContainerRefs.toArray().find(ref => ref.nativeElement.closest('.camera-card')?.innerHTML.includes(`(ID: ${cameraId})`))?.nativeElement;
    if (!videoContainer) { console.error(`Could not find video container for camera ID ${cameraId}`); return; }
    const img = videoContainer.querySelector('img');
    if (!img || !this.isRoiValid(cameraId)) return;
    const imgRect = img.getBoundingClientRect();
    const containerRect = videoContainer.getBoundingClientRect();
    const offsetX = imgRect.left - containerRect.left;
    const offsetY = imgRect.top - containerRect.top;
    const normalizedRoi = this.displayRois[cameraId].map(p => [(p.x - offsetX) / imgRect.width, (p.y - offsetY) / imgRect.height]);
    this.signalrService.invoke('SetRoi', { CameraIndex: cameraId, Roi: normalizedRoi });
    this.definingRoiFor = null;
  }

  onImageLoad(cameraId: number): void {
    if (this.definingRoiFor !== cameraId) this.updateDisplayRoi(cameraId);
  }

  private updateDisplayRoi(cameraId: number): void {
    const videoContainer = this.videoContainerRefs?.toArray().find(ref => ref.nativeElement.closest('.camera-card')?.innerHTML.includes(`(ID: ${cameraId})`))?.nativeElement;
    if (!videoContainer) return;
    const img = videoContainer.querySelector('img');
    const normalizedRoi = this.cameras[cameraId]?.roi;
    if (!img || !normalizedRoi || normalizedRoi.length < 3) { this.displayRois[cameraId] = []; return; }
    const imgRect = img.getBoundingClientRect();
    if (imgRect.width === 0 || imgRect.height === 0) return;
    const containerRect = videoContainer.getBoundingClientRect();
    const offsetX = imgRect.left - containerRect.left;
    const offsetY = imgRect.top - containerRect.top;
    this.displayRois[cameraId] = normalizedRoi.map(p => ({ x: p[0] * imgRect.width + offsetX, y: p[1] * imgRect.height + offsetY }));
  }

  resetRoi(cameraId: number): void {
    this.signalrService.invoke('SetRoi', { CameraIndex: cameraId, Roi: [] });
    this.displayRois[cameraId] = [];
  }

  getPolygonPoints = (id: number): string => (this.displayRois[id] || []).map(p => `${p.x},${p.y}`).join(' ');
  isRoiValid = (id: number): boolean => !!this.displayRois[id] && this.displayRois[id].length >= 3;
  getMaskUrl = (id: number): string => `url(${this.pageUrl}#roi-mask-${id})`;
}