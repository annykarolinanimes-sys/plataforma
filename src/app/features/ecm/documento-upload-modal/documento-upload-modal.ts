import {Component, ChangeDetectionStrategy,inject, signal, computed, output, input, OnDestroy,} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { catchError, of, Subscription } from 'rxjs';

import { DocumentosService } from '../../../core/services/documentos.service';
import {CategoriaDocumento, ContextoDocumento, DocumentoResumo, UploadProgress,} from '../../documentos/documentos.models';

const MIME_PERMITIDOS = [
  'application/pdf',
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  'application/vnd.openxmlformats-officedocument.presentationml.presentation',
  'image/jpeg', 'image/png', 'image/gif', 'image/webp', 'image/tiff',
  'text/plain', 'text/csv',
];
const TAMANHO_MAXIMO_BYTES = 50 * 1024 * 1024; // 50 MB

@Component({
  selector: 'app-documento-upload-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './documento-upload-modal.html',
  styleUrls: ['./documento-upload-modal.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentoUploadModalComponent implements OnDestroy {


  readonly novaVersaoDocumento = input<DocumentoResumo | null>(null);

  readonly uploadConcluido = output<string>();     
  readonly fechar          = output<void>();


  private readonly service = inject(DocumentosService);
  private uploadSub?: Subscription;


  readonly dragOver    = signal(false);
  readonly ficheiroSel = signal<File | null>(null);
  readonly progresso   = signal<UploadProgress | null>(null);
  readonly errorMsg    = signal<string | null>(null);
  readonly enviando    = signal(false);


  readonly categoria   = signal<CategoriaDocumento>(CategoriaDocumento.Outro);
  readonly contexto    = signal<ContextoDocumento>(ContextoDocumento.Interno);
  readonly descricao   = signal('');


  readonly isNovaVersao = computed(() => !!this.novaVersaoDocumento());

  readonly tituloModal  = computed(() =>
    this.isNovaVersao()
      ? `Nova Versão — ${this.novaVersaoDocumento()!.nomeOriginal}`
      : 'Submeter Documento');

  readonly ficheiroValido = computed(() => {
    const f = this.ficheiroSel();
    if (!f) return false;
    if (!MIME_PERMITIDOS.includes(f.type)) return false;
    if (f.size > TAMANHO_MAXIMO_BYTES) return false;
    return true;
  });

  readonly podeSometer = computed(() =>
    this.ficheiroValido() && !this.enviando() && !this.progresso()?.estado.includes('concluido'));

  readonly ficheiroFormatado = computed(() => {
    const f = this.ficheiroSel();
    if (!f) return null;
    return {
      nome: f.name,
      tamanho: formatarBytes(f.size),
      tipo: f.type || 'desconhecido',
      valido: this.ficheiroValido(),
      erroMime: !!f.type && !MIME_PERMITIDOS.includes(f.type),
      erroTamanho: f.size > TAMANHO_MAXIMO_BYTES,
    };
  });

  readonly categoriaOpcoes = Object.values(CategoriaDocumento);
  readonly contextoOpcoes  = Object.values(ContextoDocumento);

  // ─── Drag & Drop ──────────────────────────────────────────────────────────

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.dragOver.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.dragOver.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.dragOver.set(false);

    const files = event.dataTransfer?.files;
    if (files?.length) this.processarFicheiro(files[0]);
  }

  onFileInputChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) this.processarFicheiro(input.files[0]);
  }

  private processarFicheiro(ficheiro: File): void {
    this.ficheiroSel.set(ficheiro);
    this.progresso.set(null);
    this.errorMsg.set(null);
  }

  removerFicheiro(): void {
    this.ficheiroSel.set(null);
    this.progresso.set(null);
    this.errorMsg.set(null);
  }

  // ─── Submissão ────────────────────────────────────────────────────────────

  submeter(): void {
    const ficheiro = this.ficheiroSel();
    if (!ficheiro || !this.podeSometer()) return;

    this.enviando.set(true);
    this.errorMsg.set(null);

    const upload$ = this.isNovaVersao()
      ? this.service.novaVersao(
          this.novaVersaoDocumento()!.id,
          ficheiro,
          this.descricao() || undefined)
      : this.service.upload(
          ficheiro,
          this.categoria(),
          this.contexto(),
          this.descricao() || undefined);

    this.uploadSub = upload$.pipe(
      catchError(err => {
        const msg = err?.error?.detail ?? `Erro ao submeter '${ficheiro.name}'.`;
        this.errorMsg.set(msg);
        this.enviando.set(false);
        this.progresso.set(null);
        return of({ ficheiro, progresso: 0, estado: 'erro', mensagem: msg } as UploadProgress);
      }),
    ).subscribe(estado => {
      this.progresso.set(estado);

      if (estado.estado === 'concluido') {
        this.enviando.set(false);
        setTimeout(() => {
          if (estado.documentoId) this.uploadConcluido.emit(estado.documentoId);
        }, 1200);
      }

      if (estado.estado === 'erro') {
        this.enviando.set(false);
      }
    });
  }

  // ─── Helpers visuais ──────────────────────────────────────────────────────

  labelCategoria(c: CategoriaDocumento): string {
    const map: Record<CategoriaDocumento, string> = {
      [CategoriaDocumento.Contrato]:              'Contrato',
      [CategoriaDocumento.Fatura]:                'Fatura',
      [CategoriaDocumento.Proposta]:              'Proposta',
      [CategoriaDocumento.Relatorio]:             'Relatório',
      [CategoriaDocumento.Comprovativo]:          'Comprovativo',
      [CategoriaDocumento.IdentificacaoPessoal]:  'ID Pessoal',
      [CategoriaDocumento.CertificadoCompliance]: 'Compliance',
      [CategoriaDocumento.AuditoriaInterna]:      'Auditoria Interna',
      [CategoriaDocumento.CorrespondenciaLegal]:  'Correspondência Legal',
      [CategoriaDocumento.ManualProcedimento]:    'Manual/Procedimento',
      [CategoriaDocumento.Outro]:                 'Outro',
    };
    return map[c] ?? c;
  }

  labelContexto(c: ContextoDocumento): string {
    const map: Record<ContextoDocumento, string> = {
      [ContextoDocumento.Cliente]:     'Cliente',
      [ContextoDocumento.Fornecedor]:  'Fornecedor',
      [ContextoDocumento.Colaborador]: 'Colaborador',
      [ContextoDocumento.Interno]:     'Interno',
      [ContextoDocumento.Regulatorio]: 'Regulatório',
      [ContextoDocumento.Juridico]:    'Jurídico',
    };
    return map[c] ?? c;
  }

  iconeEstadoUpload(): string {
    const p = this.progresso();
    if (!p) return '';
    if (p.estado === 'concluido') return 'la-check-circle';
    if (p.estado === 'erro')      return 'la-exclamation-circle';
    return 'la-cloud-upload-alt';
  }

  fecharModal(): void {
    if (this.enviando()) return;
    this.fechar.emit();
  }

  ngOnDestroy(): void {
    this.uploadSub?.unsubscribe();
  }
}

// ─── Helper ───────────────────────────────────────────────────────────────────
function formatarBytes(bytes: number): string {
  if (bytes < 1024)           return `${bytes} B`;
  if (bytes < 1024 * 1024)    return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
