import { Injectable, signal } from '@angular/core';

export type FaturaUIState = 'list' | 'create' | 'edit' | 'details';
export type VeiculoUIState = 'list' | 'create' | 'edit' | 'details';
export type FornecedorUIState = 'list' | 'create' | 'edit';

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

}