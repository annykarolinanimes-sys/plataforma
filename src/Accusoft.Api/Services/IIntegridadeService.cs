using Accusoft.Api.Domain.Entities;
using System.IO;

namespace Accusoft.Api.Services;

public interface IIntegridadeService
{
    /// <summary>
    /// Calcula o hash SHA-256 de um fluxo.
    /// </summary>
    Task<string> CalcularHashAsync(Stream stream, CancellationToken ct = default);

    /// <summary>
    /// Calcula o hash SHA-256 de um ficheiro físico.
    /// Usado para verificação de integridade antes de servir downloads.
    /// </summary>
    Task<string> CalcularHashFicheiroAsync(string caminhoFisico, CancellationToken ct = default);

    /// <summary>
    /// Persiste um documento e o ficheiro no storage numa transação ACID.
    /// </summary>
    Task PersistirDocumentoComAcidAsync(
        Documento documento,
        Stream stream,
        string caminhoAbsoluto,
        Guid correlationId,
        CancellationToken ct = default);

    /// <summary>
    /// Verifica a integridade de um documento comparando o hash atual com o esperado.
    /// Se divergir, marca o documento como IntegridadeCompromissada.
    /// </summary>
    Task<ResultadoVerificacaoIntegridade> VerificarIntegridadeAsync(
        Guid documentoId,
        string caminhoFisico,
        string verificadoPor,
        Guid correlationId,
        CancellationToken ct = default);
}

public sealed record ResultadoVerificacaoIntegridade(
    Guid DocumentoId,
    bool Valido,
    string? HashEsperado,
    string? HashAtual,
    DateTimeOffset VerificadoEm,
    string? MensagemErro
);