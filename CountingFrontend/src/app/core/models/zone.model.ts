import { Camera } from "./camera.model";

export interface Zone {
    id: number;
    name: string;
    cameras: Camera[];
    totalCount?: number; 
}

export interface ZoneCreatePayload {
    name: string;
}

export interface ZoneAssociationPayload {
    id: number;
}