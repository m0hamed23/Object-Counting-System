
export interface Action {
    id: number;
    name: string;
    ipAddress: string;
    port: number;
    intervalMilliseconds: number;
    protocol: 'TCP' | 'UDP';
    isEnabled: boolean;
}

export interface ActionCreatePayload {
    name: string;
    ipAddress: string;
    port: number;
    intervalMilliseconds: number;
    protocol: 'TCP' | 'UDP';
    isEnabled: boolean;
}