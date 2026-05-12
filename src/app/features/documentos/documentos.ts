import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { DocumentosService, Documento } from '../../core/services/documentos.service';

@Component({
  selector: 'app-documentos',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './documentos.html',
  styleUrls: ['./documentos.css'],
})
export class DocumentosComponent implements OnInit {
  private documentosService = inject(DocumentosService);

  documentos = signal<Documento[]>([]);
  isLoading = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  filtroTipo = '';
  filtroSearch = '';

  ngOnInit(): void {
    this.carregarDocumentos();
  }

  carregarDocumentos(): void {
    this.isLoading.set(true);
    this.documentosService.listar(this.filtroTipo || undefined, this.filtroSearch || undefined)
      .subscribe({
        next: (data: Documento[]) => {
          this.documentos.set(data);
          this.isLoading.set(false);
        },
        error: (err: any) => {
          this.errorMsg.set(err?.error?.message || 'Erro ao carregar documentos');
          this.isLoading.set(false);
        }
      });
  }

  baixarDocumento(doc: Documento): void {
    this.documentosService.baixar(doc.id).subscribe({
      next: (blob: Blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${doc.nome}.pdf`;
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: (err: any) => {
        this.errorMsg.set(err?.error?.message || 'Erro ao baixar documento');
      }
    });
  }

  clearError(): void {
    this.errorMsg.set(null);
  }
}

