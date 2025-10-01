export interface Camera {
    id: number;
    name: string;
    rtspUrl: string;
    isEnabled: boolean;
    lastUpdated: Date;
}

export interface CameraCreatePayload {
    name: string;
    rtspUrl: string;
    isEnabled: boolean;
}