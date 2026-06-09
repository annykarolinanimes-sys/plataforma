import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface InventoryItem {
  id: number;
  sku: string;
  nome: string;
  descricao: string;
  warehouse: string;
  location: string;
  lotNumber: string;
  expiryDate: string;
  stockQty: number;
  reservedQty: number;
  pickingQty: number;
  minLevel: number;
  status: 'em-estoque' | 'baixo' | 'zerado' | 'vencido';
  lastMovement: string;
}

export interface MovementRecord {
  id: number;
  sku: string;
  nome: string;
  type: string;
  qty: number;
  from: string;
  to: string;
  warehouse: string;
  user: string;
  date: string;
  note: string;
}

export interface WarehouseSummary {
  warehouse: string;
  total: number;
  items: number;
}

export interface MovementRequest {
  sku: string;
  quantity: number;
  type: string;
  from: string;
  to: string;
  warehouse: string;
  note: string;
}

export interface WarehouseRequest {
  name: string;
}

@Injectable({ providedIn: 'root' })
export class WmsService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/user/warehouse`;

  getInventory(): Observable<InventoryItem[]> {
    return this.http.get<InventoryItem[]>(`${this.base}/inventory`);
  }

  getMovements(): Observable<MovementRecord[]> {
    return this.http.get<MovementRecord[]>(`${this.base}/movements`);
  }

  getWarehouses(): Observable<WarehouseSummary[]> {
    return this.http.get<WarehouseSummary[]>(`${this.base}/warehouses`);
  }

  postMovement(req: MovementRequest): Observable<{ message: string; movimentacaoId: number }> {
    return this.http.post<{ message: string; movimentacaoId: number }>(
      `${this.base}/movements`,
      req
    );
  }

  postWarehouse(req: WarehouseRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/warehouses`, req);
  }
}