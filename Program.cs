using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PrecoBoi.Api.Configuration;
using PrecoBoi.Api.Repositories;
using PrecoBoi.Api.Services;

// Mapear colunas snake_case do PostgreSQL para propriedades PascalCase do C#
DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();

// Documentação técnica OpenAPI / Swagger
builder.Services.AddSwaggerDocumentation();

// Repositories
builder.Services.AddScoped<UsuarioRepository>();
builder.Services.AddScoped<CorretorRepository>();
builder.Services.AddScoped<MunicipioOrigemRepository>();
builder.Services.AddScoped<MunicipioDestinoRepository>();
builder.Services.AddScoped<CategoriaRepository>();
builder.Services.AddScoped<IcmsRepository>();
builder.Services.AddScoped<CotacaoRegionalRepository>();
builder.Services.AddScoped<ConfigComissaoRepository>();
builder.Services.AddScoped<NegociacaoRepository>();
builder.Services.AddScoped<AuditoriaRepository>();

// Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CalculoService>();
builder.Services.AddScoped<NegociacaoService>();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        var origins = builder.Configuration["AllowedOrigins"]?.Split(',')
            ?? new[] { "http://localhost:5173" };
        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Swagger disponível em todos os ambientes (documentação técnica da API)
app.UseSwaggerDocumentation();

app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
