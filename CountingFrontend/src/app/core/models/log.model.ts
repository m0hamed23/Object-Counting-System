export interface LogEntry {
    id: number;
    timestamp: string;
    event: string;
    imageName: string | null;
    cameraIndex: number | null;
}