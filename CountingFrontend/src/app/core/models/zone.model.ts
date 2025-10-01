import { Camera } from "./camera.model";

export interface Zone {
    id: number;
    name: string;
    cameras: Camera[];
    totalCount?: number; // Simplified for real-time data
}

export interface ZoneCreatePayload {
    name: string;
}

export interface ZoneAssociationPayload {
    id: number;
}