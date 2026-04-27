using Accusoft.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Accusoft.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User>                    Users                  => Set<User>();
    public DbSet<AuditLog>               AuditLogs              => Set<AuditLog>();
    public DbSet<Alerta>                 Alertas                => Set<Alerta>();
    public DbSet<Produto>                Produtos               => Set<Produto>();
    public DbSet<ClienteCatalogo>        ClientesCatalogo       => Set<ClienteCatalogo>();
    public DbSet<FornecedorCatalogo>     FornecedoresCatalogo   => Set<FornecedorCatalogo>();
    public DbSet<TransportadoraCatalogo> TransportadorasCatalogo=> Set<TransportadoraCatalogo>();
    public DbSet<Armazem>                ArmazensCatalogo       => Set<Armazem>();
    public DbSet<RotaCatalogo>           RotasCatalogo          => Set<RotaCatalogo>();
    public DbSet<Invoice>                Faturas                => Set<Invoice>();
    public DbSet<InvoiceItem>            FaturaItens            => Set<InvoiceItem>();
    public DbSet<Estoque>                Estoques               => Set<Estoque>();
    public DbSet<MovimentacaoEstoque>    MovimentacoesEstoque   => Set<MovimentacaoEstoque>();
    public DbSet<Veiculo>                Veiculos               => Set<Veiculo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresEnum<UserRole>("user_role");
        modelBuilder.HasPostgresEnum<UserStatus>("user_status");
        modelBuilder.HasPostgresEnum<AlertaTipo>("alerta_tipo");
        modelBuilder.HasPostgresEnum<MovimentacaoTipo>("movimentacao_tipo");

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Role)  .HasColumnType("user_role");
            entity.Property(u => u.Status).HasColumnType("user_status");
        });

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

        modelBuilder.Entity<MovimentacaoEstoque>(entity =>
        {
            entity.Property(m => m.Tipo).HasColumnType("movimentacao_tipo");

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

        modelBuilder.Entity<RotaCatalogo>(entity =>
        {
            entity.HasIndex(r => r.Codigo).IsUnique();
            entity.HasOne(r => r.Transportadora)
                  .WithMany()
                  .HasForeignKey(r => r.TransportadoraId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(r => r.CriadoPorUtilizador)
                  .WithMany()
                  .HasForeignKey(r => r.CriadoPor)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasIndex(i => i.NumeroFatura).IsUnique();
            entity.HasOne(i => i.Usuario)
                  .WithMany()
                  .HasForeignKey(i => i.UsuarioId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasOne(ii => ii.Fatura)
                  .WithMany(f => f.Itens)
                  .HasForeignKey(ii => ii.FaturaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasOne(al => al.Admin)
                  .WithMany(u => u.AuditLogs)
                  .HasForeignKey(al => al.AdminId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.Property(al => al.Detalhe).HasColumnType("jsonb");
        });

        modelBuilder.Entity<Alerta>(entity =>
        {
            entity.Property(a => a.Tipo).HasColumnType("alerta_tipo");

            entity.HasOne(a => a.Usuario)
                  .WithMany(u => u.Alertas)
                  .HasForeignKey(a => a.UsuarioId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
