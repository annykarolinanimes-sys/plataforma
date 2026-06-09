import { Component, OnInit, inject, signal, computed, ViewChildren, QueryList, ElementRef, OnDestroy, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, switchMap, of, takeUntil, firstValueFrom } from 'rxjs';
import JsBarcode from 'jsbarcode';
import { jsPDF } from 'jspdf';
import { EtiquetasService } from '../../core/services/etiquetas.service';

interface EtiquetaGerada {
  id: string;
  codigo: string;
  tipo: string;
  dados: { sequencial: number; entidadeId: number; entidadeNome: string };
  url: string;
}

interface HistoricoItem {
  id: string;
  data: string;
  tipo: string;
  entidadeCodigo: string;
  entidadeNome: string;
  quantidade: number;
  template: string;
}

interface EntidadeSearchResult {
  id: number;
  codigo: string;
  nome: string;
  tipo: 'produto' | 'recepcao' | 'encomenda';
  quantidadeSugerida?: number;
}

@Component({
  selector: 'app-etiquetas',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterModule],
  templateUrl: './etiquetas.component.html',
  styleUrls: ['./etiquetas.component.css']
})
export class EtiquetasComponent implements OnDestroy {
  private readonly svc = inject(EtiquetasService);
  private readonly fb = inject(FormBuilder);
  private readonly destroy$ = new Subject<void>();
  private searchSubject = new Subject<string>();

  constructor() {
    this.initForm();
    this.setupSearchListener();
    this.setupFormListeners();
    this.carregarHistorico();
  }

  @ViewChildren('barcodeCanvas') barcodeCanvases!: QueryList<ElementRef>;

