import { Zone } from "./zone.model";

export interface Location {
    id: number;
    name: string;
    zones: Zone[];
    totalCount?: number; // Simplified for real-time data
}

export interface LocationCreatePayload {
    name: string;
}

export interface LocationAssociationPayload {
    id: number; // This will be the Zone ID
}