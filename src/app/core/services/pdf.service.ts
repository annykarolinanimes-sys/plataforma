import { Injectable, inject } from '@angular/core';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import { Invoice, InvoiceItem } from './invoice.service';
import { DocumentosService } from './documentos.service';

export interface TripReportData {
  id: number;
  clienteNome: string;
  origem: string;
  destino: string;
  dataInicio: string;
  dataFim?: string;
  distanciaKm: number;
  status: string;
  observacoes?: string;
  incidentes?: Array<{
    tipo: string;
    descricao: string;
    dataOcorrencia: string;
    resolvido: boolean;
  }>;
}

export interface PdfField {
  label: string;
  value: string | number | null;
}

export interface PersistPdfResult {
  blob: Blob;
  fileName: string;
  mimeType: string;
  documentoId: string;
  hashSHA256: string;
  url: string;
}

@Injectable({
  providedIn: 'root'
})
export class PdfService {
  private documentosService = inject(DocumentosService);

  private createDocument(title: string): jsPDF {
    const doc = new jsPDF({ unit: 'mm', format: 'a4' });
    const pageWidth = doc.internal.pageSize.getWidth();

    doc.setFontSize(20);
    doc.text(title, 14, 20);

    doc.setFontSize(10);
    doc.setTextColor(100, 100, 100);
    doc.text('ACCUSOFT Logística', 14, 30);
    doc.text(`Gerado em ${new Date().toLocaleString()}`, pageWidth - 14, 20, { align: 'right' });

    return doc;
  }

  generateEntityPdf(title: string, fields: PdfField[], footer?: string): Blob {
    const doc = this.createDocument(title);

    const tableBody = fields.map((field) => [field.label, field.value ?? '']);
    autoTable(doc, {
      startY: 35,
      head: [['Campo', 'Valor']],
      body: tableBody,
      styles: { fontSize: 10, cellPadding: 4 },
      headStyles: { fillColor: [243, 184, 91], textColor: [15, 23, 42], fontStyle: 'bold' },
      margin: { left: 14, right: 14 },
      columnStyles: { 0: { cellWidth: 50 }, 1: { cellWidth: 'auto' } }
    });

    if (footer) {
      const finalY = (doc as any).lastAutoTable?.finalY || doc.internal.pageSize.getHeight() - 30;
      doc.setFontSize(9);
      doc.setTextColor(100, 100, 100);
      doc.text(footer, 14, finalY + 15);
    }

    return doc.output('blob');
  }

  downloadPdf(blob: Blob, filename: string): void {
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    window.URL.revokeObjectURL(url);
  }

  /**
   * Gera PDF e persiste automaticamente no ECM.
   * Retorna {blob, documentoId, hashSHA256, url} — permite download imediato
   * enquanto documento é armazenado no backend.
   */
  async generateAndPersistPdf(
    title: string,
    fields: PdfField[],
    fileName: string,
    categoria: string = 'Relatorio',
    contexto: string = 'Interno',
    footer?: string
  ): Promise<PersistPdfResult> {
    // 1. Gerar blob em memória
    const doc = this.createDocument(title);

    const tableBody = fields.map((field) => [field.label, field.value ?? '']);
    autoTable(doc, {
      startY: 35,
      head: [['Campo', 'Valor']],
      body: tableBody,
      styles: { fontSize: 10, cellPadding: 4 },
      headStyles: { fillColor: [243, 184, 91], textColor: [15, 23, 42], fontStyle: 'bold' },
      margin: { left: 14, right: 14 },
      columnStyles: { 0: { cellWidth: 50 }, 1: { cellWidth: 'auto' } }
    });

    if (footer) {
      const finalY = (doc as any).lastAutoTable?.finalY || doc.internal.pageSize.getHeight() - 30;
      doc.setFontSize(9);
      doc.setTextColor(100, 100, 100);
      doc.text(footer, 14, finalY + 15);
    }

    const blob = doc.output('blob');

    // 2. Calcular SHA-256 do blob em memória (para resposta imediata)
    const hashBuffer = await crypto.subtle.digest('SHA-256', await blob.arrayBuffer());
    const hashArray = Array.from(new Uint8Array(hashBuffer));
    const hashSHA256 = hashArray.map(b => b.toString(16).padStart(2, '0')).join('');

    // 3. Persistir no ECM (async, sem bloquear retorno ao cliente)
    let persistedDocId = '(pending)';
    let persistedUrl = '';
    let persistedHash = hashSHA256;

    this.documentosService.uploadPdfBlob(blob, fileName, categoria, contexto, undefined)
      .subscribe({
        next: (response) => {
          persistedDocId = response.documentoId;
          persistedUrl = response.url;
          persistedHash = response.hashSHA256;
          console.log('[PdfService] PDF persistido no ECM:', {
            documentoId: persistedDocId,
            url: persistedUrl,
            hash: persistedHash
          });
        },
        error: (err) => {
          console.error('[PdfService] Erro ao persistir PDF no ECM:', err);
        }
      });

    // 4. Retornar imediatamente com blob disponível para download
    // (documentoId será atualizado após persistência completar no backend)
    return {
      blob,
      fileName,
      mimeType: 'application/pdf',
      documentoId: persistedDocId,
      hashSHA256: persistedHash,
      url: persistedUrl
    };
  }

