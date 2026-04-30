import { Injectable, signal } from '@angular/core';

export type FaturaUIState = 'list' | 'create' | 'edit' | 'details';
export type VeiculoUIState = 'list' | 'create' | 'edit' | 'details';
export type FornecedorUIState = 'list' | 'create' | 'edit';
export type RecepcaoUIState = 'list' | 'create' | 'edit';
export type AtribuicaoUIState = 'list' | 'create' | 'edit';
export type FechoViagemUIState = 'list' | 'create' | 'edit';
export type GestaoViagemUIState = 'list' | 'create' | 'edit';
export type IncidenteUIState = 'list' | 'create' | 'edit';
export type GuiaUIState = 'list' | 'create' | 'edit';
export type DocumentoUIState = 'list' | 'create' | 'edit' | 'details';







@Injectable({ providedIn: 'root' })
export class UiStateService {
  // ==================== FATURAS ====================
  private faturaState = signal<FaturaUIState>('list');
  private selectedFaturaId = signal<number | null>(null);

  readonly currentFaturaState = this.faturaState.asReadonly();
  readonly currentFaturaId = this.selectedFaturaId.asReadonly();

  setFaturaState(state: FaturaUIState, id?: number): void {
    this.faturaState.set(state);
    this.selectedFaturaId.set(id ?? null);
  }

  goToFaturaList(): void {
    this.faturaState.set('list');
    this.selectedFaturaId.set(null);
  }

  goToFaturaCreate(): void {
    this.faturaState.set('create');
    this.selectedFaturaId.set(null);
  }

  goToFaturaEdit(id: number): void {
    this.faturaState.set('edit');
    this.selectedFaturaId.set(id);
  }

  goToFaturaDetails(id: number): void {
    this.faturaState.set('details');
    this.selectedFaturaId.set(id);
  }

  // Métodos de verificação para Faturas
  isFaturaList(): boolean { return this.faturaState() === 'list'; }
  isFaturaCreate(): boolean { return this.faturaState() === 'create'; }
  isFaturaEdit(): boolean { return this.faturaState() === 'edit'; }
  isFaturaDetails(): boolean { return this.faturaState() === 'details'; }

  // ==================== VEÍCULOS ====================
  private veiculoState = signal<VeiculoUIState>('list');
  private selectedVeiculoId = signal<number | null>(null);

  readonly currentVeiculoState = this.veiculoState.asReadonly();
  readonly currentVeiculoId = this.selectedVeiculoId.asReadonly();

  setVeiculoState(state: VeiculoUIState, id?: number): void {
    this.veiculoState.set(state);
    this.selectedVeiculoId.set(id ?? null);
  }

  goToVeiculoList(): void {
    this.veiculoState.set('list');
    this.selectedVeiculoId.set(null);
  }

  goToVeiculoCreate(): void {
    this.veiculoState.set('create');
    this.selectedVeiculoId.set(null);
  }

  goToVeiculoEdit(id: number): void {
    this.veiculoState.set('edit');
    this.selectedVeiculoId.set(id);
  }

  goToVeiculoDetails(id: number): void {
    this.veiculoState.set('details');
    this.selectedVeiculoId.set(id);
  }

  // Métodos de verificação para Veículos
  isVeiculoList(): boolean { return this.veiculoState() === 'list'; }
  isVeiculoCreate(): boolean { return this.veiculoState() === 'create'; }
  isVeiculoEdit(): boolean { return this.veiculoState() === 'edit'; }
  isVeiculoDetails(): boolean { return this.veiculoState() === 'details'; }

  private fornecedorState = signal<FornecedorUIState>('list');
  private selectedFornecedorId = signal<number | null>(null);

  readonly currentFornecedorState = this.fornecedorState.asReadonly();
  readonly currentFornecedorId = this.selectedFornecedorId.asReadonly();

  goToFornecedorList(): void {
    this.fornecedorState.set('list');
    this.selectedFornecedorId.set(null);
  }

  goToFornecedorCreate(): void {
    this.fornecedorState.set('create');
    this.selectedFornecedorId.set(null);
  }

  goToFornecedorEdit(id: number): void {
    this.fornecedorState.set('edit');
    this.selectedFornecedorId.set(id);
  }

  isFornecedorList(): boolean { return this.fornecedorState() === 'list'; }
  isFornecedorCreate(): boolean { return this.fornecedorState() === 'create'; }
  isFornecedorEdit(): boolean { return this.fornecedorState() === 'edit'; }

  private recepcaoState = signal<RecepcaoUIState>('list');
  private selectedRecepcaoId = signal<number | null>(null);

  readonly currentRecepcaoState = this.recepcaoState.asReadonly();
  readonly currentRecepcaoId = this.selectedRecepcaoId.asReadonly();

  goToRecepcaoList(): void {
    this.recepcaoState.set('list');
    this.selectedRecepcaoId.set(null);
  }

  goToRecepcaoCreate(): void {
    this.recepcaoState.set('create');
    this.selectedRecepcaoId.set(null);
  }

  goToRecepcaoEdit(id: number): void {
    this.recepcaoState.set('edit');
    this.selectedRecepcaoId.set(id);
  }

