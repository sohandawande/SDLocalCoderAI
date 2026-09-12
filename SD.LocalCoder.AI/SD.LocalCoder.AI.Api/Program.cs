using Microsoft.SemanticKernel;
using Scalar.AspNetCore;
using SD.LocalCoder.AI.Api.DependencyInjection;
using SD.LocalCoder.AI.Git.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddApiLayer(builder.Configuration);
builder.Services.AddGitLayer(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

// CORS for Angular later
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ===== Ollama via OpenAI-compatible endpoint =====
builder.Services.AddKernel()
    .AddOpenAIChatCompletion(
        modelId: "qwen2.5-coder:14b",               // Change to your model name
        apiKey: "ollama",                           // Any non-empty string is fine
        endpoint: new Uri("http://localhost:11434/v1")
    );

var app = builder.Build();

// Configure the HTTP request pipeline.
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
