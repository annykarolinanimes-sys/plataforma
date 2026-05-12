import { Component, OnInit, inject, signal, computed, ViewChildren, QueryList, ElementRef, AfterViewInit, OnDestroy, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, switchMap, of, takeUntil, firstValueFrom } from 'rxjs';
import JsBarcode from 'jsbarcode';
import { EtiquetasService } from '../../core/services/etiquetas.service';

interface EtiquetaResponse {
  id: string;
  codigo: string;
  tipo: string;
  dados: {
    sequencial: number;
    entidadeId: number;
    entidadeNome: string;
  };
  url: string;
}

interface ProdutoListResponse {
  id: number;
  sku: string;
  nome: string;
  descricao?: string;
}

interface RecepcaoListResponse {
  id: number;
  numeroRecepcao: string;
  fornecedor: string;
  quantidadePendente: number;
  status?: string;
}

interface EncomendaListResponse {
  id: number;
  numeroEncomenda: string;
  clienteNome: string;
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
    this.setupEntidadeEffect();
    this.setupBarcodeEffect();
  }

  @ViewChildren('barcodeCanvas') barcodeCanvases!: QueryList<ElementRef>;

  isLoading = signal(false);
  isGenerating = signal(false);
  isPrinting = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);
  showPreview = signal(false);

  etiquetasGeradas = signal<EtiquetaResponse[]>([]);
  entidadeSelecionada = signal<EntidadeSearchResult | null>(null);
  ultimaImpressao = signal<string | null>(null);

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
      return this.Math.min(entidade.quantidadeSugerida, this.quantidadeMaxima);
    }
    return null;
  });

  readonly podeGerar = computed(() => {
    return this.form?.valid && this.entidadeSelecionada() !== null && !this.isGenerating();
  });

  readonly entidadeDisplayName = computed(() => {
    const entidade = this.entidadeSelecionada();
    if (!entidade) return 'Nenhuma';
    return `${entidade.codigo} - ${entidade.nome}`;
  });

  readonly tipos = [
    { value: 'produto', label: 'Produto', icon: 'la-boxes', description: 'Etiquetas por produto' },
    { value: 'recepcao', label: 'Recepção', icon: 'la-box', description: 'Etiquetas por recepção' },
    { value: 'encomenda', label: 'Encomenda', icon: 'la-shopping-cart', description: 'Etiquetas por encomenda' }
  ];

  readonly templates = [
    { value: 'padrao', label: 'Padrão (70x40mm)', width: 70, height: 40, class: 'padrao' },
    { value: 'pequeno', label: 'Pequeno (50x30mm)', width: 50, height: 30, class: 'pequeno' },
    { value: 'grande', label: 'Grande (100x60mm)', width: 100, height: 60, class: 'grande' },
    { value: 'custom', label: 'Personalizado', width: 0, height: 0, class: 'custom' }
  ];


  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }


  private initForm(): void {
    this.form = this.fb.group({
      tipo: ['produto', Validators.required],
      entidadeId: [null],
      entidadeInput: ['', Validators.required],
      quantidade: [1, [Validators.required, Validators.min(1), Validators.max(this.quantidadeMaxima)]],
      template: ['padrao', Validators.required],
      incluirTexto: [true],
      incluirCodigo: [true],
      larguraCustom: [70],
      alturaCustom: [40]
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
        this.resultadosBusca.set(resultados);
        this.showSearchDropdown.set(resultados.length > 0);
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

  private setupEntidadeEffect(): void {
    effect(() => {
      const sugerida = this.quantidadeSugerida();
      if (sugerida && sugerida > 0) {
        this.form.patchValue({ quantidade: sugerida });
        this.successMsg.set(`Sugeridas ${sugerida} etiquetas baseadas nos itens da recepção.`);
        setTimeout(() => this.successMsg.set(null), 3000);
      }
    });
  }

  private setupBarcodeEffect(): void {
    effect(() => {
      const etiquetas = this.etiquetasGeradas();
      if (etiquetas.length > 0 && this.barcodeCanvases && this.barcodeCanvases.length > 0) {
        setTimeout(() => {
          this.gerarCodigosBarras();
        }, 100);
      }
    });
  }


  private async buscarEntidades(term: string): Promise<EntidadeSearchResult[]> {
    const tipo = this.form.get('tipo')?.value;
    const termLower = term.toLowerCase();
    const resultados: EntidadeSearchResult[] = [];

    if (tipo === 'produto') {
      const produtos = await firstValueFrom(this.svc.obterProdutos(term));
      return (produtos || []).map(p => ({
        id: p.id,
        codigo: p.sku,
        nome: p.nome,
        tipo: 'produto'
      }));
    }

    if (tipo === 'recepcao') {
      const rececoes = await firstValueFrom(this.svc.obterRececoes()) as any[];
      return (rececoes || [])
        .filter(r =>
          r.numeroRecepcao.toLowerCase().includes(termLower) ||
          r.fornecedor.toLowerCase().includes(termLower)
        )
        .map(r => ({
          id: r.id,
          codigo: r.numeroRecepcao,
          nome: r.fornecedor,
          tipo: 'recepcao',
          quantidadeSugerida: r.quantidadePendente || 0
        }));
    }

    if (tipo === 'encomenda') {
      const encomendas = await firstValueFrom(this.svc.obterEncomendas()) as any[];
      return (encomendas || [])
        .filter(e =>
          e.numeroEncomenda.toLowerCase().includes(termLower) ||
          e.clienteNome.toLowerCase().includes(termLower)
        )
        .map(e => ({
          id: e.id,
          codigo: e.numeroEncomenda,
          nome: e.clienteNome,
          tipo: 'encomenda'
        }));
    }

    return resultados;
  }


  selecionarEntidade(entidade: EntidadeSearchResult): void {
    this.entidadeSelecionada.set(entidade);
    this.form.patchValue({
      entidadeId: entidade.id,
      entidadeInput: `${entidade.codigo} - ${entidade.nome}`
    });
    this.showSearchDropdown.set(false);
    
    if (entidade.tipo === 'recepcao' && entidade.quantidadeSugerida) {
    }
  }

  private limparSelecao(): void {
    this.entidadeSelecionada.set(null);
    this.form.patchValue({ entidadeId: null, entidadeInput: '' });
    this.resultadosBusca.set([]);
    this.showSearchDropdown.set(false);
  }


  gerarEtiquetas(): void {
    this.form.markAllAsTouched();
    
    if (this.form.invalid) {
      this.errorMsg.set('Selecione uma entidade válida.');
      return;
    }

    const quantidade = this.form.get('quantidade')?.value;
    if (quantidade > this.quantidadeMaxima) {
      this.errorMsg.set(`Máximo de ${this.quantidadeMaxima} etiquetas por geração.`);
      return;
    }

    this.isGenerating.set(true);
    this.errorMsg.set(null);
    this.etiquetasGeradas.set([]);

    const entidade = this.entidadeSelecionada();
    if (!entidade) {
      this.errorMsg.set('Entidade não encontrada.');
      this.isGenerating.set(false);
      return;
    }

    const codigoBase = entidade.codigo;
    const tipo = this.form.get('tipo')?.value;
    const quantidadeValue = this.form.get('quantidade')?.value || 1;
    
    const etiquetas: EtiquetaResponse[] = [];
    for (let i = 0; i < quantidadeValue; i++) {
      const codigo = `${codigoBase}-${(i + 1).toString().padStart(3, '0')}`;
      etiquetas.push({
        id: `${Date.now()}-${i}`,
        codigo: codigo,
        tipo: tipo,
        dados: { 
          sequencial: i + 1,
          entidadeId: entidade.id,
          entidadeNome: entidade.nome
        },
        url: ''
      });
    }
    
    this.etiquetasGeradas.set(etiquetas);
    this.showPreview.set(true);
    this.isGenerating.set(false);
    
    this.successMsg.set(`${etiquetas.length} etiqueta(s) gerada(s) com sucesso!`);
    setTimeout(() => this.successMsg.set(null), 3000);
  }

  gerarCodigosBarras(): void {
    if (!this.barcodeCanvases || this.barcodeCanvases.length === 0) return;
    
    const template = this.form.get('template')?.value;
    const barHeight = template === 'pequeno' ? 30 : template === 'grande' ? 50 : 40;
    const barWidth = template === 'pequeno' ? 1.2 : template === 'grande' ? 2 : 1.5;
    
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


  async imprimir(): Promise<void> {
    if (this.etiquetasGeradas().length === 0) return;
    
    this.isPrinting.set(true);
    
    try {
      const template = this.form.get('template')?.value;
      const templateConfig = this.templates.find(t => t.value === template);
      const incluirTexto = this.form.get('incluirTexto')?.value;
      const incluirCodigo = this.form.get('incluirCodigo')?.value;
      const entidade = this.entidadeSelecionada();
      
      const widthMm = templateConfig?.width || 70;
      const heightMm = templateConfig?.height || 40;
      
      const printWindow = window.open('', '_blank');
      if (!printWindow) {
        this.errorMsg.set('Não foi possível abrir a janela de impressão. Verifique se o popup está bloqueado.');
        this.isPrinting.set(false);
        return;
      }
      
      const htmlContent = this.gerarHtmlImpressao(templateConfig, widthMm, heightMm, incluirTexto, incluirCodigo, entidade);
      printWindow.document.write(htmlContent);
      printWindow.document.close();
      
      await new Promise(resolve => setTimeout(resolve, 500));
      printWindow.print();
      
      this.ultimaImpressao.set(new Date().toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' }));
      this.successMsg.set('Etiquetas enviadas para impressão!');
      setTimeout(() => this.successMsg.set(null), 3000);
    } catch (error) {
      console.error('Erro na impressão:', error);
      this.errorMsg.set('Erro ao imprimir etiquetas.');
    } finally {
      this.isPrinting.set(false);
    }
  }

  private gerarHtmlImpressao(
    templateConfig: any, 
    widthMm: number, 
    heightMm: number, 
    incluirTexto: boolean, 
    incluirCodigo: boolean,
    entidade: EntidadeSearchResult | null
  ): string {
    const barHeight = templateConfig?.value === 'pequeno' ? 30 : templateConfig?.value === 'grande' ? 50 : 40;
    let htmlContent = `
      <!DOCTYPE html>
      <html>
      <head>
        <title>Impressão de Etiquetas</title>
        <meta charset="UTF-8">
        <style>
          * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
          }
          
          body {
            font-family: 'IBM Plex Sans', system-ui, Arial, sans-serif;
            background: white;
            padding: 0;
            margin: 0;
          }
          
          /* Container das etiquetas */
          .etiquetas-container {
            display: flex;
            flex-wrap: wrap;
            gap: 8px;
            justify-content: flex-start;
            padding: 4px;
          }
          
          /* Cada etiqueta com dimensões exatas em mm */
          .etiqueta {
            border: 1px solid #e2e8f0;
            border-radius: 4px;
            padding: 6px;
            text-align: center;
            background: white;
            width: ${widthMm}mm;
            min-height: ${heightMm}mm;
            page-break-inside: avoid;
            box-shadow: 0 1px 2px rgba(0,0,0,0.05);
          }
          
          .barcode-container {
            margin: 4px 0;
            display: flex;
            justify-content: center;
            align-items: center;
          }
          
          .barcode-container canvas,
          .barcode-container img {
            max-width: 100%;
            height: auto;
          }
          
          .codigo {
            font-family: 'IBM Plex Mono', monospace;
            font-size: ${templateConfig?.value === 'pequeno' ? '9px' : '11px'};
            font-weight: 600;
            color: #0f172a;
            letter-spacing: 0.5px;
            margin-top: 6px;
            word-break: break-all;
          }
          
          .texto {
            font-size: ${templateConfig?.value === 'pequeno' ? '7px' : '9px'};
            color: #475569;
            margin-top: 4px;
            line-height: 1.3;
          }
          
          /* Esconder elementos de UI durante a impressão */
          @media print {
            body {
              padding: 0;
              margin: 0;
            }
            
            .etiqueta {
              break-inside: avoid;
              box-shadow: none;
              border-color: #cbd5e1;
            }
            
            /* Garantir que não há margens extras */
            @page {
              size: ${widthMm}mm ${heightMm}mm;
              margin: 2mm;
            }
          }
        </style>
      </head>
      <body>
        <div class="etiquetas-container">
    `;
    
    for (const etiqueta of this.etiquetasGeradas()) {
      const canvasTemp = document.createElement('canvas');
      JsBarcode(canvasTemp, etiqueta.codigo, {
        format: 'CODE128',
        width: templateConfig?.value === 'pequeno' ? 1.2 : templateConfig?.value === 'grande' ? 2 : 1.5,
        height: barHeight,
        displayValue: false,
        margin: 0
      });
      const barcodeDataUrl = canvasTemp.toDataURL('image/png');
      
      const texto = entidade ? `${entidade.codigo} - ${entidade.nome}` : '';
      
      htmlContent += `
        <div class="etiqueta">
          <div class="barcode-container">
            <img src="${barcodeDataUrl}" alt="Código de Barras" style="max-width: 100%;" />
          </div>
          ${incluirCodigo ? `<div class="codigo">${etiqueta.codigo}</div>` : ''}
          ${incluirTexto && texto ? `<div class="texto">${this.escapeHtml(texto)}</div>` : ''}
        </div>
      `;
    }
    
    htmlContent += `
        </div>
      </body>
      </html>
    `;
    
    return htmlContent;
  }

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }


  getTextoParaEtiqueta(): string {
    const entidade = this.entidadeSelecionada();
    if (!entidade) return '';
    return `${entidade.codigo} - ${entidade.nome}`;
  }

  getTemplateClass(): string {
    const template = this.form.get('template')?.value;
    const config = this.templates.find(t => t.value === template);
    return config?.class || 'padrao';
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

  fecharPreview(): void {
    this.showPreview.set(false);
  }

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
}