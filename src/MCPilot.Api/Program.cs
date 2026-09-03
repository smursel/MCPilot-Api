using MCPilot.Api.Infrastructure;
using MCPilot.Application.Abstractions;
using MCPilot.Application.Analytics;
using MCPilot.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

const string AngularCorsPolicy = "angular";

builder.Services
    .AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Servis API anahtari.",
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Reference = new Microsoft.OpenApi.Models.OpenApiReference
            {
                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                Id = "ApiKey",
            },
        }] = Array.Empty<string>(),
    });

    options.SwaggerDoc("v1", new()
    {
        Title = "MCPilot API",
        Version = "v1",
        Description = "Dogal dil sorularini MCP araclari uzerinden veritabanina baglayan sohbet API'si.",
    });

    var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlFile))
    {
        options.IncludeXmlComments(xmlFile);
    }
});

builder.Services.AddProblemDetails();

builder.Services.AddCors(options => options.AddPolicy(AngularCorsPolicy, policy =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                  ?? ["http://localhost:4200"];

    policy.WithOrigins(origins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
}));

var apiKeyOptions = builder.Configuration.GetSection(ApiKeyOptions.SectionName).Get<ApiKeyOptions>() ?? new ApiKeyOptions();
builder.Services.AddSingleton(apiKeyOptions);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Services.AddMCPilot(builder.Configuration);

var app = builder.Build();

// nginx arkasinda calisirken sema (https) ve istemci IP'si bu basliklardan okunur.
app.UseForwardedHeaders();

var pathBase = app.Configuration["PathBase"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
}

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var feature = context.Features.Get<IExceptionHandlerFeature>();
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("MCPilot.Api");
    logger.LogError(feature?.Error, "Istek islenirken hata olustu: {Path}", context.Request.Path);

    var isUpstreamFailure = feature?.Error is LlmProviderException or AnalyticsUnavailableException;

    var problem = new ProblemDetails
    {
        Title = feature?.Error switch
        {
            LlmProviderException => "Yapay zeka servisine erisilemedi",
            AnalyticsUnavailableException => "Analiz verisi alinamadi",
            _ => "Istek islenemedi",
        },
        Status = isUpstreamFailure ? StatusCodes.Status502BadGateway : StatusCodes.Status500InternalServerError,
        Detail = app.Environment.IsDevelopment() || isUpstreamFailure
            ? feature?.Error.Message
            : "Beklenmeyen bir hata olustu.",
    };

    context.Response.StatusCode = problem.Status.Value;
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(problem);
}));

if (app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(AngularCorsPolicy);
app.UseMiddleware<ApiKeyMiddleware>();
app.UseMiddleware<SessionCookieMiddleware>();
app.MapControllers();

app.Run();
