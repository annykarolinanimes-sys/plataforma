using Accusoft.Api.Domain.Entities;
using Accusoft.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accusoft.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração Fluent API para os campos de versionamento do Documento.
/// Complementa DocumentoConfiguration via IEntityTypeConfiguration parcial.
///
/// Nota: Em EF Core não é possível ter dois IEntityTypeConfiguration para a mesma
/// entidade, por isso este ficheiro serve como documentação de referência e os
/// campos de versioning devem ser acrescentados diretamente ao DocumentoConfiguration.
///
/// Este ficheiro contém a configuração COMPLETA atualizada incluindo versioning.
/// </summary>
public sealed class DocumentoVersioningConfiguration : IEntityTypeConfiguration<Documento>
{
    public void Configure(EntityTypeBuilder<Documento> builder)
    {
        // ─── Nota de Integração ────────────────────────────────────────────────
        // Este ficheiro SUBSTITUI DocumentoConfiguration.cs ao ser aplicado.
        // Contém toda a configuração original + campos de versionamento.
        // Em projetos reais, consolidar num único IEntityTypeConfiguration<Documento>.

        // ─── Versionamento ────────────────────────────────────────────────────

        builder.Property(d => d.Versao)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(d => d.IsLatest)
            .IsRequired()
            .HasDefaultValue(true);

        // Self-reference: VersaoAnterior
        builder.Property(d => d.VersaoAnteriorId);

        // Self-reference: DocumentoOrigem (raiz da cadeia de versões)
        builder.Property(d => d.DocumentoOrigemId);

        // Relacionamento self-referencing para cadeia de versões
        builder.HasOne(d => d.DocumentoOrigem)
            .WithMany(d => d.VersoesDerivadas)
            .HasForeignKey(d => d.DocumentoOrigemId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // ─── Rejeição ─────────────────────────────────────────────────────────

        builder.Property(d => d.ComentarioRejeicao)
            .HasMaxLength(2000);

        builder.Property(d => d.RejeitadoPor)
            .HasMaxLength(256);

        // ─── Índices de Versionamento ─────────────────────────────────────────

        // Garantia de unicidade: apenas um IsLatest=true por cadeia de versões
        // (não é constraint nativa EF — enforced no domínio + job de reconciliação)
        builder.HasIndex(d => new { d.DocumentoOrigemId, d.IsLatest })
            .HasDatabaseName("IX_Documentos_Origem_IsLatest")
            .HasFilter("[DocumentoOrigemId] IS NOT NULL AND [IsLatest] = 1");

        // Pesquisa de todas as versões de um documento
        builder.HasIndex(d => new { d.DocumentoOrigemId, d.Versao })
            .HasDatabaseName("IX_Documentos_Origem_Versao")
            .HasFilter("[DocumentoOrigemId] IS NOT NULL");

        // Navegação pela cadeia de versões (anterior → próximo)
        builder.HasIndex(d => d.VersaoAnteriorId)
            .HasDatabaseName("IX_Documentos_VersaoAnterior")
            .HasFilter("[VersaoAnteriorId] IS NOT NULL");
    }
}

/// <summary>
/// Script SQL de migração manual para adicionar campos de versionamento.
/// Usar apenas se não estiver a usar EF Core Migrations automáticas.
/// </summary>
public static class MigracaoVersioningScript
{
    public const string UpScript = """
        -- ECM: Adição de campos de versionamento ao schema ecm.Documentos
        -- Versão: 2.0 | Data: gerado automaticamente

        ALTER TABLE [ecm].[Documentos]
            ADD [Versao]              INT              NOT NULL DEFAULT 1,
                [IsLatest]            BIT              NOT NULL DEFAULT 1,
                [DocumentoOrigemId]   UNIQUEIDENTIFIER NULL,
                [VersaoAnteriorId]    UNIQUEIDENTIFIER NULL,
                [ComentarioRejeicao]  NVARCHAR(2000)   NULL,
                [RejeitadoPor]        NVARCHAR(256)    NULL,
                [RejeitadoEm]         DATETIMEOFFSET   NULL;

        -- Índice para garantir IsLatest único por cadeia
        CREATE INDEX [IX_Documentos_Origem_IsLatest]
            ON [ecm].[Documentos] ([DocumentoOrigemId], [IsLatest])
            WHERE [DocumentoOrigemId] IS NOT NULL AND [IsLatest] = 1;

        -- Índice para listar versões de um documento
        CREATE INDEX [IX_Documentos_Origem_Versao]
            ON [ecm].[Documentos] ([DocumentoOrigemId], [Versao])
            WHERE [DocumentoOrigemId] IS NOT NULL;

        -- FK self-reference para cadeia de versões
        ALTER TABLE [ecm].[Documentos]
            ADD CONSTRAINT [FK_Documentos_DocumentoOrigem]
            FOREIGN KEY ([DocumentoOrigemId])
            REFERENCES [ecm].[Documentos] ([Id]);
        """;

    public const string DownScript = """
        -- ECM: Rollback de campos de versionamento

        ALTER TABLE [ecm].[Documentos]
            DROP CONSTRAINT [FK_Documentos_DocumentoOrigem];

        DROP INDEX [IX_Documentos_Origem_IsLatest] ON [ecm].[Documentos];
        DROP INDEX [IX_Documentos_Origem_Versao]   ON [ecm].[Documentos];

        ALTER TABLE [ecm].[Documentos]
            DROP COLUMN [Versao],
                        [IsLatest],
                        [DocumentoOrigemId],
                        [VersaoAnteriorId],
                        [ComentarioRejeicao],
                        [RejeitadoPor],
                        [RejeitadoEm];
        """;
}
