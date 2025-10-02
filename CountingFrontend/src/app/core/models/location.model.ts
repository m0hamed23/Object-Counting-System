import { Zone } from "./zone.model";

export interface Location {
    id: number;
    name: string;
    zones: Zone[];
    totalCount?: number; 
}

export interface LocationCreatePayload {
    name: string;
}

export interface LocationAssociationPayload {
    id: number; 
}