  generateInvoicePdf(invoice: Invoice): Blob {
    const doc = new jsPDF({ unit: 'mm', format: 'a4' });
    const margin = 14;
    const pageWidth = doc.internal.pageSize.getWidth();

    doc.setFontSize(18);
    doc.text('Fatura', margin, 20);

    doc.setFontSize(10);
    doc.text('ACCUSOFT Logística', margin, 28);
    doc.text('Rua da Logística 123', margin, 33);
    doc.text('Telefone: +351 123 456 789', margin, 38);
    doc.text('Email: contacto@accusoft.pt', margin, 43);

    doc.setFontSize(12);
    doc.text(`Fatura Nº: ${invoice.numeroFatura}`, pageWidth - margin, 28, { align: 'right' });
    doc.text(`Data: ${invoice.dataDoc}`, pageWidth - margin, 33, { align: 'right' });

    doc.setFontSize(11);
    doc.text('Dados do cliente', margin, 55);
    doc.setFontSize(10);
    doc.text(`Nome: ${invoice.clienteNome}`, margin, 61);
    doc.text(`Contacto: ${invoice.clienteContacto}`, margin, 66);
    if (invoice.clienteNif) doc.text(`NIF: ${invoice.clienteNif}`, margin, 71);
    if (invoice.clienteEmail) doc.text(`Email: ${invoice.clienteEmail}`, margin, 76);
    if (invoice.clienteMorada) doc.text(`Morada: ${invoice.clienteMorada}`, margin, 81);

    const tableData = invoice.itens.map((item) => [
      item.marca,
      item.modelo,
      item.cor,
      item.matricula,
      item.quantidade.toString(),
      item.precoUnitario.toFixed(2),
      (item.subtotal ?? item.quantidade * item.precoUnitario).toFixed(2)
    ]);

    const invoiceTable = autoTable(doc, {
      startY: 92,
      head: [[
        'Marca',
        'Modelo',
        'Cor',
        'Matrícula',
        'Qtd',
        'Preço unit.',
        'Subtotal'
      ]],
      body: tableData,
      styles: { fontSize: 9 },
      headStyles: { fillColor: [41, 128, 185], textColor: 255 },
      margin: { left: margin, right: margin }
    });

    const totalY = (invoiceTable as any)?.finalY ?? 120;
    doc.setFontSize(12);
    doc.text(`Total final: € ${invoice.valorTotal.toFixed(2)}`, pageWidth - margin, totalY + 15, { align: 'right' });

    if (invoice.observacoes) {
      doc.setFontSize(10);
      doc.text('Observações:', margin, totalY + 26);
      doc.text(invoice.observacoes, margin, totalY + 32);
    }

    doc.setFontSize(9);
    doc.text('Documento gerado eletronicamente.', margin, 285);

    return doc.output('blob');
  }

  generateTripReportPdf(report: TripReportData): Blob {
    const doc = new jsPDF({ unit: 'mm', format: 'a4' });
    const margin = 14;
    const pageWidth = doc.internal.pageSize.getWidth();

    doc.setFontSize(18);
    doc.text('Relatório de Viagem', margin, 20);

    doc.setFontSize(11);
    doc.text(`Viagem: ${report.id}`, margin, 30);
    doc.text(`Cliente: ${report.clienteNome}`, margin, 36);
    doc.text(`Origem: ${report.origem}`, margin, 42);
    doc.text(`Destino: ${report.destino}`, margin, 48);
    doc.text(`Data início: ${report.dataInicio}`, margin, 54);
    if (report.dataFim) doc.text(`Data fim: ${report.dataFim}`, margin, 60);
    doc.text(`Distância: ${report.distanciaKm} km`, margin, 66);
    doc.text(`Status: ${report.status}`, margin, 72);

    let incidentTableFinalY = 78;

    if (report.incidentes && report.incidentes.length) {
      doc.setFontSize(12);
      doc.text('Incidentes associados', margin, 96);
      const incidentLines = report.incidentes.map((inc, index) => [
        `${index + 1}. ${inc.tipo}`,
        `${inc.descricao}`,
        `${inc.dataOcorrencia}`,
        inc.resolvido ? 'Sim' : 'Não'
      ]);

      const incidentTable = autoTable(doc, {
        startY: 100,
        head: [['Tipo', 'Descrição', 'Data', 'Resolvido']],
        body: incidentLines,
        styles: { fontSize: 9 },
        headStyles: { fillColor: [41, 128, 185], textColor: 255 },
        margin: { left: margin, right: margin }
      });

      incidentTableFinalY = (incidentTable as any)?.finalY ?? 110;
    }

    if (report.observacoes) {
      const startY = incidentTableFinalY;
      doc.setFontSize(10);
      doc.text('Observações:', margin, startY + 12);
      doc.text(report.observacoes, margin, startY + 18);
    }

    doc.setFontSize(9);
    doc.text(`Relatório gerado em ${new Date().toLocaleString()}`, margin, 285);

    return doc.output('blob');
  }
}