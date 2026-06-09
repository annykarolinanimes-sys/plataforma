using Accusoft.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User>                    Users                   => Set<User>();
    public DbSet<AuditLog>               AuditLogs               => Set<AuditLog>();
    public DbSet<Alerta>                 Alertas                 => Set<Alerta>();
    public DbSet<Produto>                Produtos                => Set<Produto>();
    public DbSet<ClienteCatalogo>        ClientesCatalogo        => Set<ClienteCatalogo>();
    public DbSet<FornecedorCatalogo>     FornecedoresCatalogo    => Set<FornecedorCatalogo>();
    public DbSet<TransportadoraCatalogo> TransportadorasCatalogo => Set<TransportadoraCatalogo>();
    public DbSet<Armazem>                ArmazensCatalogo        => Set<Armazem>();
    public DbSet<Invoice>                Faturas                 => Set<Invoice>();
    public DbSet<InvoiceItem>            FaturaItens             => Set<InvoiceItem>();
    public DbSet<FaturaAnexo>            FaturaAnexos            => Set<FaturaAnexo>();
    public DbSet<Estoque>                Estoques                => Set<Estoque>();
    public DbSet<MovimentacaoEstoque>    MovimentacoesEstoque    => Set<MovimentacaoEstoque>();
    public DbSet<Veiculo>                Veiculos                => Set<Veiculo>();
    public DbSet<VeiculoAnexo>           VeiculoAnexos           => Set<VeiculoAnexo>();
    public DbSet<Recepcao>               Recepcoes               => Set<Recepcao>();
    public DbSet<RecepcaoItem>           RecepcaoItens           => Set<RecepcaoItem>();
    public DbSet<Atribuicao>             Atribuicoes             => Set<Atribuicao>();
    public DbSet<AtribuicaoEntrega>      AtribuicaoEntregas      => Set<AtribuicaoEntrega>();
    public DbSet<GestaoViagem>           GestaoViagens           => Set<GestaoViagem>();
    public DbSet<Incidente>              Incidentes              => Set<Incidente>();
    public DbSet<IncidenteAnexo>         IncidenteAnexos         => Set<IncidenteAnexo>(); 
    public DbSet<Guia>                   Guias                   => Set<Guia>();
    public DbSet<GuiaItem>               GuiaItens               => Set<GuiaItem>();
    public DbSet<ChatMessage>            ChatMessages            => Set<ChatMessage>();
    public DbSet<Sessao>                 Sessoes                 => Set<Sessao>();
    public DbSet<Motorista>              Motoristas              { get; internal set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresEnum<UserRole>();
        modelBuilder.HasPostgresEnum<UserStatus>();
        modelBuilder.HasPostgresEnum<AlertaTipo>();
        modelBuilder.HasPostgresEnum<MovimentacaoTipo>();
        modelBuilder.HasPostgresEnum<DocTipo>();
        modelBuilder.HasPostgresEnum<EnvioEstado>();

        // ── User ──────────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasOne(u => u.Transportadora)
                  .WithMany()
                  .HasForeignKey(u => u.TransportadoraId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Produto ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Produto>(entity =>
        {
            entity.HasIndex(p => p.Sku).IsUnique();
            entity.Property(p => p.PrecoCompra).HasColumnType("decimal(15,4)");
            entity.Property(p => p.PrecoVenda) .HasColumnType("decimal(15,4)");
            entity.HasOne(p => p.Fornecedor)
                  .WithMany()
                  .HasForeignKey(p => p.FornecedorId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(p => p.CriadoPorUtilizador)
                  .WithMany()
                  .HasForeignKey(p => p.CriadoPor)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Estoque ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Estoque>(entity =>
        {
            entity.HasIndex(e => new { e.ProdutoId, e.ArmazemId, e.Localizacao, e.Lote })
                  .IsUnique()
                  .HasDatabaseName("uq_estoque_produto_armazem_local_lote");
            entity.HasOne(e => e.Produto)
                  .WithMany(p => p.Estoques)
                  .HasForeignKey(e => e.ProdutoId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Armazem)
                  .WithMany()
                  .HasForeignKey(e => e.ArmazemId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ── MovimentacaoEstoque ───────────────────────────────────────────────
        modelBuilder.Entity<MovimentacaoEstoque>(entity =>
        {
            entity.HasOne(m => m.Produto)
                  .WithMany(p => p.Movimentacoes)
                  .HasForeignKey(m => m.ProdutoId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(m => m.Armazem)
                  .WithMany()
                  .HasForeignKey(m => m.ArmazemId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(m => m.Usuario)
                  .WithMany()
                  .HasForeignKey(m => m.UsuarioId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Catálogos ─────────────────────────────────────────────────────────
        modelBuilder.Entity<ClienteCatalogo>(entity =>
        {
            entity.HasIndex(c => c.Codigo).IsUnique();
            entity.HasOne(c => c.CriadoPorUtilizador)
                  .WithMany()
                  .HasForeignKey(c => c.CriadoPor)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FornecedorCatalogo>(entity =>
        {
            entity.HasIndex(f => f.Codigo).IsUnique();
            entity.HasOne(f => f.CriadoPorUtilizador)
                  .WithMany()
                  .HasForeignKey(f => f.CriadoPor)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TransportadoraCatalogo>(entity =>
        {
            entity.HasIndex(t => t.Codigo).IsUnique();
            entity.HasOne(t => t.CriadoPorUtilizador)
                  .WithMany()
                  .HasForeignKey(t => t.CriadoPor)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Armazem>(entity =>
        {
            entity.HasIndex(a => a.Codigo).IsUnique();
            entity.HasOne(a => a.CriadoPorUtilizador)
                  .WithMany()
                  .HasForeignKey(a => a.CriadoPor)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Faturas ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasIndex(i => i.NumeroFatura).IsUnique();
            entity.HasOne(i => i.Usuario)
                  .WithMany()
                  .HasForeignKey(i => i.UsuarioId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.Cliente)
                  .WithMany()
                  .HasForeignKey(i => i.ClienteId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.Viagem)
                  .WithMany()
                  .HasForeignKey(i => i.ViagemId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasOne(ii => ii.Fatura)
                  .WithMany(f => f.Itens)
                  .HasForeignKey(ii => ii.FaturaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FaturaAnexo>(entity =>
        {
            entity.HasOne(a => a.Fatura)
                  .WithMany(f => f.Anexos)
                  .HasForeignKey(a => a.FaturaId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(a => a.Usuario)
                  .WithMany()
                  .HasForeignKey(a => a.UsuarioId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── AuditLog ──────────────────────────────────────────────────────────
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasOne(al => al.Admin)
                  .WithMany(u => u.AuditLogs)
                  .HasForeignKey(al => al.AdminId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.Property(al => al.Detalhe).HasColumnType("jsonb");
        });

        // ── Alerta ────────────────────────────────────────────────────────────
        modelBuilder.Entity<Alerta>(entity =>
        {
            entity.HasOne(a => a.Usuario)
                  .WithMany(u => u.Alertas)
                  .HasForeignKey(a => a.UsuarioId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Veiculo ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Veiculo>(entity =>
        {
            entity.HasIndex(v => v.Vin)
                  .IsUnique()
                  .HasFilter("vin IS NOT NULL")
                  .HasDatabaseName("uq_veiculos_vin");

            entity.HasOne(v => v.CriadoPorUtilizador)
                  .WithMany()
                  .HasForeignKey(v => v.CriadoPor)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(v => v.Proprietario)
                  .WithMany()
                  .HasForeignKey(v => v.ProprietarioId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ── VeiculoAnexo ──────────────────────────────────────────────────────
        modelBuilder.Entity<VeiculoAnexo>(entity =>
        {
            entity.HasOne(a => a.Veiculo)
                  .WithMany(v => v.Anexos)
                  .HasForeignKey(a => a.VeiculoId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Usuario)
                  .WithMany()
                  .HasForeignKey(a => a.UsuarioId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IncidenteAnexo>(entity =>
        {
            entity.HasOne(a => a.Incidente)
                  .WithMany(i => i.Anexos)
                  .HasForeignKey(a => a.IncidenteId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Usuario)
                  .WithMany()
                  .HasForeignKey(a => a.UsuarioId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}