import { Injectable, NgZone } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Subject, ReplaySubject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export type WsConnectionState = 'Connecting' | 'Connected' | 'Reconnecting' | 'Disconnected' | 'Error';

@Injectable({
  providedIn: 'root'
})
export class SignalrService {
  private hubConnection!: signalR.HubConnection;
  
  private readonly _connectionState = new BehaviorSubject<WsConnectionState>('Disconnected');
  public readonly connectionState$ = this._connectionState.asObservable();

  public initialState$ = new ReplaySubject<any>(1); 
  public cameraStatus$ = new Subject<any>();
  public zoneStatus$ = new Subject<any>();
  public locationStatus$ = new Subject<any>();
  public roiUpdateAck$ = new Subject<any>();
  public errorMsg$ = new Subject<any>();

  constructor(private authService: AuthService, private ngZone: NgZone) {}

  public get connectionStateValue(): WsConnectionState {
    return this._connectionState.getValue();
  }

  public startConnection(): void {
    if (this.hubConnection && this.hubConnection.state !== signalR.HubConnectionState.Disconnected) {
      return;
    }
    
    this.updateState('Connecting');
    
    const token = this.authService.getToken();
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(environment.signalRHubUrl, { accessTokenFactory: () => token || "" })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();

    this.registerEvents();
    this.hubConnection.start()
      .then(() => {
        this.updateState('Connected');
        console.log('SignalR connection started successfully.');
      })
      .catch(err => {
        console.error('Error while starting SignalR connection: ' + err);
        this.updateState('Error');
      });
  }

  public stopConnection(): void {
    if (this.hubConnection) {
      this.hubConnection.stop().then(() => this.updateState('Disconnected'));
    }
  }

  private registerEvents(): void {
    this.hubConnection.onclose(() => this.updateState('Disconnected'));
    this.hubConnection.onreconnecting(() => this.updateState('Reconnecting'));
    
    this.hubConnection.onreconnected(() => {
        this.updateState('Connected');
        this.invoke('RequestInitialState');
    });

    this.hubConnection.on('initial_state', data => this.ngZone.run(() => this.initialState$.next(data)));
    this.hubConnection.on('camera_status', data => this.ngZone.run(() => this.cameraStatus$.next(data)));
    this.hubConnection.on('zone_status', data => this.ngZone.run(() => this.zoneStatus$.next(data)));
    this.hubConnection.on('location_status', data => this.ngZone.run(() => this.locationStatus$.next(data)));
    this.hubConnection.on('roi_update_ack', data => this.ngZone.run(() => this.roiUpdateAck$.next(data)));
    this.hubConnection.on('error_msg', data => this.ngZone.run(() => this.errorMsg$.next(data)));
  }

  private updateState(state: WsConnectionState): void {
    this.ngZone.run(() => this._connectionState.next(state));
  }

  public invoke(methodName: string, ...args: any[]) {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      return this.hubConnection.invoke(methodName, ...args);
    }
    console.error(`Cannot invoke '${methodName}'. Connection is not in the 'Connected' state.`);
    return Promise.reject('Connection not established.');
  }
}