  isRecepcaoList(): boolean { return this.recepcaoState() === 'list'; }
  isRecepcaoCreate(): boolean { return this.recepcaoState() === 'create'; }
  isRecepcaoEdit(): boolean { return this.recepcaoState() === 'edit'; }


  private atribuicaoState = signal<AtribuicaoUIState>('list');
  private selectedAtribuicaoId = signal<number | null>(null);

  readonly currentAtribuicaoState = this.atribuicaoState.asReadonly();
  readonly currentAtribuicaoId = this.selectedAtribuicaoId.asReadonly();

  goToAtribuicaoList(): void {
    this.atribuicaoState.set('list');
    this.selectedAtribuicaoId.set(null);
  }

  goToAtribuicaoCreate(): void {
    this.atribuicaoState.set('create');
    this.selectedAtribuicaoId.set(null);
  }

  goToAtribuicaoEdit(id: number): void {
    this.atribuicaoState.set('edit');
    this.selectedAtribuicaoId.set(id);
  }

  isAtribuicaoList(): boolean { return this.atribuicaoState() === 'list'; }
  isAtribuicaoCreate(): boolean { return this.atribuicaoState() === 'create'; }
  isAtribuicaoEdit(): boolean { return this.atribuicaoState() === 'edit'; }

  private fechoViagemState = signal<FechoViagemUIState>('list');
  private selectedFechoViagemId = signal<number | null>(null);

  readonly currentFechoViagemState = this.fechoViagemState.asReadonly();
  readonly currentFechoViagemId = this.selectedFechoViagemId.asReadonly();

  goToFechoViagemList(): void {
    this.fechoViagemState.set('list');
    this.selectedFechoViagemId.set(null);
  }

  goToFechoViagemCreate(): void {
    this.fechoViagemState.set('create');
    this.selectedFechoViagemId.set(null);
  }

  goToFechoViagemEdit(id: number): void {
    this.fechoViagemState.set('edit');
    this.selectedFechoViagemId.set(id);
  }

  isFechoViagemList(): boolean { return this.fechoViagemState() === 'list'; }
  isFechoViagemCreate(): boolean { return this.fechoViagemState() === 'create'; }
  isFechoViagemEdit(): boolean { return this.fechoViagemState() === 'edit'; }

  private gestaoViagemState = signal<GestaoViagemUIState>('list');
  private selectedGestaoViagemId = signal<number | null>(null);

  readonly currentGestaoViagemState = this.gestaoViagemState.asReadonly();
  readonly currentGestaoViagemId = this.selectedGestaoViagemId.asReadonly();

  goToGestaoViagemList(): void {
    this.gestaoViagemState.set('list');
    this.selectedGestaoViagemId.set(null);
  }

  goToGestaoViagemCreate(): void {
    this.gestaoViagemState.set('create');
    this.selectedGestaoViagemId.set(null);
  }

  goToGestaoViagemEdit(id: number): void {
    this.gestaoViagemState.set('edit');
    this.selectedGestaoViagemId.set(id);
  }

  isGestaoViagemList(): boolean { return this.gestaoViagemState() === 'list'; }
  isGestaoViagemCreate(): boolean { return this.gestaoViagemState() === 'create'; }
  isGestaoViagemEdit(): boolean { return this.gestaoViagemState() === 'edit'; }

  private incidenteState = signal<IncidenteUIState>('list');
  private selectedIncidenteId = signal<number | null>(null);

  readonly currentIncidenteState = this.incidenteState.asReadonly();
  readonly currentIncidenteId = this.selectedIncidenteId.asReadonly();

  goToIncidenteList(): void {
    this.incidenteState.set('list');
    this.selectedIncidenteId.set(null);
  }

  goToIncidenteCreate(): void {
    this.incidenteState.set('create');
    this.selectedIncidenteId.set(null);
  }

  goToIncidenteEdit(id: number): void {
    this.incidenteState.set('edit');
    this.selectedIncidenteId.set(id);
  }

  isIncidenteList(): boolean { return this.incidenteState() === 'list'; }
  isIncidenteCreate(): boolean { return this.incidenteState() === 'create'; }
  isIncidenteEdit(): boolean { return this.incidenteState() === 'edit'; }

  private guiaState = signal<GuiaUIState>('list');
  private selectedGuiaId = signal<number | null>(null);

  readonly currentGuiaState = this.guiaState.asReadonly();
  readonly currentGuiaId = this.selectedGuiaId.asReadonly();

  goToGuiaList(): void {
    this.guiaState.set('list');
    this.selectedGuiaId.set(null);
  }

  goToGuiaCreate(): void {
    this.guiaState.set('create');
    this.selectedGuiaId.set(null);
  }

  goToGuiaEdit(id: number): void {
    this.guiaState.set('edit');
    this.selectedGuiaId.set(id);
  }

  isGuiaList(): boolean { return this.guiaState() === 'list'; }
  isGuiaCreate(): boolean { return this.guiaState() === 'create'; }
  isGuiaEdit(): boolean { return this.guiaState() === 'edit'; }



}