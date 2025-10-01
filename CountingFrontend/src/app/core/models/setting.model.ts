export interface Setting {
    name: string;
    displayName: string;
    value: string | null;
    description: string | null;
    isVisible: boolean;
}