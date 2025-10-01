import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { BarrierCreatePayload, PlcCreatePayload } from '../models/barrier.model';
import { environment } from '../../../../environments/environment';

// Interfaces for data received FROM backend (matching backend DTOs directly)
export interface BackendBarrierDto {
  id: number;
  name: string;
  plcId: number;
  plcIp: string | null;
  openRelay: number;
  closeRelay: number;
}

export interface BackendPlcDto {
  id: number;
  ipAddress: string;
  ports: (number | null)[];
  outputStartAddress: number | null;
  inputStartAddress: number | null;
  numOutputs: number | null;
  numInputs: number | null;
}


@Injectable({
  providedIn: 'root'
})
export class BarrierControlService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // Barrier Operations
  getBarriers(): Observable<BackendBarrierDto[]> {
    return this.http.get<BackendBarrierDto[]>(`${this.apiUrl}/barriers`);
  }

  addBarrier(barrier: BarrierCreatePayload): Observable<BackendBarrierDto> {
    return this.http.post<BackendBarrierDto>(`${this.apiUrl}/barriers`, barrier);
  }

  updateBarrier(id: number, barrier: BarrierCreatePayload): Observable<BackendBarrierDto> {
    return this.http.put<BackendBarrierDto>(`${this.apiUrl}/barriers/${id}`, barrier);
  }

  deleteBarrier(id: number): Observable<any> {
    return this.http.delete<any>(`${this.apiUrl}/barriers/${id}`);
  }

  controlBarrier(id: number, action: 'open' | 'close'): Observable<any> {
    // *** THE FIX IS HERE: Change the body from `{}` to `null` ***
    // This prevents Angular from sending a "Content-Type: application/json" header,
    // which was confusing the ASP.NET Core model binder and causing the 404.
    return this.http.post<any>(`${this.apiUrl}/barriers/${id}/${action}`, null);
  }

  // PLC Operations
  getPLCs(): Observable<BackendPlcDto[]> {
    return this.http.get<BackendPlcDto[]>(`${this.apiUrl}/plcs`);
  }

  addPLC(plc: PlcCreatePayload): Observable<BackendPlcDto> {
    return this.http.post<BackendPlcDto>(`${this.apiUrl}/plcs`, plc);
  }

  updatePLC(id: number, plc: PlcCreatePayload): Observable<BackendPlcDto> {
    return this.http.put<BackendPlcDto>(`${this.apiUrl}/plcs/${id}`, plc);
  }

  deletePLC(id: number): Observable<any> {
    return this.http.delete<any>(`${this.apiUrl}/plcs/${id}`);
  }
}