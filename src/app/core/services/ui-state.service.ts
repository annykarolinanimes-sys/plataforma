import { Injectable, signal } from '@angular/core';

export type FaturaUIState = 'list' | 'create' | 'edit' | 'details';

@Injectable({ providedIn: 'root' })
export class UiStateService {
  // Estado da UI para Faturas
  private faturaState = signal<FaturaUIState>('list');
  private selectedFaturaId = signal<number | null>(null);

  readonly currentState = this.faturaState.asReadonly();
  readonly currentId = this.selectedFaturaId.asReadonly();

  setState(state: FaturaUIState, id?: number): void {
    this.faturaState.set(state);
    this.selectedFaturaId.set(id ?? null);
  }

  goToList(): void {
    this.faturaState.set('list');
    this.selectedFaturaId.set(null);
  }

  goToCreate(): void {
    this.faturaState.set('create');
    this.selectedFaturaId.set(null);
  }

  goToEdit(id: number): void {
    this.faturaState.set('edit');
    this.selectedFaturaId.set(id);
  }

  goToDetails(id: number): void {
    this.faturaState.set('details');
    this.selectedFaturaId.set(id);
  }

  isList(): boolean {
    return this.faturaState() === 'list';
  }

  isCreate(): boolean {
    return this.faturaState() === 'create';
  }

  isEdit(): boolean {
    return this.faturaState() === 'edit';
  }

  isDetails(): boolean {
    return this.faturaState() === 'details';
  }
}