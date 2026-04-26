using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Api.BackgroundServices;
using RetroBoard.Api.Hubs;
using RetroBoard.Application;
using RetroBoard.Application.Common.Exceptions;
using RetroBoard.Infrastructure;
using RetroBoard.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// MediatR also needs the API assembly so the SignalR notification handlers register.
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
    typeof(Program).Assembly,
    typeof(RetroBoard.Application.DependencyInjection).Assembly));

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<PresenceSweeperService>();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins("http://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BoardDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler(o => o.Run(async ctx =>
{
    var feature = ctx.Features.Get<IExceptionHandlerFeature>();
    var ex = feature?.Error;
    var (status, title) = ex switch
    {
        ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
        NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
        ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
        _ => (StatusCodes.Status500InternalServerError, "Internal error"),
    };
    var problem = new ProblemDetails
    {
        Status = status,
        Title = title,
        Detail = ex?.Message,
    };
    if (ex is ValidationException vex)
        problem.Extensions["errors"] = vex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage });
    ctx.Response.StatusCode = status;
    ctx.Response.ContentType = "application/problem+json";
    await ctx.Response.WriteAsJsonAsync(problem);
}));

app.UseCors();
app.MapControllers();
app.MapHub<BoardHub>("/hubs/board");

app.Run();

public partial class Program;
