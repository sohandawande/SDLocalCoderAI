using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Scalar.AspNetCore;
using SD.LocalCoder.AI.Api.DependencyInjection;
using SD.LocalCoder.AI.Core.DependencyInjection;
using SD.LocalCoder.AI.Git.DependencyInjection;
using SD.LocalCoder.AI.Model.Common.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AiProviderOptions>(
    builder.Configuration.GetSection(AiProviderOptions.SectionName));

var ai = builder.Configuration.GetSection(AiProviderOptions.SectionName).Get<AiProviderOptions>()
          ?? new AiProviderOptions();

builder.Services.AddControllers();
builder.Services.AddApiLayer(builder.Configuration);
builder.Services.AddGitLayer(builder.Configuration);
builder.Services.AddCoreLayer(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Offline Ollama or online OpenAI-compatible endpoint (config-driven)
builder.Services.AddKernel()
    .AddOpenAIChatCompletion(
        modelId: ai.ModelId,
        apiKey: string.IsNullOrWhiteSpace(ai.ApiKey) ? "ollama" : ai.ApiKey,
        endpoint: new Uri(ai.Endpoint)
    );

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors("AllowAngular");
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();
