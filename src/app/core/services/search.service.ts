import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class SearchService {
  readonly query = signal('');

  setQuery(value: string): void {
    this.query.set(value?.trim() ?? '');
  }

  clear(): void {
    this.query.set('');
  }
}
