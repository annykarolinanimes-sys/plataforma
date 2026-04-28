import { Component, OnInit, inject, signal, ViewChildren, QueryList, ElementRef, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import JsBarcode from 'jsbarcode';
import { EtiquetasService, EtiquetaResponse } from '../../core/services/etiquetas.service';

@Component({
  selector: 'app-etiquetas',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './etiquetas.component.html',
  styleUrls: ['./etiquetas.component.css']
})
export class EtiquetasComponent implements OnInit, AfterViewInit {
  private readonly svc = inject(EtiquetasService);
  private readonly fb = inject(FormBuilder);

  @ViewChildren('barcodeCanvas') barcodeCanvases!: QueryList<ElementRef>;

  // Estado
  isLoading = signal(false);
  isGenerating = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  // Formulário
  form!: FormGroup;

  // Resultados
  etiquetasGeradas = signal<EtiquetaResponse[]>([]);
  showPreview = signal(false);

  // Listas para selects
  produtos = signal<{ id: number; sku: string; nome: string }[]>([]);
  rececoes = signal<{ id: number; numeroRecepcao: string; fornecedor: string }[]>([]);
  encomendas = signal<{ id: number; numeroEncomenda: string; clienteNome: string }[]>([]);
  paletes = signal<{ id: number; codigo: string; localizacao: string }[]>([]);

  readonly tipos = [
    { value: 'produto', label: 'Produto', icon: 'la-boxes' },
    { value: 'recepcao', label: 'Recepção', icon: 'la-box' },
    { value: 'encomenda', label: 'Encomenda', icon: 'la-shopping-cart' },
    { value: 'palete', label: 'Palete / Contentor', icon: 'la-pallet' }
  ];

  readonly formatos = [
    { value: 'barcode', label: 'Código de Barras', icon: 'la-barcode' },
    { value: 'qrcode', label: 'QR Code', icon: 'la-qrcode' },
    { value: 'ambos', label: 'Ambos', icon: 'la-layer-group' }
  ];

  readonly opcoesImpressao = [
    { value: 'padrao', label: 'Padrão (70x40mm)', width: 70, height: 40 },
    { value: 'pequeno', label: 'Pequeno (50x30mm)', width: 50, height: 30 },
    { value: 'grande', label: 'Grande (100x60mm)', width: 100, height: 60 },
    { value: 'custom', label: 'Personalizado', width: 0, height: 0 }
  ];

  ngOnInit(): void {
    this.initForm();
    this.carregarProdutos();
    this.carregarRececoes();
    this.carregarEncomendas();
    this.carregarPaletes();
  }

  ngAfterViewInit(): void {
    setTimeout(() => {
      this.gerarCodigosBarras();
    }, 100);
  }

  private initForm(): void {
    this.form = this.fb.group({
      tipo: ['produto', Validators.required],
      entidadeId: [null, Validators.required],
      quantidade: [1, [Validators.required, Validators.min(1), Validators.max(100)]],
      formato: ['barcode', Validators.required],
      template: ['padrao', Validators.required],
      incluirTexto: [true],
      larguraCustom: [70],
      alturaCustom: [40]
    });

    this.form.get('tipo')?.valueChanges.subscribe(() => {
      this.form.patchValue({ entidadeId: null });
      this.carregarDadosPorTipo();
    });
  }

  gerarCodigosBarras(): void {
    if (!this.barcodeCanvases) return;
    
    this.barcodeCanvases.forEach((canvas: ElementRef, index: number) => {
      const codigo = this.etiquetasGeradas()[index]?.codigo;
      if (codigo && canvas.nativeElement) {
        try {
          JsBarcode(canvas.nativeElement, codigo, {
            format: 'CODE128',
            width: 1.5,
            height: 40,
            displayValue: true,
            fontSize: 12,
            margin: 5
          });
        } catch (err) {
          console.error('Erro ao gerar código de barras:', err);
        }
      }
    });
  }

  carregarDadosPorTipo(): void {
    const tipo = this.form.get('tipo')?.value;
    if (tipo === 'produto') this.carregarProdutos();
    if (tipo === 'recepcao') this.carregarRececoes();
    if (tipo === 'encomenda') this.carregarEncomendas();
    if (tipo === 'palete') this.carregarPaletes();
  }

  carregarProdutos(search?: string): void {
    this.svc.obterProdutos(search).subscribe({
      next: (data: { id: number; sku: string; nome: string }[]) => {
        this.produtos.set(data);
      },
      error: () => this.produtos.set([])
    });
  }

  carregarRececoes(): void {
    this.svc.obterRececoes().subscribe({
      next: (data: { id: number; numeroRecepcao: string; fornecedor: string }[]) => {
        this.rececoes.set(data);
      },
      error: () => this.rececoes.set([])
    });
  }

  carregarEncomendas(): void {
    this.svc.obterEncomendas().subscribe({
      next: (data: { id: number; numeroEncomenda: string; clienteNome: string }[]) => {
        this.encomendas.set(data);
      },
      error: () => this.encomendas.set([])
    });
  }

  carregarPaletes(): void {
    this.svc.obterPaletes().subscribe({
      next: (data: { id: number; codigo: string; localizacao: string }[]) => {
        this.paletes.set(data);
      },
      error: () => this.paletes.set([])
    });
  }

  onProdutoSearch(event: Event): void {
    const search = (event.target as HTMLInputElement).value;
    if (search && search.length > 2) {
      this.carregarProdutos(search);
    }
  }

  getEntidadeSelecionada(): any {
    const tipo = this.form.get('tipo')?.value;
    const id = this.form.get('entidadeId')?.value;
    if (!id) return null;
    
    if (tipo === 'produto') return this.produtos().find(p => p.id === id);
    if (tipo === 'recepcao') return this.rececoes().find(r => r.id === id);
    if (tipo === 'encomenda') return this.encomendas().find(e => e.id === id);
    if (tipo === 'palete') return this.paletes().find(p => p.id === id);
    return null;
  }

  getCodigoParaEtiqueta(): string {
    const entidade = this.getEntidadeSelecionada();
    const tipo = this.form.get('tipo')?.value;
    if (!entidade) return '';
    
    if (tipo === 'produto') return entidade.sku;
    if (tipo === 'recepcao') return entidade.numeroRecepcao;
    if (tipo === 'encomenda') return entidade.numeroEncomenda;
    if (tipo === 'palete') return entidade.codigo;
    return '';
  }

  getTextoParaEtiqueta(): string {
    const entidade = this.getEntidadeSelecionada();
    const tipo = this.form.get('tipo')?.value;
    const quantidade = this.form.get('quantidade')?.value;
    if (!entidade) return '';
    
    let texto = '';
    if (tipo === 'produto') texto = `${entidade.sku} - ${entidade.nome}`;
    if (tipo === 'recepcao') texto = `${entidade.numeroRecepcao} - ${entidade.fornecedor}`;
    if (tipo === 'encomenda') texto = `${entidade.numeroEncomenda} - ${entidade.clienteNome}`;
    if (tipo === 'palete') texto = `${entidade.codigo} - ${entidade.localizacao || 'Sem localização'}`;
    
    if (quantidade > 1) texto += ` (x${quantidade})`;
    return texto;
  }

  gerarEtiquetas(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.errorMsg.set('Preencha todos os campos obrigatórios.');
      return;
    }

    this.isGenerating.set(true);
    this.errorMsg.set(null);
    this.etiquetasGeradas.set([]);

    const v = this.form.getRawValue();
    const quantidade = v.quantidade;
    const codigoBase = this.getCodigoParaEtiqueta();
    
    const etiquetas: EtiquetaResponse[] = [];
    for (let i = 0; i < quantidade; i++) {
      const codigo = `${codigoBase}-${(i + 1).toString().padStart(3, '0')}`;
      etiquetas.push({
        id: `${Date.now()}-${i}`,
        codigo: codigo,
        tipo: v.tipo,
        dados: { sequencial: i + 1 },
        url: ''
      });
    }
    
    this.etiquetasGeradas.set(etiquetas);
    this.showPreview.set(true);
    this.isGenerating.set(false);
    
    setTimeout(() => {
      this.gerarCodigosBarras();
    }, 100);
    
    this.successMsg.set(`${etiquetas.length} etiqueta(s) gerada(s) com sucesso!`);
    setTimeout(() => this.successMsg.set(null), 3000);
  }

  imprimir(): void {
    if (this.etiquetasGeradas().length === 0) return;
    
    const printWindow = window.open('', '_blank');
    if (!printWindow) return;
    
    const formato = this.form.get('formato')?.value;
    const incluirTexto = this.form.get('incluirTexto')?.value;
    const texto = this.getTextoParaEtiqueta();
    
    printWindow.document.write(`
      <!DOCTYPE html>
      <html>
      <head>
        <title>Impressão de Etiquetas</title>
        <meta charset="UTF-8">
        <style>
          * { margin: 0; padding: 0; box-sizing: border-box; }
          body { font-family: Arial, sans-serif; padding: 20px; background: white; }
          .etiquetas-container { display: flex; flex-wrap: wrap; gap: 16px; }
          .etiqueta {
            border: 1px solid #ccc;
            border-radius: 8px;
            padding: 16px;
            text-align: center;
            width: 280px;
            min-height: 180px;
            page-break-inside: avoid;
          }
          .codigo { font-size: 12px; font-weight: bold; margin-top: 12px; }
          .texto { font-size: 11px; margin-top: 8px; color: #555; }
          .qrcode-container { margin: 10px 0; display: flex; justify-content: center; }
          @media print {
            body { padding: 0; margin: 0; }
            .etiqueta { break-inside: avoid; }
          }
        </style>
      </head>
      <body>
        <div class="etiquetas-container">
    `);
    
    this.etiquetasGeradas().forEach(etiqueta => {
      printWindow.document.write(`
        <div class="etiqueta">
          ${formato === 'qrcode' || formato === 'ambos' ? `
            <div class="qrcode-container">
              <img src="https://api.qrserver.com/v1/create-qr-code/?size=100x100&data=${etiqueta.codigo}" style="width: 80px; height: 80px;" />
            </div>
          ` : ''}
          <div class="codigo">${etiqueta.codigo}</div>
          ${incluirTexto ? `<div class="texto">${texto}</div>` : ''}
        </div>
      `);
    });
    
    printWindow.document.write(`
        </div>
      </body>
      </html>
    `);
    
    printWindow.document.close();
    setTimeout(() => printWindow.print(), 500);
  }

  limpar(): void {
    this.etiquetasGeradas.set([]);
    this.showPreview.set(false);
    this.form.patchValue({ entidadeId: null, quantidade: 1 });
  }
}