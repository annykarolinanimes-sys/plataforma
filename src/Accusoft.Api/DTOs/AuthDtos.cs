using Accusoft.Api.Models;

namespace Accusoft.Api.DTOs;

public static class EnumExtensions
{
    public static string ToApiString(this UserRole role) => role switch
    {
        UserRole.Admin => "admin",
        UserRole.User  => "user",
        _              => role.ToString().ToLower(),
    };

    public static string ToApiString(this UserStatus status) => status switch
    {
        UserStatus.Ativo   => "ativo",
        UserStatus.Inativo => "inativo",
        _                  => status.ToString().ToLower(),
    };

    public static string ToApiString(this EnvioEstado estado) => estado switch
    {
        EnvioEstado.Pendente  => "pendente",
        EnvioEstado.Entregue  => "entregue",
        EnvioEstado.Atraso    => "atraso",
        EnvioEstado.Cancelado => "cancelado",
        _                     => estado.ToString().ToLower(),
    };

    public static string ToApiString(this DocTipo tipo) => tipo switch
    {
        DocTipo.Pdf     => "pdf",
        DocTipo.Docx    => "docx",
        DocTipo.Xlsx    => "xlsx",
        DocTipo.Imagem  => "imagem",
        DocTipo.Arquivo => "arquivo",
        DocTipo.Outro   => "outro",
        _               => tipo.ToString().ToLower(),
    };

    public static string ToApiString(this AlertaTipo tipo) => tipo switch
    {
        AlertaTipo.Documento => "documento",
        AlertaTipo.Envio     => "envio",
        AlertaTipo.Sistema   => "sistema",
        _                    => tipo.ToString().ToLower(),
    };


    public static EnvioEstado ToEnvioEstado(this string s) => s.ToLower() switch
    {
        "pendente"  => EnvioEstado.Pendente,
        "entregue"  => EnvioEstado.Entregue,
        "atraso"    => EnvioEstado.Atraso,
        "cancelado" => EnvioEstado.Cancelado,
        _           => throw new ArgumentOutOfRangeException(nameof(s), $"Estado inválido: {s}"),
    };

    public static DocTipo ToDocTipo(this string ext) => ext.ToLower() switch
    {
        ".pdf"           => DocTipo.Pdf,
        ".docx" or ".doc"=> DocTipo.Docx,
        ".xlsx" or ".xls"=> DocTipo.Xlsx,
        ".jpg" or ".jpeg"
            or ".png"    => DocTipo.Imagem,
        ".zip" or ".rar" => DocTipo.Arquivo,
        _                => DocTipo.Outro,
    };

    public static AlertaTipo ToAlertaTipo(this string s) => s.ToLower() switch
    {
        "documento" => AlertaTipo.Documento,
        "envio"     => AlertaTipo.Envio,
        "sistema"   => AlertaTipo.Sistema,
        _           => AlertaTipo.Sistema,
    };
}

public record LoginRequest(string Email, string Senha);

public record RegisterRequest(
    string  Nome,
    string  Email,
    string  Senha,
    string? Departamento,
    string? Cargo,
    string? Telefone
);

public record AuthResponse(
    string Token,
    string Nome,
    string Email,
    string Role,    
    int    UserId
);


public record UserDto(
    int     Id,
    string  Nome,
    string  Email,
    string  Role,       
    string  Status,     
    string? Departamento,
    string? Cargo,
    string? Telefone,
    string? AvatarUrl,
    DateTimeOffset  DataCriacao,
    DateTimeOffset? UltimoLogin
);

public record UpdateProfileRequest(
    string  Nome,
    string? Departamento,
    string? Cargo,
    string? Telefone
);

public record CreateEnvioRequest(
    string NomeEquipamento,
    DateOnly DataPrevista,
    string Estado
);

public record UpdateEnvioRequest(
    string?   NomeEquipamento,
    DateOnly? DataPrevista,
    string?   Estado
);

public record EnvioDto(
    int      Id,
    string   IdString,
    string   NomeEquipamento,
    DateOnly DataPrevista,
    string   Estado,           
    int      UsuarioId,
    string   NomeUsuario,
    DateTimeOffset DataCriacao,
    DateTimeOffset DataAtualizacao,
    IEnumerable<DocumentoDto> Documentos
);


public record DocumentoDto(
    int     Id,
    string  Nome,
    string  PathUrl,
    string  Tipo,             
    long    TamanhoBytes,
    string  TamanhoFormatado,
    int     UsuarioId,
    int?    EnvioId,
    DateTimeOffset  DataUpload,
    DateTimeOffset? DataAbertura
);


public record AlertaDto(
    int     Id,
    string  Tipo,              
    string  Mensagem,
    string? Detalhe,
    bool    Lido,
    DateTimeOffset Data,
    int?    EnvioId = null,
    int?    DocumentoId = null
);

public record MarcarLidoRequest(IEnumerable<int> Ids);


public record ToggleUserRequest(int UserId);

public record AdminStatsDto(
    int TotalUsuariosAtivos,
    int TotalUsuariosInativos,
    int TotalAlertas,
    int AlertasNaoLidos
);

public record MovementRequest(
    string  Sku,
    decimal Quantity,
    string  Type,
    string  From,
    string  To,
    string  Warehouse,
    string  Note
);

public record WarehouseRequest(string Name);

public record InventoryItemDto(
    int     Id,
    string  Sku,
    string  Nome,
    string  Descricao,
    string  Warehouse,
    string  Location,
    string  LotNumber,
    string  ExpiryDate,
    decimal StockQty,
    decimal ReservedQty,
    decimal PickingQty,
    int     MinLevel,
    string  Status,        
    string  LastMovement
);

public record MovementRecordDto(
    int     Id,
    string  Sku,
    string  Nome,
    string  Type,
    decimal Qty,
    string  From,
    string  To,
    string  Warehouse,
    string  User,
    string  Date,
    string  Note
);

public record WarehouseSummaryDto(
    string  Warehouse,
    decimal Total,
    int     Items
);

public record AuditLogDto(
    int     Id,
    int     AdminId,
    string  NomeAdmin,
    string  Acao,
    string? Detalhe,
    string? IpAddress,
    DateTimeOffset Timestamp
);
