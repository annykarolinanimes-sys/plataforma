
using System.Text;
using Accusoft.Api.Data;
using Accusoft.Api.Models;
using Accusoft.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
var translator = new Npgsql.NameTranslation.NpgsqlSnakeCaseNameTranslator();
var dsBuilder  = new NpgsqlDataSourceBuilder(connectionString);
dsBuilder.MapEnum<UserRole>("user_role");
dsBuilder.MapEnum<UserStatus>("user_status");
dsBuilder.MapEnum<AlertaTipo>("alerta_tipo");
dsBuilder.MapEnum<MovimentacaoTipo>("movimentacao_tipo");
dsBuilder.MapEnum<EnvioEstado>("envio_estado");
dsBuilder.MapEnum<DocTipo>("doc_tipo");
var dataSource = dsBuilder.Build();

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(dataSource));

builder.Services.AddScoped<IJwtService,         JwtService>();
builder.Services.AddScoped<IAuditService,       AuditService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt => {
        opt.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew                = TimeSpan.Zero,
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(opt => {
    opt.AddPolicy("AngularDev", policy =>
        policy.WithOrigins("http://localhost:4200", "https://accusoft.exemplo.com")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddControllers()
    .AddJsonOptions(o => {
        o.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter(
                System.Text.Json.JsonNamingPolicy.CamelCase));
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Accusoft API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer",
        BearerFormat = "JWT", In = ParameterLocation.Header, Description = "Bearer {token}",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

app.UseCors("AngularDev");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (app.Environment.IsDevelopment()) {
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.Run();
