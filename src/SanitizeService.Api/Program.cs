using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using SanitizeService.Api;
using SanitizeService.Application;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
});

builder.Services.Configure<SanitizationOptions>(builder.Configuration.GetSection(SanitizationOptions.SectionName));
builder.Services.AddSanitization();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SanitizeExceptionHandler>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Host-wide HTTP body ceiling (independent of per-endpoint file limits; see SanitizationOptions.MaxRequestBodyBytes).
var maxRequestBodyBytes = builder.Configuration.GetValue(
    $"{SanitizationOptions.SectionName}:{nameof(SanitizationOptions.MaxRequestBodyBytes)}",
    SanitizationOptions.DefaultMaxRequestBodyBytes);

builder.Services.Configure<KestrelServerOptions>(o => { o.Limits.MaxRequestBodySize = maxRequestBodyBytes; });
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = maxRequestBodyBytes;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.MapControllers();

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
