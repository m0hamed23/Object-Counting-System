import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { FormBuilder, FormGroup, FormArray, Validators, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { BarrierControlService, BackendBarrierDto, BackendPlcDto } from '../../services/barrier-control.service';
import { Barrier, PLC, PlcCreatePayload, BarrierCreatePayload } from '../../models/barrier.model';

@Component({
  selector: 'app-barrier-control',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './barrier-control.component.html',
  styleUrls: ['./barrier-control.component.css']
})
export class BarrierControlComponent implements OnInit {
  @ViewChild('barrierDialog') barrierDialog!: ElementRef<HTMLDialogElement>;
  @ViewChild('plcDialog') plcDialog!: ElementRef<HTMLDialogElement>;

  barriers: Barrier[] = [];
  plcs: PLC[] = [];
  barrierForm!: FormGroup;
  plcForm!: FormGroup;
  editingBarrier: Barrier | null = null;
  editingPLC: PLC | null = null;

  constructor(
    private barrierService: BarrierControlService,
    private fb: FormBuilder
  ) {
    this.initializeForms();
  }

  ngOnInit() {
    this.loadBarriers();
    this.loadPLCs();
  }

  private initializeForms() {
    this.barrierForm = this.fb.group({
      name: ['', Validators.required],
      // Backend uses camelCase 'plcId', but form control can have snake_case
      // This is mapped in the saveBarrier method
      plc_id: [null, Validators.required],
      open_relay: [null, [Validators.required, Validators.min(1)]], // Relays should be 1-based
      close_relay: [null, [Validators.required, Validators.min(1)]] // Relays should be 1-based
    });

    this.plcForm = this.fb.group({
      ip_address: ['', [Validators.required, Validators.pattern('^(?:[0-9]{1,3}\\.){3}[0-9]{1,3}$')]],
      output_start_address: [null, [Validators.required, Validators.min(0)]],
      input_start_address: [null, [Validators.required, Validators.min(0)]],
      num_outputs: [null, [Validators.required, Validators.min(1)]],
      num_inputs: [null, [Validators.required, Validators.min(1)]],
      ports: this.fb.array(Array(8).fill(null).map(() => this.fb.control<number | null>(null)))
    });
  }

  get plcPorts() {
    return this.plcForm.get('ports') as FormArray;
  }

  loadBarriers() {
    this.barrierService.getBarriers().subscribe((backendBarriers: BackendBarrierDto[]) => {
      // Map from backend's camelCase to frontend's snake_case if needed, but it's better to be consistent.
      // Let's assume frontend model is also camelCase for simplicity.
      this.barriers = backendBarriers.map(b => ({
        id: b.id,
        name: b.name,
        plc_id: b.plcId,
        plc_ip: b.plcIp,
        open_relay: b.openRelay,
        close_relay: b.closeRelay
      }));
    });
  }

  loadPLCs() {
    this.barrierService.getPLCs().subscribe((backendPlcs: BackendPlcDto[]) => {
      this.plcs = backendPlcs.map(p => ({
        id: p.id,
        ip_address: p.ipAddress,
        ports: p.ports && p.ports.length > 0 ? p.ports.slice(0, 8).concat(Array(Math.max(0, 8 - p.ports.length)).fill(null)) : Array(8).fill(null),
        output_start_address: p.outputStartAddress,
        input_start_address: p.inputStartAddress,
        num_outputs: p.numOutputs,
        num_inputs: p.numInputs
      }));
    });
  }

  showAddBarrierDialog() {
    this.editingBarrier = null;
    this.barrierForm.reset({
      name: '',
      plc_id: null,
      open_relay: null,
      close_relay: null
    });
    this.barrierDialog.nativeElement.showModal();
  }

  showAddPLCDialog() {
    this.editingPLC = null;
    this.plcForm.reset({
      ip_address: '',
      output_start_address: 8256, // Common default
      input_start_address: 0, // Common default
      num_outputs: 16, // Common default
      num_inputs: 16, // Common default
    });
    const portsArray = this.plcForm.get('ports') as FormArray;
    portsArray.controls.forEach(control => control.setValue(null));
    this.plcDialog.nativeElement.showModal();
  }

  editBarrier(barrier: Barrier) {
    this.editingBarrier = barrier;
    this.barrierForm.patchValue({
      name: barrier.name,
      plc_id: barrier.plc_id,
      open_relay: barrier.open_relay,
      close_relay: barrier.close_relay
    });
    this.barrierDialog.nativeElement.showModal();
  }

  editPLC(plc: PLC) {
    this.editingPLC = plc;
    const formPorts = (plc.ports || []).slice(0, 8);
    while (formPorts.length < 8) {
      formPorts.push(null);
    }
    this.plcForm.patchValue({
      ip_address: plc.ip_address,
      output_start_address: plc.output_start_address,
      input_start_address: plc.input_start_address,
      num_outputs: plc.num_outputs,
      num_inputs: plc.num_inputs,
      ports: formPorts
    });
    this.plcDialog.nativeElement.showModal();
  }

  saveBarrier() {
    if (!this.barrierForm.valid) {
      console.warn('Barrier form is invalid:', this.barrierForm.errors);
      this.barrierForm.markAllAsTouched();
      return;
    }
    
    // Map from form control names to the camelCase payload expected by the backend
    const barrierPayload: BarrierCreatePayload = {
      name: this.barrierForm.value.name,
      plcId: this.barrierForm.value.plc_id,
      openRelay: this.barrierForm.value.open_relay,
      closeRelay: this.barrierForm.value.close_relay
    };
    
    console.log('Saving Barrier Data (payload to backend):', barrierPayload);

    const saveOperation = this.editingBarrier
      ? this.barrierService.updateBarrier(this.editingBarrier.id, barrierPayload)
      : this.barrierService.addBarrier(barrierPayload);

    saveOperation.subscribe({
      next: () => {
        this.loadBarriers();
        this.closeBarrierDialog();
      },
      error: (err) => {
        console.error('Error saving barrier:', err);
        alert(`Failed to save barrier: ${err.error?.message || err.message}`);
      }
    });
  }

  savePLC() {
    if (!this.plcForm.valid) {
      console.warn('PLC form is invalid. Errors:', this.plcForm.errors);
      this.plcForm.markAllAsTouched();
      return;
    }
    
    const rawPorts: (string | number | null)[] = this.plcForm.value.ports;
    const processedPorts = rawPorts.map(p => {
      if (p === null || (typeof p === 'string' && p.trim() === '')) return null;
      const num = Number(p);
      return isNaN(num) ? null : num;
    });

    // Map from form control names to the camelCase payload
    const plcPayload: PlcCreatePayload = {
      ipAddress: this.plcForm.value.ip_address,
      outputStartAddress: this.plcForm.value.output_start_address,
      inputStartAddress: this.plcForm.value.input_start_address,
      numOutputs: this.plcForm.value.num_outputs,
      numInputs: this.plcForm.value.num_inputs,
      ports: processedPorts
    };

    console.log('Saving PLC Data (payload to backend):', plcPayload);

    const saveOperation = this.editingPLC
      ? this.barrierService.updatePLC(this.editingPLC.id, plcPayload)
      : this.barrierService.addPLC(plcPayload);

    saveOperation.subscribe({
      next: () => {
        this.loadPLCs();
        this.closePLCDialog();
      },
      error: (err) => {
        console.error('Error saving PLC:', err);
        alert(`Failed to save PLC: ${err.error?.message || err.message}`);
      }
    });
  }

  deleteBarrier(id: number) {
    if (confirm('Are you sure you want to delete this barrier?')) {
      this.barrierService.deleteBarrier(id).subscribe({
        next: () => this.loadBarriers(),
        error: (err) => {
          console.error('Error deleting barrier:', err);
          alert(`Error deleting barrier: ${err.error?.message || err.message}`);
        }
      });
    }
  }

  deletePLC(id: number) {
    if (confirm('Are you sure you want to delete this PLC? This will fail if any barriers are still assigned to it.')) {
      this.barrierService.deletePLC(id).subscribe({
        next: () => {
          this.loadPLCs();
          this.loadBarriers(); // Refresh barriers in case any were linked
        },
        error: (err) => {
          console.error('Error deleting PLC:', err);
          alert(`Error deleting PLC: ${err.error?.message || err.message}`);
        }
      });
    }
  }

  controlBarrier(id: number, action: 'open' | 'close') {
    this.barrierService.controlBarrier(id, action).subscribe({
      next: (res: any) => {
        console.log(`Barrier ${id} ${action} command sent successfully.`, res);
        // Optionally show a temporary success message
      },
      error: (err) => {
        console.error(`Error ${action}ing barrier ${id}:`, err);
        alert(`Failed to ${action} barrier: ${err.error?.message || err.message}`);
      }
    });
  }

  closeBarrierDialog() {
    this.barrierDialog.nativeElement.close();
    this.editingBarrier = null;
  }

  closePLCDialog() {
    this.plcDialog.nativeElement.close();
    this.editingPLC = null;
  }
}