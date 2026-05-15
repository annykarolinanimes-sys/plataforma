import {Component, OnInit, ChangeDetectionStrategy,inject, signal, computed, input,} from '@angular/core';
import { CommonModule } from '@angular/common';
import { catchError, of } from 'rxjs';

import { DocumentosService } from '../../../core/services/documentos.service';
import { DocumentoHistoricoItem, TipoOperacaoHistorico } from '../../documentos/documentos.models';

interface HistoricoVM extends DocumentoHistoricoItem {
  icone: string;
  cor: string;
  labelTipo: string;
}

@Component({
  selector: 'app-historico-timeline',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './historico-timeline.html',
  styleUrls: ['./historico-timeline.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HistoricoTimelineComponent implements OnInit {

  readonly documentoId    = input.required<string>();
  readonly nomeDocumento  = input<string>('');

  private readonly service = inject(DocumentosService);

  readonly isLoading  = signal(true);
  readonly errorMsg   = signal<string | null>(null);
  readonly historico  = signal<HistoricoVM[]>([]);
  readonly expandido  = signal(true);

  // Agrupamento por data para separadores visuais
  readonly historicoAgrupado = computed(() => {
    const items = this.historico();
    const grupos = new Map<string, HistoricoVM[]>();
    for (const item of items) {
      const chave = new Date(item.ocorridoEm).toLocaleDateString('pt-PT', {
        year: 'numeric', month: 'long', day: 'numeric',
      });
      if (!grupos.has(chave)) grupos.set(chave, []);
      grupos.get(chave)!.push(item);
    }
    return Array.from(grupos.entries()).map(([data, items]) => ({ data, items }));
  });

  readonly totalEventos  = computed(() => this.historico().length);
  readonly ultimoEvento  = computed(() => this.historico()[0] ?? null);

  ngOnInit(): void {
    this.carregarHistorico();
  }

  private carregarHistorico(): void {
    this.isLoading.set(true);
    this.service.obterHistorico(this.documentoId()).pipe(
      catchError(err => {
        this.errorMsg.set(err?.error?.detail ?? 'Erro ao carregar histórico.');
        return of([]);
      }),
    ).subscribe(items => {
      this.historico.set(items.map(i => this.enriquecerItem(i)));
      this.isLoading.set(false);
    });
  }

  private enriquecerItem(item: DocumentoHistoricoItem): HistoricoVM {
    const { icone, cor, label } = this.metaOperacao(item.tipoOperacao);
    return { ...item, icone, cor, labelTipo: label };
  }

  private metaOperacao(tipo: TipoOperacaoHistorico): { icone: string; cor: string; label: string } {
    const map: Record<TipoOperacaoHistorico, { icone: string; cor: string; label: string }> = {
      [TipoOperacaoHistorico.Upload]:                  { icone: 'la-cloud-upload-alt', cor: 'timeline--upload',     label: 'Upload' },
      [TipoOperacaoHistorico.Download]:                { icone: 'la-download',          cor: 'timeline--download',   label: 'Download' },
      [TipoOperacaoHistorico.Visualizacao]:            { icone: 'la-eye',               cor: 'timeline--view',       label: 'Visualização' },
      [TipoOperacaoHistorico.Validacao]:               { icone: 'la-check-double',      cor: 'timeline--validate',   label: 'Validação' },
      [TipoOperacaoHistorico.SoftDelete]:              { icone: 'la-trash-alt',          cor: 'timeline--delete',     label: 'Eliminação' },
      [TipoOperacaoHistorico.Restauro]:                { icone: 'la-undo-alt',           cor: 'timeline--restore',    label: 'Restauro' },
      [TipoOperacaoHistorico.ScanAntivirus]:           { icone: 'la-shield-alt',         cor: 'timeline--scan',       label: 'Scan Antivírus' },
      [TipoOperacaoHistorico.VerificacaoIntegridade]:  { icone: 'la-fingerprint',        cor: 'timeline--integrity',  label: 'Integridade' },
      [TipoOperacaoHistorico.TransicaoEstado]:         { icone: 'la-exchange-alt',       cor: 'timeline--transition', label: 'Transição de Estado' },
      [TipoOperacaoHistorico.Quarentena]:              { icone: 'la-lock',               cor: 'timeline--quarantine', label: 'Quarentena' },
      [TipoOperacaoHistorico.Encriptacao]:             { icone: 'la-key',                cor: 'timeline--encrypt',    label: 'Encriptação' },
      [TipoOperacaoHistorico.RetencaoLegal]:           { icone: 'la-balance-scale',      cor: 'timeline--legal',      label: 'Retenção Legal' },
      [TipoOperacaoHistorico.Arquivamento]:            { icone: 'la-archive',            cor: 'timeline--archive',    label: 'Arquivamento' },
      [TipoOperacaoHistorico.AlteracaoMetadados]:      { icone: 'la-edit',               cor: 'timeline--meta',       label: 'Metadados' },
      [TipoOperacaoHistorico.AcessoNegado]:            { icone: 'la-ban',                cor: 'timeline--denied',     label: 'Acesso Negado' },
    };
    return map[tipo] ?? { icone: 'la-circle', cor: 'timeline--generic', label: tipo };
  }

  inicialAvatar(nome: string): string {
    return nome?.charAt(0)?.toUpperCase() ?? '?';
  }

  temEstadoTransicao(item: HistoricoVM): boolean {
    return !!(item.estadoAnterior && item.estadoPosterior);
  }
}
