using System.Reflection;
using Microsoft.OpenApi.Models;

namespace PrecoBoi.Api.Configuration;

/// <summary>
/// Configuração da documentação técnica da API (OpenAPI / Swagger).
/// Centraliza a definição de metadados, esquema de segurança JWT e
/// inclusão dos comentários XML para manter o <c>Program.cs</c> enxuto.
/// </summary>
public static class SwaggerExtensions
{
    /// <summary>Nome do esquema de segurança Bearer usado nas operações protegidas.</summary>
    private const string BearerScheme = "Bearer";

    /// <summary>
    /// Registra o gerador OpenAPI com metadados completos, autenticação JWT
    /// e os comentários XML do projeto.
    /// </summary>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Facholi · API de Originação de Gado",
                Version = "v1",
                Description = """
                    Documentação técnica da **API de Originação** (PrecoBoi.Api).

                    Plataforma de apoio à compra/originação de bovinos: gestão de cadastros
                    (corretores, municípios, categorias, ICMS e cotações regionais),
                    **simulação de preços de praça**, **negociações** e **dashboards** analíticos.

                    ## Autenticação
                    Todos os endpoints — exceto `POST /api/auth/login` e `GET /api/debug/tz` —
                    exigem um token JWT no cabeçalho `Authorization: Bearer {token}`.
                    Obtenha o token via `POST /api/auth/login`.

                    ## Perfis de acesso
                    - **Admin** — acesso total, incluindo cadastros, usuários e auditoria.
                    - **Comprador** — operação de negociações, simulações e dashboards.

                    Operações restritas a administradores estão marcadas como tal e retornam
                    `403 Forbidden` quando acessadas por outros perfis.

                    ## Convenções
                    - Valores monetários em Reais (R$); preços de gado em **R$/kg** ou **R$/@** (arroba = 30 kg).
                    - Datas/horas no fuso do servidor (America/Sao_Paulo).
                    - Erros de negócio retornam `{ "mensagem": "..." }` com status `400`.
                    """,
                Contact = new OpenApiContact
                {
                    Name = "Facholi · Equipe de Desenvolvimento",
                    Email = "suporte@wdevdigital.com.br"
                }
            });

            // Esquema de segurança JWT Bearer
            options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = """
                    Informe **apenas** o token JWT (o prefixo `Bearer ` é adicionado automaticamente).
                    Exemplo: `eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...`
                    """
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = BearerScheme
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Habilita o uso de [SwaggerTag], [SwaggerOperation] etc.
            options.EnableAnnotations();

            // Ordena as operações por método HTTP para uma leitura previsível
            options.OrderActionsBy(api => $"{api.GroupName}_{api.HttpMethod}");

            // Inclui os comentários XML gerados pelo compilador
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        });

        return services;
    }

    /// <summary>
    /// Habilita os middlewares do Swagger (JSON OpenAPI + UI interativa).
    /// A UI fica disponível na raiz da aplicação (<c>/</c>).
    /// </summary>
    public static WebApplication UseSwaggerDocumentation(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Facholi Originação API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "Facholi · API de Originação — Documentação Técnica";
            options.DefaultModelsExpandDepth(1);
            options.DisplayRequestDuration();
            options.EnableDeepLinking();
            options.EnableFilter();
        });

        return app;
    }
}
