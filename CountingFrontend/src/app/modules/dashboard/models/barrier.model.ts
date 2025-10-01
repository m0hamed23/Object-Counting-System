export interface Barrier {
  id: number;
  name: string;
  plc_id: number;     // Matches formControlName, used for display/editing
  plc_ip: string | null; // Can be null if PLC IP not found
  open_relay: number; // Matches formControlName
  close_relay: number;// Matches formControlName
}

export interface PLC {
  id: number;
  ip_address: string;             // Matches formControlName
  ports: (number | null)[];       // Matches formArray element type (number or null)
  output_start_address: number | null; // Matches formControlName
  input_start_address: number | null;  // Matches formControlName
  num_outputs: number | null;          // Matches formControlName
  num_inputs: number | null;           // Matches formControlName
}

// This interface is for the payload sent TO the backend
// It should match the backend DTO (PlcCreateDto)
export interface PlcCreatePayload {
  ipAddress: string;
  ports?: (number | null)[] | null; // Optional, matches backend DTO
  outputStartAddress: number | null;
  inputStartAddress: number | null;
  numOutputs: number | null;
  numInputs: number | null;
}

// This interface is for the payload sent TO the backend for barriers
export interface BarrierCreatePayload {
    name: string;
    plcId: number;
    openRelay: number;
    closeRelay: number;
}