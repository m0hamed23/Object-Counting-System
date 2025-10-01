import { Subject, Subscription } from 'rxjs';
import { take } from 'rxjs/operators';
import { CommonModule, DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { environment } from '../../../../../environments/environment';
import { SettingsService } from '../../services/settings.service';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { AuthService } from '../../../../services/auth.service';
import { Component, OnInit, OnDestroy, ViewChildren, ElementRef, QueryList, ChangeDetectorRef, NgZone, Inject } from '@angular/core';
import * as signalR from "@microsoft/signalr";

// --- INTERFACES ---
interface CameraFeed {
  index: number;
  rtsp_url: string;
  display_url: SafeUrl | string;
  status: 'Normal' | 'Jam' | 'Clear' | 'Inactive' | 'Connecting' | 'Reconnecting' | 'Error';
  is_active: boolean;
  message?: string;
  isJamActive?: boolean;
  stationaryCount?: number;
}

interface Point {
  x: number;
  y: number;
}

@Component({
  selector: 'app-crowd-monitor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './crowd-monitor.component.html',
  styleUrls: ['./crowd-monitor.component.css']
})
export class CrowdMonitorComponent implements OnInit, OnDestroy {
  @ViewChildren('videoContainerRef') videoContainerRefs!: QueryList<ElementRef<HTMLDivElement>>;

  private hubConnection!: signalR.HubConnection;
  private hubConnectionSubscription: Subscription = new Subscription();
  private destroy$ = new Subject<void>();

  wsStatus: 'Connecting' | 'Connected' | 'Reconnecting' | 'Disconnected' | 'Error' = 'Connecting';
  mode: 'Manual' | 'Automatic' = 'Manual';
  cameraFeeds: CameraFeed[] = [];
  isLoadingSettings = true;

  definingRoiFor: number | null = null;
  
  // This will hold the pixel-based ROI for rendering on the SVG.
  // When defining a new ROI, this is populated by user clicks.
  // When loading an existing ROI, this is populated by updateDisplayRoi().
  currentRois: { [key: number]: Point[] } = {};

  // This will hold the normalized ROI received from the backend, used as the source of truth.
  private savedNormalizedRois: { [key: number]: number[][] } = {};
  
  private readonly pageUrl: string;

  constructor(
    private settingsService: SettingsService,
    private sanitizer: DomSanitizer,
    private authService: AuthService,
    private ngZone: NgZone,
    private cdr: ChangeDetectorRef,
    @Inject(DOCUMENT) private document: Document
  ) {
    this.pageUrl = this.document.location.href.split('#')[0];
  }

  ngOnInit() {
    this.loadInitialSettingsAndFeeds();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
    this.hubConnectionSubscription.unsubscribe();
    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
      this.hubConnection.stop().catch(err => console.error("Error stopping SignalR connection:", err));
    }
  }

  private initializeSignalRConnection() {
    const hubUrl = environment.signalRHubUrl;
    console.log(`Initializing SignalR connection to: ${hubUrl}`);
    this.wsStatus = 'Connecting';
    this.cdr.detectChanges();

    const token = this.authService.getToken();
    if (!token) {
        console.error("SignalR: No auth token found. Connection will likely fail if auth is required.");
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => token || "" })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 15000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.hubConnection.onreconnecting((error?: Error) => {
        this.ngZone.run(() => {
            console.warn('SignalR attempting to reconnect due to:', error);
            this.wsStatus = 'Reconnecting';
            this.cameraFeeds.forEach(feed => {
                if (feed.status !== 'Error' && feed.status !== 'Inactive') {
                    feed.status = 'Reconnecting';
                    feed.is_active = false;
                }
            });
            this.cdr.detectChanges();
        });
    });

    this.hubConnection.onreconnected((connectionId?: string) => {
        this.ngZone.run(() => {
            console.log('SignalR reconnected successfully. Connection ID:', connectionId);
            this.wsStatus = 'Connected';
            this.invokeHubMethod('RequestInitialState');
            this.cdr.detectChanges();
        });
    });

    this.hubConnection.onclose((error?: Error) => {
        this.ngZone.run(() => {
            console.warn('SignalR connection closed.', error);
            this.wsStatus = 'Disconnected';
             this.cameraFeeds.forEach(feed => {
                if (feed.status !== 'Error' && feed.status !== 'Inactive') {
                    feed.status = 'Reconnecting';
                    feed.is_active = false;
                }
            });
            this.cdr.detectChanges();
        });
    });

    this.hubConnection.on('camera_status', (message: any) => this.ngZone.run(() => { this.handleSignalRMessage(message, 'camera_status'); }));
    this.hubConnection.on('initial_state', (message: any) => this.ngZone.run(() => { this.handleSignalRMessage(message, 'initial_state'); }));
    this.hubConnection.on('mode_updated', (message: any) => this.ngZone.run(() => { this.handleSignalRMessage(message, 'mode_updated'); }));
    this.hubConnection.on('barrier_action_status', (message: any) => this.ngZone.run(() => { this.handleSignalRMessage(message, 'barrier_action_status'); }));
    this.hubConnection.on('roi_update_ack', (message: any) => this.ngZone.run(() => { this.handleSignalRMessage(message, 'roi_update_ack'); }));
    this.hubConnection.on('error_msg', (message: any) => this.ngZone.run(() => { this.handleSignalRMessage(message, 'error_msg'); }));

    this.startSignalRConnection();
  }

  private startSignalRConnection() {
      console.log("Attempting to start SignalR connection...");
      this.hubConnection.start()
        .then(() => {
          this.ngZone.run(() => {
            console.log('SignalR connected successfully. Connection ID:', this.hubConnection.connectionId);
            this.wsStatus = 'Connected';
            this.invokeHubMethod('RequestInitialState');
            this.cdr.detectChanges();
          });
        })
        .catch(err => {
          this.ngZone.run(() => {
            console.error('Error starting SignalR connection:', err);
            this.wsStatus = 'Error';
            this.cameraFeeds.forEach(feed => {
                if (feed.status !== 'Error' && feed.status !== 'Inactive') {
                    feed.status = 'Reconnecting';
                     feed.is_active = false;
                }
            });
            this.cdr.detectChanges();
          });
        });
  }

  private loadInitialSettingsAndFeeds() {
    console.log("Loading initial settings...");
    this.isLoadingSettings = true;
    this.cdr.detectChanges();

    this.settingsService.settings$.pipe(
      take(1)
    ).subscribe({
      next: (settings) => {
        this.ngZone.run(() => {
          console.log("Settings fetched:", settings);
          this.cameraFeeds = [];

          for (let i = 1; i <= 4; i++) {
            const rtspSetting = settings.find(s => s.name === `rtsp_url_${i}`);
            const index = i - 1;
            this.cameraFeeds[index] = {
              index,
              rtsp_url: rtspSetting?.value || '',
              display_url: '',
              status: 'Inactive',
              is_active: false
            };
            this.currentRois[index] = [];
            this.savedNormalizedRois[index] = [];
          }

          const modeSetting = settings.find(s => s.name === 'operation_mode');
          this.mode = (modeSetting?.value === 'Automatic') ? 'Automatic' : 'Manual';
          
          this.isLoadingSettings = false;
          this.cdr.detectChanges();
          this.initializeSignalRConnection();
        });
      },
      error: (err) => {
        this.ngZone.run(() => {
          console.error("Error loading settings:", err);
          this.isLoadingSettings = false;
          
          this.cameraFeeds = Array.from({ length: 4 }, (_, i) => ({
            index: i,
            rtsp_url: '',
            display_url: '',
            status: 'Error',
            is_active: false,
            message: 'Failed to load settings'
          }));
          
          this.mode = 'Manual';
          this.cdr.detectChanges();
          this.initializeSignalRConnection();
        });
      }
    });
  }

  private handleSignalRMessage(message: any, messageType: string) {
    this.ngZone.run(() => {
        switch (messageType) {
        case 'camera_status':
            const feed = this.cameraFeeds.find(f => f.index === message.cameraIndex);
            if (feed) {
              feed.status = message.status;
              feed.is_active = !(message.status === 'Inactive' || message.status === 'Error' || message.status === 'Connecting' || message.status === 'Reconnecting');
              feed.message = message.message || '';
              feed.isJamActive = message.isJamActive;
              feed.stationaryCount = message.stationaryCount;
              if (message.frameUrl) {
                  feed.display_url = this.sanitizer.bypassSecurityTrustUrl(message.frameUrl);
              } else if (feed.status !== 'Connecting' && feed.status !== 'Reconnecting' ) {
                  feed.display_url = '';
              }

              if (message.roi) {
                this.savedNormalizedRois[feed.index] = message.roi;
                // --- THE FIX IS HERE ---
                // Only update the displayed ROI from the backend if the user is NOT actively defining a new one.
                if (feed.is_active && this.definingRoiFor !== feed.index) {
                    this.updateDisplayRoi(feed.index);
                }
              }
            }
            break;
        case 'initial_state':
            console.log("Received initial state from SignalR backend:", message);
            if (message.mode === 'Manual' || message.mode === 'Automatic') this.mode = message.mode;
            if (message.cameras && Array.isArray(message.cameras)) {
                message.cameras.forEach((camState: any) => {
                    const existingFeed = this.cameraFeeds.find(f => f.index === camState.cameraIndex);
                    if (existingFeed) {
                        existingFeed.status = camState.status;
                        existingFeed.is_active = !(camState.status === 'Inactive' || camState.status === 'Error' || camState.status === 'Connecting' || camState.status === 'Reconnecting');
                        existingFeed.message = camState.message || '';
                        existingFeed.isJamActive = camState.isJamActive;
                        existingFeed.stationaryCount = camState.stationaryCount;
                        if (camState.frameUrl) {
                            existingFeed.display_url = this.sanitizer.bypassSecurityTrustUrl(camState.frameUrl);
                        } else {
                            existingFeed.display_url = '';
                        }
                        if (camState.roi) {
                            this.savedNormalizedRois[existingFeed.index] = camState.roi;
                            this.updateDisplayRoi(existingFeed.index);
                        }
                    }
                });
            }
            break;
        case 'mode_updated':
            if (message.mode === 'Manual' || message.mode === 'Automatic') {
                this.mode = message.mode;
                console.log("Global mode updated from server (SignalR):", this.mode);
            }
            break;
        case 'barrier_action_status': console.log("Barrier action status (SignalR):", message); break;
        case 'roi_update_ack': console.log(`ROI update acknowledged by SignalR backend for camera ${message.cameraIndex}`); break;
        case 'error_msg': console.error("Error message from SignalR backend:", message.message); break;
        default: console.warn("Received unhandled SignalR message type:", messageType, message);
        }
        this.cdr.detectChanges();
    });
  }

  private invokeHubMethod(methodName: string, ...args: any[]) {
    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
      this.hubConnection.invoke(methodName, ...args)
        .catch(err => console.error(`Error invoking hub method '${methodName}':`, err));
    } else {
      console.warn(`Cannot invoke hub method '${methodName}': SignalR connection not established.`);
    }
  }

  onModeChange() {
    this.invokeHubMethod('ModeChange', this.mode);
  }

  openBarrier() {
    if (this.mode === 'Manual') this.invokeHubMethod('BarrierControlWs', 'open');
  }

  closeBarrier() {
    if (this.mode === 'Manual') this.invokeHubMethod('BarrierControlWs', 'close');
  }

  toggleCamera(index: number) {
    const feed = this.cameraFeeds[index];
    if (!feed || !this.hubConnection || this.hubConnection.state !== signalR.HubConnectionState.Connected) {
       console.warn(`Cannot toggle camera ${index}: Feed missing or SignalR disconnected.`);
       return;
    }
    const desiredAction = feed.is_active && feed.status !== 'Connecting' ? 'stop' : 'start';
    console.log(`Client: Sending ${desiredAction} command for camera ${index} (SignalR)`);

    if (desiredAction === 'start') {
        feed.status = 'Connecting';
        feed.is_active = true;
        feed.display_url = '';
    } else {
        feed.status = 'Inactive';
        feed.is_active = false;
        // When stopping, clear the displayed ROI, it will be re-rendered from saved data on start
        this.currentRois[index] = [];
    }
    this.cdr.detectChanges();
    this.invokeHubMethod('CameraControl', index, desiredAction);
  }

  toggleDefineRoi(cameraIndex: number) {
    if (this.definingRoiFor === cameraIndex) {
      this.cancelRoiDefinition(cameraIndex, true);
    } else {
       const feed = this.cameraFeeds[cameraIndex];
       if (feed && feed.is_active && feed.status !== 'Error' && feed.status !== 'Connecting' && feed.status !== 'Reconnecting') {
           console.log(`Starting ROI definition for camera ${cameraIndex}`);
           this.definingRoiFor = cameraIndex;
           this.currentRois[cameraIndex] = []; // Start with a clean slate for the new definition
       } else {
            console.warn(`Cannot define ROI for camera ${cameraIndex} in status: ${feed?.status}`);
       }
    }
    this.cdr.detectChanges();
  }

  addRoiPoint(event: MouseEvent, cameraIndex: number) {
    if (this.definingRoiFor !== cameraIndex) return;
  
    const svgElement = event.currentTarget as SVGElement;
    const svgRect = svgElement.getBoundingClientRect();
  
    const newPoint: Point = {
      x: event.clientX - svgRect.left,
      y: event.clientY - svgRect.top
    };
  
    // Create a new array reference to trigger Angular's change detection for *ngFor.
    const updatedRoi = [...this.currentRois[cameraIndex], newPoint];
    this.currentRois[cameraIndex] = updatedRoi;
  
    console.log(`Added point for Cam ${cameraIndex}:`, newPoint, `Total points: ${this.currentRois[cameraIndex].length}`);
    
    this.cdr.detectChanges();
  }

  cancelRoiDefinition(cameraIndex: number, andExitMode: boolean = false) {
    console.log(`Clearing/resetting ROI points for camera ${cameraIndex}`);
    // Restore the displayed ROI from the last saved state
    this.updateDisplayRoi(cameraIndex);
    if (andExitMode) {
      this.definingRoiFor = null;
    }
    this.cdr.detectChanges();
  }
  
  isRoiValid(cameraIndex: number): boolean {
    return !!this.currentRois[cameraIndex] && this.currentRois[cameraIndex].length >= 3;
  }

  getPolygonPoints(cameraIndex: number): string {
    if (!this.currentRois[cameraIndex]) return '';
    return this.currentRois[cameraIndex].map(p => `${p.x},${p.y}`).join(' ');
  }

  public getMaskUrl(cameraIndex: number): string {
    return `url(${this.pageUrl}#roi-mask-${cameraIndex})`;
  }

  sendRoi(cameraIndex: number) {
    const roiClientPoints = this.currentRois[cameraIndex];
    const videoContainerElement = this.videoContainerRefs.toArray()[cameraIndex]?.nativeElement;
    const imgElement = videoContainerElement?.querySelector('img');
  
    if (!this.isRoiValid(cameraIndex) || !videoContainerElement || !imgElement) {
      console.error('Cannot send ROI: Invalid polygon data or elements not found.');
      return;
    }
    if (!this.hubConnection || this.hubConnection.state !== signalR.HubConnectionState.Connected) {
      console.error('Cannot send ROI: SignalR connection not established.');
      this.definingRoiFor = null;
      return;
    }
  
    const containerRect = videoContainerElement.getBoundingClientRect();
    const imgRect = imgElement.getBoundingClientRect();
  
    const offsetX = imgRect.left - containerRect.left;
    const offsetY = imgRect.top - containerRect.top;
    const imgWidth = imgRect.width;
    const imgHeight = imgRect.height;
  
    if (imgWidth === 0 || imgHeight === 0) {
      console.error('Cannot normalize ROI: Image dimensions are zero.');
      return;
    }
  
    const roiPointsNormalizedForBackend = roiClientPoints.map(point => {
      const adjustedX = point.x - offsetX;
      const adjustedY = point.y - offsetY;
      const normX = Math.max(0, Math.min(1, adjustedX / imgWidth));
      const normY = Math.max(0, Math.min(1, adjustedY / imgHeight));
      return [normX, normY];
    });
  
    const roiDataDto = {
      CameraIndex: cameraIndex,
      Roi: roiPointsNormalizedForBackend
    };
  
    this.invokeHubMethod('SetRoi', roiDataDto);
    console.log(`Polygon ROI for camera ${cameraIndex + 1} sent to backend (normalized):`, roiPointsNormalizedForBackend);
    
    // Update the "source of truth" with the new ROI
    this.savedNormalizedRois[cameraIndex] = roiPointsNormalizedForBackend;

    this.definingRoiFor = null;
    this.cdr.detectChanges();
  }

  private updateDisplayRoi(cameraIndex: number) {
    const normalizedRoi = this.savedNormalizedRois[cameraIndex];
    if (!normalizedRoi || normalizedRoi.length < 3) {
      this.ngZone.run(() => { 
        this.currentRois[cameraIndex] = [];
        this.cdr.detectChanges();
      });
      return;
    }
    
    const videoContainerElement = this.videoContainerRefs.toArray()[cameraIndex]?.nativeElement;
    const imgElement = videoContainerElement?.querySelector('img');
    
    if (!imgElement) return;

    const imgRect = imgElement.getBoundingClientRect();
    const imgWidth = imgRect.width;
    const imgHeight = imgRect.height;
    
    if (imgWidth === 0 || imgHeight === 0) {
      return; 
    }
    
    this.ngZone.run(() => {
      // Denormalize points based on the actual displayed image size
      const denormalizedPoints = normalizedRoi.map(point => ({
        x: point[0] * imgWidth,
        y: point[1] * imgHeight
      }));
      // Recalculate SVG coordinates based on image offset within container
      const containerRect = videoContainerElement.getBoundingClientRect();
      const offsetX = imgRect.left - containerRect.left;
      const offsetY = imgRect.top - containerRect.top;

      this.currentRois[cameraIndex] = denormalizedPoints.map(p => ({
          x: p.x + offsetX,
          y: p.y + offsetY
      }));
      this.cdr.detectChanges();
    });
  }

  public onImageLoad(cameraIndex: number) {
    // If we are not currently defining a new ROI, update the display with the saved one.
    if (this.definingRoiFor !== cameraIndex) {
      this.updateDisplayRoi(cameraIndex);
    }
  }
}