  isLoading = signal(false);
  isGenerating = signal(false);
  isPrinting = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);
  showPreview = signal(false);
  showHistorico = signal(false);

  etiquetasGeradas = signal<EtiquetaGerada[]>([]);
  entidadeSelecionada = signal<EntidadeSearchResult | null>(null);
  ultimaImpressao = signal<string | null>(null);
  historico = signal<HistoricoItem[]>([]);

  searchTerm = signal('');
  showSearchDropdown = signal(false);
  isLoadingSearch = signal(false);
  resultadosBusca = signal<EntidadeSearchResult[]>([]);

  form!: FormGroup;

  readonly etiquetasFilaCount = computed(() => this.etiquetasGeradas().length);
  readonly quantidadeMaxima = 500;
  readonly Math = Math;

  readonly quantidadeSugerida = computed(() => {
    const entidade = this.entidadeSelecionada();
    if (entidade?.tipo === 'recepcao' && entidade.quantidadeSugerida) {
      return Math.min(entidade.quantidadeSugerida, this.quantidadeMaxima);
    }
    return null;
  });

  readonly podeGerar = computed(() => {
    const entidade = this.entidadeSelecionada();
    const qtd = this.form?.get('quantidade')?.value ?? 0;
    return entidade !== null && qtd >= 1 && !this.isGenerating();
  });

  readonly entidadeDisplayName = computed(() => {
    const e = this.entidadeSelecionada();
    if (!e) return 'Nenhuma';
    return `${e.codigo} - ${e.nome}`;
  });

  readonly tipos = [
    { value: 'produto', label: 'Produto', icon: 'la-boxes', description: 'Etiquetas por produto' },
    { value: 'recepcao', label: 'Receção', icon: 'la-box', description: 'Etiquetas por receção' }
  ];

  readonly templates = [
    { value: 'padrao', label: 'Padrão (70x40mm)', width: 70, height: 40, class: 'padrao' },
    { value: 'pequeno', label: 'Pequeno (50x30mm)', width: 50, height: 30, class: 'pequeno' },
    { value: 'grande', label: 'Grande (100x60mm)', width: 100, height: 60, class: 'grande' },
  ];

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initForm(): void {
    this.form = this.fb.group({
      tipo: ['produto', Validators.required],
      entidadeId: [null],
      entidadeInput: [''],
      quantidade: [1, [Validators.required, Validators.min(1), Validators.max(this.quantidadeMaxima)]],
      template: ['padrao', Validators.required],
      incluirTexto: [true],
      incluirCodigo: [true],
    });
  }

  private setupSearchListener(): void {
    this.searchSubject
      .pipe(
        debounceTime(400),
        distinctUntilChanged(),
        switchMap(term => {
          if (!term || term.length < 2) {
            this.resultadosBusca.set([]);
            this.showSearchDropdown.set(false);
            return of([]);
          }
          this.isLoadingSearch.set(true);
          return this.buscarEntidades(term);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe(resultados => {
        this.resultadosBusca.set(resultados as EntidadeSearchResult[]);
        this.showSearchDropdown.set((resultados as EntidadeSearchResult[]).length > 0);
        this.isLoadingSearch.set(false);
      });
  }

  private setupFormListeners(): void {
    this.form.get('tipo')?.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.limparSelecao();
    });

    this.form.get('entidadeInput')?.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(value => {
      this.searchTerm.set(value);
      if (value && value.length >= 2) {
        this.searchSubject.next(value);
      } else {
        this.resultadosBusca.set([]);
        this.showSearchDropdown.set(false);
      }
    });
  }

  private async buscarEntidades(term: string): Promise<EntidadeSearchResult[]> {
    const tipo = this.form.get('tipo')?.value;
    const termLower = term.toLowerCase();

    if (tipo === 'produto') {
      const produtos = await firstValueFrom(this.svc.obterProdutos(term));
      return (produtos || []).map(p => ({
        id: p.id, codigo: p.sku, nome: p.nome, tipo: 'produto' as const
      }));
    }

    if (tipo === 'recepcao') {
      const rececoes = await firstValueFrom(this.svc.obterRececoes()) as any[];
      return (rececoes || [])
        .filter((r: any) =>
          r.numeroRecepcao?.toLowerCase().includes(termLower) ||
          r.fornecedor?.toLowerCase().includes(termLower)
        )
        .map((r: any) => ({
          id: r.id, codigo: r.numeroRecepcao, nome: r.fornecedor,
          tipo: 'recepcao' as const, quantidadeSugerida: r.quantidadePendente || 0
        }));
    }

    if (tipo === 'encomenda') {
      const encomendas = await firstValueFrom(this.svc.obterEncomendas()) as any[];
      return (encomendas || [])
        .filter((e: any) =>
          e.numeroEncomenda?.toLowerCase().includes(termLower) ||
          e.clienteNome?.toLowerCase().includes(termLower)
        )
        .map((e: any) => ({
          id: e.id, codigo: e.numeroEncomenda, nome: e.clienteNome, tipo: 'encomenda' as const
        }));
    }
    return [];
  }

  selecionarEntidade(entidade: EntidadeSearchResult): void {
    this.entidadeSelecionada.set(entidade);
    this.form.patchValue({
      entidadeId: entidade.id,
      entidadeInput: `${entidade.codigo} - ${entidade.nome}`
    });
    this.showSearchDropdown.set(false);

    if (entidade.tipo === 'recepcao' && entidade.quantidadeSugerida && entidade.quantidadeSugerida > 0) {
      const sugerida = Math.min(entidade.quantidadeSugerida, this.quantidadeMaxima);
      this.form.patchValue({ quantidade: sugerida });
      this.successMsg.set(`Sugeridas ${sugerida} etiquetas baseadas nos itens da receção.`);
      setTimeout(() => this.successMsg.set(null), 3000);
    }
  }

  private limparSelecao(): void {
    this.entidadeSelecionada.set(null);
    this.form.patchValue({ entidadeId: null, entidadeInput: '' });
    this.resultadosBusca.set([]);
    this.showSearchDropdown.set(false);
  }

  gerarEtiquetas(): void {
    const entidade = this.entidadeSelecionada();
    if (!entidade) {
      this.errorMsg.set('Selecione uma entidade válida.');
      return;
    }

    const quantidade = this.form.get('quantidade')?.value;
    if (!quantidade || quantidade < 1) {
      this.errorMsg.set('Quantidade deve ser pelo menos 1.');
      return;
    }
    if (quantidade > this.quantidadeMaxima) {
      this.errorMsg.set(`Máximo de ${this.quantidadeMaxima} etiquetas por geração.`);
      return;
    }

    this.isGenerating.set(true);
    this.errorMsg.set(null);

    const tipo = this.form.get('tipo')?.value;
    const codigoBase = entidade.codigo;
    const etiquetas: EtiquetaGerada[] = [];

    for (let i = 0; i < quantidade; i++) {
      const codigo = `${codigoBase}-${(i + 1).toString().padStart(3, '0')}`;
      etiquetas.push({
        id: `${Date.now()}-${i}`,
        codigo,
        tipo,
        dados: { sequencial: i + 1, entidadeId: entidade.id, entidadeNome: entidade.nome },
        url: ''
      });
    }

    this.etiquetasGeradas.set(etiquetas);
    this.showPreview.set(true);
    this.isGenerating.set(false);

    this.adicionarAoHistorico(entidade, quantidade);

    this.successMsg.set(`${etiquetas.length} etiqueta(s) gerada(s) com sucesso!`);
    setTimeout(() => this.successMsg.set(null), 3000);

    setTimeout(() => this.gerarCodigosBarras(), 200);
  }

  gerarCodigosBarras(): void {
    if (!this.barcodeCanvases || this.barcodeCanvases.length === 0) return;
    const template = this.form.get('template')?.value;
    const barHeight = template === 'pequeno' ? 30 : template === 'grande' ? 50 : 40;
    const barWidth  = template === 'pequeno' ? 1.2 : template === 'grande' ? 2 : 1.5;

    this.barcodeCanvases.forEach((canvas: ElementRef, index: number) => {
      const codigo = this.etiquetasGeradas()[index]?.codigo;
      if (codigo && canvas?.nativeElement) {
        try {
          JsBarcode(canvas.nativeElement, codigo, {
            format: 'CODE128',
            width: barWidth,
            height: barHeight,
            displayValue: true,
            fontSize: template === 'pequeno' ? 10 : 12,
            margin: template === 'pequeno' ? 2 : 5
          });
        } catch (err) {
          console.error('Erro ao gerar código de barras:', err);
        }
      }
    });
  }

  private createBarcodeCanvas(codigo: string, template: string): HTMLCanvasElement {
    const barHeight = template === 'pequeno' ? 30 : template === 'grande' ? 50 : 40;
    const barWidth  = template === 'pequeno' ? 1.2 : template === 'grande' ? 2 : 1.5;
    const canvas = document.createElement('canvas');
    canvas.width = 400;
    canvas.height = 100;

    try {
      JsBarcode(canvas, codigo, {
        format: 'CODE128',
        width: barWidth,
        height: barHeight,
        displayValue: false,
        margin: 0
      });
    } catch (error) {
      console.error('Erro ao gerar o barcode para PDF/impressão:', error);
    }

    return canvas;
  }

  private buildPrintHtml(): string {
    const template = this.form.get('template')?.value;
    const templateCfg = this.templates.find(t => t.value === template);
    const incluirTexto  = this.form.get('incluirTexto')?.value;
    const incluirCodigo = this.form.get('incluirCodigo')?.value;
    const entidade = this.entidadeSelecionada();
    const widthMm  = templateCfg?.width ?? 70;
    const heightMm = templateCfg?.height ?? 40;
    const texto = entidade ? `${entidade.codigo} - ${entidade.nome}` : '';

    let html = `<!DOCTYPE html><html><head><meta charset="UTF-8">
      <title>Etiquetas</title>
      <style>
        * { margin:0; padding:0; box-sizing:border-box; }
        body { font-family:'IBM Plex Sans',Arial,sans-serif; background:white; }
        .etiquetas-container { display:flex; flex-wrap:wrap; gap:8px; padding:4px; }
        .etiqueta {
          border:1px solid #e2e8f0; border-radius:4px; padding:6px;
          text-align:center; background:white;
          width:${widthMm}mm; min-height:${heightMm}mm;
          page-break-inside:avoid;
        }
        .barcode-container { margin:4px 0; display:flex; justify-content:center; }
        .barcode-container img { max-width:100%; height:auto; }
        .codigo { font-family:monospace; font-size:${template === 'pequeno' ? '9px':'11px'}; font-weight:600; color:#0f172a; margin-top:4px; }
        .texto { font-size:${template === 'pequeno' ? '7px':'9px'}; color:#475569; margin-top:2px; }
        @page { size:${widthMm}mm ${heightMm}mm; margin:2mm; }
        @media print { .etiqueta { box-shadow:none; border-color:#cbd5e1; } }
      </style></head><body><div class="etiquetas-container">`;

    for (const etiqueta of this.etiquetasGeradas()) {
      const canvas = this.createBarcodeCanvas(etiqueta.codigo, template);
      const barcodeDataUrl = canvas.toDataURL('image/png');

      html += `<div class="etiqueta">
        <div class="barcode-container"><img src="${barcodeDataUrl}" alt="Barcode"/></div>
        ${incluirCodigo ? `<div class="codigo">${etiqueta.codigo}</div>` : ''}
        ${incluirTexto && texto ? `<div class="texto">${this.escapeHtml(texto)}</div>` : ''}
      </div>`;
    }

    html += `</div></body></html>`;
    return html;
  }

  async imprimir(): Promise<void> {
    if (this.etiquetasGeradas().length === 0) return;
    this.isPrinting.set(true);

    try {
      const printWindow = window.open('', '_blank');
      if (!printWindow) {
        this.errorMsg.set('Não foi possível abrir janela de impressão.');
        return;
      }

      printWindow.document.write(this.buildPrintHtml());
      printWindow.document.close();
      printWindow.focus();
      printWindow.print();
      printWindow.addEventListener('afterprint', () => printWindow.close());

      this.ultimaImpressao.set(new Date().toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' }));
      this.successMsg.set('Etiqueta enviada para impressão!');
      setTimeout(() => this.successMsg.set(null), 3000);
    } catch (error) {
      console.error('Erro ao abrir impressão:', error);
      this.errorMsg.set('Erro ao abrir diálogo de impressão.');
    } finally {
      this.isPrinting.set(false);
    }
  }

  async gerarPDF(): Promise<void> {
    if (this.etiquetasGeradas().length === 0) return;
    this.isPrinting.set(true);

    try {
      const template = this.form.get('template')?.value;
      const templateCfg = this.templates.find(t => t.value === template);
      const incluirTexto = this.form.get('incluirTexto')?.value;
      const incluirCodigo = this.form.get('incluirCodigo')?.value;
      const entidade = this.entidadeSelecionada();
      const entidadeTexto = entidade ? `${entidade.codigo} - ${entidade.nome}` : '';
      const widthMm = templateCfg?.width ?? 70;
      const heightMm = templateCfg?.height ?? 40;

      const pdf = new jsPDF({ unit: 'mm', format: 'a4', orientation: 'portrait' });
      const pageWidth = pdf.internal.pageSize.getWidth();
      const pageHeight = pdf.internal.pageSize.getHeight();
      const margin = 10;
      const maxCols = Math.max(1, Math.floor((pageWidth - margin * 2) / widthMm));
      const maxRows = Math.max(1, Math.floor((pageHeight - margin * 2) / heightMm));
      const maxPerPage = Math.max(1, maxCols * maxRows);

      let x = margin;
      let y = margin;

      for (let index = 0; index < this.etiquetasGeradas().length; index++) {
        if (index > 0 && index % maxPerPage === 0) {
          pdf.addPage();
          x = margin;
          y = margin;
        }

        const etiqueta = this.etiquetasGeradas()[index];
        const canvas = this.createBarcodeCanvas(etiqueta.codigo, template);
        const barcodeDataUrl = canvas.toDataURL('image/png');

        pdf.addImage(barcodeDataUrl, 'PNG', x, y, widthMm - 4, heightMm - 12);

        if (incluirCodigo) {
          pdf.setFontSize(7);
          pdf.text(etiqueta.codigo, x + 1, y + heightMm - 8, { maxWidth: widthMm - 5 });
        }

        if (incluirTexto && entidadeTexto) {
          pdf.setFontSize(6);
          pdf.text(entidadeTexto, x + 1, y + heightMm - 3, { maxWidth: widthMm - 5 });
        }

        x += widthMm;
        if (x + widthMm > pageWidth - margin + 0.01) {
          x = margin;
          y += heightMm;
        }
      }

      pdf.save('etiquetas.pdf');
      this.successMsg.set('PDF gerado com sucesso!');
      setTimeout(() => this.successMsg.set(null), 3000);
    } catch (error) {
      console.error('Erro ao gerar PDF:', error);
      this.errorMsg.set('Erro ao gerar PDF.');
    } finally {
      this.isPrinting.set(false);
    }
  }

  private adicionarAoHistorico(entidade: EntidadeSearchResult, quantidade: number): void {
    const item: HistoricoItem = {
      id: Date.now().toString(),
      data: new Date().toLocaleString('pt-PT'),
      tipo: this.form.get('tipo')?.value,
      entidadeCodigo: entidade.codigo,
      entidadeNome: entidade.nome,
      quantidade,
      template: this.form.get('template')?.value
    };

    const atual = this.historico();
    const novo = [item, ...atual].slice(0, 50); 
    this.historico.set(novo);

    try {
      localStorage.setItem('etiquetas_historico', JSON.stringify(novo));
    } catch (e) {
      console.warn('Could not save history to localStorage');
    }
  }

  private carregarHistorico(): void {
    try {
      const saved = localStorage.getItem('etiquetas_historico');
      if (saved) {
        this.historico.set(JSON.parse(saved));
      }
    } catch (e) {
      console.warn('Could not load history from localStorage');
    }
  }

  limparHistorico(): void {
    this.historico.set([]);
    try { localStorage.removeItem('etiquetas_historico'); } catch(e) {}
  }

  getTextoParaEtiqueta(): string {
    const e = this.entidadeSelecionada();
    if (!e) return '';
    return `${e.codigo} - ${e.nome}`;
  }

  getTemplateClass(): string {
    const template = this.form.get('template')?.value;
    return this.templates.find(t => t.value === template)?.class ?? 'padrao';
  }

  limparFila(): void {
    this.etiquetasGeradas.set([]);
    this.showPreview.set(false);
    this.successMsg.set('Fila de etiquetas limpa.');
    setTimeout(() => this.successMsg.set(null), 2000);
  }

  limpar(): void {
    this.etiquetasGeradas.set([]);
    this.showPreview.set(false);
    this.limparSelecao();
    this.form.patchValue({ quantidade: 1 });
    this.errorMsg.set(null);
    this.successMsg.set(null);
  }

  fecharPreview(): void { this.showPreview.set(false); }

  hasError(fieldName: string, errorType?: string): boolean {
    const control = this.form.get(fieldName);
    if (!control) return false;
    if (errorType) return control.touched && control.hasError(errorType);
    return control.touched && control.invalid;
  }

  onQuantidadeChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    let value = parseInt(input.value, 10);
    if (isNaN(value)) value = 1;
    if (value > this.quantidadeMaxima) {
      this.errorMsg.set(`Máximo de ${this.quantidadeMaxima} etiquetas por geração.`);
      this.form.patchValue({ quantidade: this.quantidadeMaxima });
      setTimeout(() => this.errorMsg.set(null), 3000);
    } else if (value < 1) {
      this.form.patchValue({ quantidade: 1 });
    }
  }

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }
}