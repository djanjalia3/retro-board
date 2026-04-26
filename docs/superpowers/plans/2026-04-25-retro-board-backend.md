# RetroBoard Backend (.NET + Postgres) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the ASP.NET Core 9 backend for RetroBoard with CQRS (MediatR), EF Core + Postgres, SignalR realtime hub, and an integration-tested local dev setup.

**Architecture:** Clean layering — Domain, Application (MediatR + FluentValidation), Infrastructure (EF Core + Npgsql), Api (controllers + SignalR hub + background sweeper). HTTP for writes; SignalR for server→client push. Notifications fan out from MediatR `INotification` handlers to `IHubContext<BoardHub>` group `board:{slug}`.

**Tech Stack:** .NET 9, ASP.NET Core, MediatR 12, FluentValidation 11, EF Core 9 + Npgsql, SignalR, xUnit, Testcontainers for .NET, Microsoft.AspNetCore.Mvc.Testing.

**Spec reference:** `docs/superpowers/specs/2026-04-25-retro-board-dotnet-rewrite-design.md`

---

## File Structure (Backend)

```
server/
  RetroBoard.sln
  src/
    RetroBoard.Domain/
      RetroBoard.Domain.csproj
      Boards/
        Board.cs
        BoardColumn.cs
        DefaultColumns.cs
      Cards/
        Card.cs
        CardVote.cs
      Presence/
        Participant.cs
        ParticipantConnection.cs
      Common/
        Slug.cs
        ParticipantKeyFactory.cs
    RetroBoard.Application/
      RetroBoard.Application.csproj
      DependencyInjection.cs
      Common/
        Behaviors/
          ValidationBehavior.cs
          LoggingBehavior.cs
        Abstractions/
          IBoardDbContext.cs
          IClock.cs
        Exceptions/
          NotFoundException.cs
          ConflictException.cs
        Dtos/
          BoardDto.cs
          BoardSummaryDto.cs
          ColumnDto.cs
          CardDto.cs
          ParticipantDto.cs
          VoteResultDto.cs
      Boards/
        Commands/
          CreateBoard/CreateBoardCommand.cs
          CreateBoard/CreateBoardCommandHandler.cs
          CreateBoard/CreateBoardCommandValidator.cs
          ImportBoard/ImportBoardCommand.cs
          ImportBoard/ImportBoardCommandHandler.cs
          ImportBoard/ImportBoardCommandValidator.cs
        Queries/
          GetBoard/GetBoardQuery.cs
          GetBoard/GetBoardQueryHandler.cs
          ListBoards/ListBoardsQuery.cs
          ListBoards/ListBoardsQueryHandler.cs
          BoardExists/BoardExistsQuery.cs
          BoardExists/BoardExistsQueryHandler.cs
      Cards/
        Commands/
          AddCard/AddCardCommand.cs
          AddCard/AddCardCommandHandler.cs
          AddCard/AddCardCommandValidator.cs
          DeleteCard/DeleteCardCommand.cs
          DeleteCard/DeleteCardCommandHandler.cs
          CastVote/CastVoteCommand.cs
          CastVote/CastVoteCommandHandler.cs
          CastVote/CastVoteCommandValidator.cs
        Notifications/
          CardAddedNotification.cs
          CardDeletedNotification.cs
          VoteCastNotification.cs
      Presence/
        Commands/
          JoinBoard/JoinBoardCommand.cs
          JoinBoard/JoinBoardCommandHandler.cs
          LeaveBoard/LeaveBoardCommand.cs
          LeaveBoard/LeaveBoardCommandHandler.cs
          RefreshPresence/RefreshPresenceCommand.cs
          RefreshPresence/RefreshPresenceCommandHandler.cs
          SweepStalePresence/SweepStalePresenceCommand.cs
          SweepStalePresence/SweepStalePresenceCommandHandler.cs
        Notifications/
          PresenceChangedNotification.cs
    RetroBoard.Infrastructure/
      RetroBoard.Infrastructure.csproj
      DependencyInjection.cs
      Persistence/
        BoardDbContext.cs
        Configurations/
          BoardConfiguration.cs
          BoardColumnConfiguration.cs
          CardConfiguration.cs
          CardVoteConfiguration.cs
          ParticipantConfiguration.cs
          ParticipantConnectionConfiguration.cs
        Migrations/  (generated)
      Time/
        SystemClock.cs
    RetroBoard.Api/
      RetroBoard.Api.csproj
      Program.cs
      appsettings.json
      appsettings.Development.json
      Controllers/
        BoardsController.cs
        CardsController.cs
        VotesController.cs
      Hubs/
        BoardHub.cs
        BoardHubClient.cs            (server→client typed interface)
      Realtime/
        CardAddedNotificationHandler.cs
        CardDeletedNotificationHandler.cs
        VoteCastNotificationHandler.cs
        PresenceChangedNotificationHandler.cs
      BackgroundServices/
        PresenceSweeperService.cs
  tests/
    RetroBoard.Domain.Tests/
      RetroBoard.Domain.Tests.csproj
      Common/
        SlugTests.cs
        ParticipantKeyFactoryTests.cs
    RetroBoard.Application.Tests/
      RetroBoard.Application.Tests.csproj
      TestSupport/
        TestDb.cs
        FakeClock.cs
      Boards/...           (one test file per handler)
      Cards/...
      Presence/...
    RetroBoard.Api.Tests/
      RetroBoard.Api.Tests.csproj
      TestSupport/
        PostgresFixture.cs
        ApiFactory.cs
        SignalRTestClient.cs
      Endpoints/
        BoardsEndpointsTests.cs
        CardsEndpointsTests.cs
        VotesEndpointsTests.cs
      Realtime/
        SignalRBroadcastTests.cs
        VoteConcurrencyTests.cs
        PresenceTests.cs

docker-compose.yml         (repo root, Postgres only)
```

---

## Task 1: Solution scaffolding

**Files:**
- Create: `server/RetroBoard.sln` and project skeletons listed above
- Create: `.gitignore` additions for `bin/`, `obj/`, `*.user`

- [ ] **Step 1: Create solution and projects**

```bash
cd /Users/davitjanjalia/Desktop/retro-board
mkdir -p server/src server/tests
cd server
dotnet new sln -n RetroBoard
dotnet new classlib -n RetroBoard.Domain -o src/RetroBoard.Domain -f net9.0
dotnet new classlib -n RetroBoard.Application -o src/RetroBoard.Application -f net9.0
dotnet new classlib -n RetroBoard.Infrastructure -o src/RetroBoard.Infrastructure -f net9.0
dotnet new webapi -n RetroBoard.Api -o src/RetroBoard.Api -f net9.0 --no-openapi false
dotnet new xunit -n RetroBoard.Domain.Tests -o tests/RetroBoard.Domain.Tests -f net9.0
dotnet new xunit -n RetroBoard.Application.Tests -o tests/RetroBoard.Application.Tests -f net9.0
dotnet new xunit -n RetroBoard.Api.Tests -o tests/RetroBoard.Api.Tests -f net9.0
dotnet sln add src/**/*.csproj tests/**/*.csproj
```

- [ ] **Step 2: Wire project references**

```bash
cd /Users/davitjanjalia/Desktop/retro-board/server
dotnet add src/RetroBoard.Application reference src/RetroBoard.Domain
dotnet add src/RetroBoard.Infrastructure reference src/RetroBoard.Application src/RetroBoard.Domain
dotnet add src/RetroBoard.Api reference src/RetroBoard.Application src/RetroBoard.Infrastructure
dotnet add tests/RetroBoard.Domain.Tests reference src/RetroBoard.Domain
dotnet add tests/RetroBoard.Application.Tests reference src/RetroBoard.Application src/RetroBoard.Infrastructure
dotnet add tests/RetroBoard.Api.Tests reference src/RetroBoard.Api src/RetroBoard.Infrastructure
```

- [ ] **Step 3: Add NuGet packages**

```bash
cd /Users/davitjanjalia/Desktop/retro-board/server

# Application
dotnet add src/RetroBoard.Application package MediatR --version 12.4.1
dotnet add src/RetroBoard.Application package FluentValidation --version 11.10.0
dotnet add src/RetroBoard.Application package FluentValidation.DependencyInjectionExtensions --version 11.10.0
dotnet add src/RetroBoard.Application package Microsoft.Extensions.DependencyInjection.Abstractions --version 9.0.0
dotnet add src/RetroBoard.Application package Microsoft.Extensions.Logging.Abstractions --version 9.0.0
dotnet add src/RetroBoard.Application package Microsoft.EntityFrameworkCore --version 9.0.0

# Infrastructure
dotnet add src/RetroBoard.Infrastructure package Microsoft.EntityFrameworkCore --version 9.0.0
dotnet add src/RetroBoard.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL --version 9.0.2
dotnet add src/RetroBoard.Infrastructure package Microsoft.EntityFrameworkCore.Design --version 9.0.0

# Api
dotnet add src/RetroBoard.Api package MediatR --version 12.4.1
dotnet add src/RetroBoard.Api package FluentValidation.AspNetCore --version 11.3.0
dotnet add src/RetroBoard.Api package Microsoft.EntityFrameworkCore.Design --version 9.0.0
dotnet add src/RetroBoard.Api package Swashbuckle.AspNetCore --version 7.2.0

# Tests
dotnet add tests/RetroBoard.Application.Tests package Microsoft.EntityFrameworkCore.InMemory --version 9.0.0
dotnet add tests/RetroBoard.Application.Tests package FluentAssertions --version 6.12.1
dotnet add tests/RetroBoard.Application.Tests package NSubstitute --version 5.3.0

dotnet add tests/RetroBoard.Api.Tests package Microsoft.AspNetCore.Mvc.Testing --version 9.0.0
dotnet add tests/RetroBoard.Api.Tests package Microsoft.AspNetCore.SignalR.Client --version 9.0.0
dotnet add tests/RetroBoard.Api.Tests package Testcontainers.PostgreSql --version 4.0.0
dotnet add tests/RetroBoard.Api.Tests package FluentAssertions --version 6.12.1

dotnet add tests/RetroBoard.Domain.Tests package FluentAssertions --version 6.12.1
```

- [ ] **Step 4: Verify the solution builds**

```bash
cd /Users/davitjanjalia/Desktop/retro-board/server
dotnet build
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 5: Commit**

```bash
cd /Users/davitjanjalia/Desktop/retro-board
git add server/RetroBoard.sln server/src server/tests
git commit -m "chore(server): scaffold .NET solution with projects and packages"
```

---

## Task 2: Domain — Slug

**Files:**
- Create: `server/src/RetroBoard.Domain/Common/Slug.cs`
- Create: `server/tests/RetroBoard.Domain.Tests/Common/SlugTests.cs`

- [ ] **Step 1: Write failing tests**

`server/tests/RetroBoard.Domain.Tests/Common/SlugTests.cs`:

```csharp
using FluentAssertions;
using RetroBoard.Domain.Common;
using Xunit;

namespace RetroBoard.Domain.Tests.Common;

public class SlugTests
{
    [Theory]
    [InlineData("Sprint 12 Retro", "sprint-12-retro")]
    [InlineData("  Hello   World  ", "hello-world")]
    [InlineData("Q1!! Review??", "q1-review")]
    [InlineData("--leading-and-trailing--", "leading-and-trailing")]
    [InlineData("MIXED Case 123", "mixed-case-123")]
    public void Create_returns_lowercase_hyphenated_slug(string input, string expected)
    {
        Slug.Create(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void Create_throws_when_input_yields_empty_slug(string input)
    {
        var act = () => Slug.Create(input);
        act.Should().Throw<ArgumentException>().WithMessage("*invalid board name*");
    }
}
```

- [ ] **Step 2: Run tests; expect FAIL (type not found)**

```bash
cd /Users/davitjanjalia/Desktop/retro-board/server
dotnet test tests/RetroBoard.Domain.Tests
```

- [ ] **Step 3: Implement `Slug`**

`server/src/RetroBoard.Domain/Common/Slug.cs`:

```csharp
using System.Text.RegularExpressions;

namespace RetroBoard.Domain.Common;

public static class Slug
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex Invalid = new(@"[^a-z0-9-]", RegexOptions.Compiled);
    private static readonly Regex MultiHyphen = new(@"-+", RegexOptions.Compiled);

    public static string Create(string name)
    {
        var s = (name ?? string.Empty).Trim().ToLowerInvariant();
        s = Whitespace.Replace(s, "-");
        s = Invalid.Replace(s, "");
        s = MultiHyphen.Replace(s, "-").Trim('-');
        if (string.IsNullOrEmpty(s))
            throw new ArgumentException("invalid board name", nameof(name));
        return s;
    }
}
```

- [ ] **Step 4: Run tests; expect PASS**

```bash
cd /Users/davitjanjalia/Desktop/retro-board/server
dotnet test tests/RetroBoard.Domain.Tests
```

- [ ] **Step 5: Commit**

```bash
cd /Users/davitjanjalia/Desktop/retro-board
git add server/src/RetroBoard.Domain/Common/Slug.cs server/tests/RetroBoard.Domain.Tests/Common/SlugTests.cs
git commit -m "feat(domain): add Slug.Create with validation"
```

---

## Task 3: Domain — ParticipantKeyFactory & DefaultColumns

**Files:**
- Create: `server/src/RetroBoard.Domain/Common/ParticipantKeyFactory.cs`
- Create: `server/src/RetroBoard.Domain/Boards/DefaultColumns.cs`
- Create: `server/tests/RetroBoard.Domain.Tests/Common/ParticipantKeyFactoryTests.cs`

- [ ] **Step 1: Write failing tests**

`server/tests/RetroBoard.Domain.Tests/Common/ParticipantKeyFactoryTests.cs`:

```csharp
using FluentAssertions;
using RetroBoard.Domain.Common;
using Xunit;

namespace RetroBoard.Domain.Tests.Common;

public class ParticipantKeyFactoryTests
{
    [Theory]
    [InlineData("Alice", "alice")]
    [InlineData("Bob Smith", "bob-smith")]
    public void Create_returns_slug_when_input_slugifiable(string input, string expected)
    {
        ParticipantKeyFactory.Create(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("!!!", "anon-3")]
    [InlineData("", "anon-0")]
    public void Create_falls_back_to_anon_with_length_when_slug_empty(string input, string expected)
    {
        ParticipantKeyFactory.Create(input).Should().Be(expected);
    }
}
```

- [ ] **Step 2: Run tests; expect FAIL**

```bash
dotnet test server/tests/RetroBoard.Domain.Tests
```

- [ ] **Step 3: Implement `ParticipantKeyFactory` and `DefaultColumns`**

`server/src/RetroBoard.Domain/Common/ParticipantKeyFactory.cs`:

```csharp
namespace RetroBoard.Domain.Common;

public static class ParticipantKeyFactory
{
    public static string Create(string displayName)
    {
        var input = displayName ?? string.Empty;
        try
        {
            return Slug.Create(input);
        }
        catch (ArgumentException)
        {
            return $"anon-{input.Length}";
        }
    }
}
```

`server/src/RetroBoard.Domain/Boards/DefaultColumns.cs`:

```csharp
namespace RetroBoard.Domain.Boards;

public static class DefaultColumns
{
    public static readonly IReadOnlyList<string> Titles = new[]
    {
        "What went well",
        "What didn't go well",
        "Shoutouts",
        "Action items",
    };
}
```

- [ ] **Step 4: Run tests; expect PASS**

```bash
dotnet test server/tests/RetroBoard.Domain.Tests
```

- [ ] **Step 5: Commit**

```bash
cd /Users/davitjanjalia/Desktop/retro-board
git add server/src/RetroBoard.Domain/Common/ParticipantKeyFactory.cs \
        server/src/RetroBoard.Domain/Boards/DefaultColumns.cs \
        server/tests/RetroBoard.Domain.Tests/Common/ParticipantKeyFactoryTests.cs
git commit -m "feat(domain): add ParticipantKeyFactory and DefaultColumns"
```

---

## Task 4: Domain entities

**Files:**
- Create: `server/src/RetroBoard.Domain/Boards/Board.cs`
- Create: `server/src/RetroBoard.Domain/Boards/BoardColumn.cs`
- Create: `server/src/RetroBoard.Domain/Cards/Card.cs`
- Create: `server/src/RetroBoard.Domain/Cards/CardVote.cs`
- Create: `server/src/RetroBoard.Domain/Presence/Participant.cs`
- Create: `server/src/RetroBoard.Domain/Presence/ParticipantConnection.cs`

- [ ] **Step 1: Write entities** (no test — these are pure data shapes used by EF and tested through repository tests)

`server/src/RetroBoard.Domain/Boards/Board.cs`:

```csharp
using RetroBoard.Domain.Cards;
using RetroBoard.Domain.Presence;

namespace RetroBoard.Domain.Boards;

public class Board
{
    public long Id { get; set; }
    public string Slug { get; set; } = default!;
    public string Name { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }

    public List<BoardColumn> Columns { get; set; } = new();
    public List<Card> Cards { get; set; } = new();
    public List<Participant> Participants { get; set; } = new();
}
```

`server/src/RetroBoard.Domain/Boards/BoardColumn.cs`:

```csharp
namespace RetroBoard.Domain.Boards;

public class BoardColumn
{
    public long Id { get; set; }
    public long BoardId { get; set; }
    public int Position { get; set; }
    public string Title { get; set; } = default!;
}
```

`server/src/RetroBoard.Domain/Cards/Card.cs`:

```csharp
namespace RetroBoard.Domain.Cards;

public class Card
{
    public Guid Id { get; set; }
    public long BoardId { get; set; }
    public long ColumnId { get; set; }
    public string Text { get; set; } = default!;
    public string Author { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }

    public List<CardVote> Votes { get; set; } = new();
}
```

`server/src/RetroBoard.Domain/Cards/CardVote.cs`:

```csharp
namespace RetroBoard.Domain.Cards;

public class CardVote
{
    public Guid CardId { get; set; }
    public string SessionId { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}
```

`server/src/RetroBoard.Domain/Presence/Participant.cs`:

```csharp
namespace RetroBoard.Domain.Presence;

public class Participant
{
    public long Id { get; set; }
    public long BoardId { get; set; }
    public string ParticipantKey { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }

    public List<ParticipantConnection> Connections { get; set; } = new();
}
```

`server/src/RetroBoard.Domain/Presence/ParticipantConnection.cs`:

```csharp
namespace RetroBoard.Domain.Presence;

public class ParticipantConnection
{
    public long ParticipantId { get; set; }
    public string ConnectionId { get; set; } = default!;
    public string SessionId { get; set; } = default!;
    public DateTimeOffset ConnectedAt { get; set; }
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build server/src/RetroBoard.Domain
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add server/src/RetroBoard.Domain
git commit -m "feat(domain): add core entities"
```

---

## Task 5: Application — Common abstractions, exceptions, DTOs

**Files:**
- Create: `server/src/RetroBoard.Application/Common/Abstractions/IBoardDbContext.cs`
- Create: `server/src/RetroBoard.Application/Common/Abstractions/IClock.cs`
- Create: `server/src/RetroBoard.Application/Common/Exceptions/NotFoundException.cs`
- Create: `server/src/RetroBoard.Application/Common/Exceptions/ConflictException.cs`
- Create: `server/src/RetroBoard.Application/Common/Dtos/BoardSummaryDto.cs`
- Create: `server/src/RetroBoard.Application/Common/Dtos/ColumnDto.cs`
- Create: `server/src/RetroBoard.Application/Common/Dtos/CardDto.cs`
- Create: `server/src/RetroBoard.Application/Common/Dtos/BoardDto.cs`
- Create: `server/src/RetroBoard.Application/Common/Dtos/ParticipantDto.cs`
- Create: `server/src/RetroBoard.Application/Common/Dtos/VoteResultDto.cs`

- [ ] **Step 1: Write abstractions and DTOs**

`server/src/RetroBoard.Application/Common/Abstractions/IBoardDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using RetroBoard.Domain.Boards;
using RetroBoard.Domain.Cards;
using RetroBoard.Domain.Presence;

namespace RetroBoard.Application.Common.Abstractions;

public interface IBoardDbContext
{
    DbSet<Board> Boards { get; }
    DbSet<BoardColumn> BoardColumns { get; }
    DbSet<Card> Cards { get; }
    DbSet<CardVote> CardVotes { get; }
    DbSet<Participant> Participants { get; }
    DbSet<ParticipantConnection> ParticipantConnections { get; }

    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

`server/src/RetroBoard.Application/Common/Abstractions/IClock.cs`:

```csharp
namespace RetroBoard.Application.Common.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
```

`server/src/RetroBoard.Application/Common/Exceptions/NotFoundException.cs`:

```csharp
namespace RetroBoard.Application.Common.Exceptions;

public class NotFoundException(string message) : Exception(message);
```

`server/src/RetroBoard.Application/Common/Exceptions/ConflictException.cs`:

```csharp
namespace RetroBoard.Application.Common.Exceptions;

public class ConflictException(string message) : Exception(message);
```

`server/src/RetroBoard.Application/Common/Dtos/ColumnDto.cs`:

```csharp
namespace RetroBoard.Application.Common.Dtos;

public record ColumnDto(long Id, int Position, string Title);
```

`server/src/RetroBoard.Application/Common/Dtos/CardDto.cs`:

```csharp
namespace RetroBoard.Application.Common.Dtos;

public record CardDto(
    Guid Id,
    long ColumnId,
    int ColumnIndex,
    string Text,
    string Author,
    DateTimeOffset CreatedAt,
    int Votes);
```

`server/src/RetroBoard.Application/Common/Dtos/BoardSummaryDto.cs`:

```csharp
namespace RetroBoard.Application.Common.Dtos;

public record BoardSummaryDto(long Id, string Slug, string Name, DateTimeOffset CreatedAt);
```

`server/src/RetroBoard.Application/Common/Dtos/BoardDto.cs`:

```csharp
namespace RetroBoard.Application.Common.Dtos;

public record BoardDto(
    long Id,
    string Slug,
    string Name,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ColumnDto> Columns,
    IReadOnlyList<CardDto> Cards);
```

`server/src/RetroBoard.Application/Common/Dtos/ParticipantDto.cs`:

```csharp
namespace RetroBoard.Application.Common.Dtos;

public record ParticipantDto(
    string ParticipantKey,
    string DisplayName,
    DateTimeOffset JoinedAt,
    DateTimeOffset LastSeenAt,
    int ConnectionCount);
```

`server/src/RetroBoard.Application/Common/Dtos/VoteResultDto.cs`:

```csharp
namespace RetroBoard.Application.Common.Dtos;

public record VoteResultDto(bool Voted, int Votes);
```

- [ ] **Step 2: Build**

```bash
dotnet build server/src/RetroBoard.Application
```

Expected: success.

- [ ] **Step 3: Commit**

```bash
git add server/src/RetroBoard.Application/Common
git commit -m "feat(application): add abstractions, exceptions, and DTOs"
```

---

## Task 6: Application — Pipeline behaviors + DI extension

**Files:**
- Create: `server/src/RetroBoard.Application/Common/Behaviors/ValidationBehavior.cs`
- Create: `server/src/RetroBoard.Application/Common/Behaviors/LoggingBehavior.cs`
- Create: `server/src/RetroBoard.Application/DependencyInjection.cs`

- [ ] **Step 1: Write `ValidationBehavior`**

`server/src/RetroBoard.Application/Common/Behaviors/ValidationBehavior.cs`:

```csharp
using FluentValidation;
using MediatR;

namespace RetroBoard.Application.Common.Behaviors;

public class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var failures = (await Task.WhenAll(
                    validators.Select(v => v.ValidateAsync(context, cancellationToken))))
                .SelectMany(r => r.Errors)
                .Where(f => f != null)
                .ToList();
            if (failures.Count != 0)
                throw new ValidationException(failures);
        }
        return await next();
    }
}
```

- [ ] **Step 2: Write `LoggingBehavior`**

`server/src/RetroBoard.Application/Common/Behaviors/LoggingBehavior.cs`:

```csharp
using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace RetroBoard.Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await next();
            logger.LogInformation("{Request} handled in {Elapsed} ms", name, sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Request} failed after {Elapsed} ms", name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
```

- [ ] **Step 3: Write DI extension**

`server/src/RetroBoard.Application/DependencyInjection.cs`:

```csharp
using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RetroBoard.Application.Common.Behaviors;

namespace RetroBoard.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        return services;
    }
}
```

- [ ] **Step 4: Build**

```bash
dotnet build server/src/RetroBoard.Application
```

- [ ] **Step 5: Commit**

```bash
git add server/src/RetroBoard.Application/Common/Behaviors \
        server/src/RetroBoard.Application/DependencyInjection.cs
git commit -m "feat(application): add MediatR pipeline behaviors and DI"
```

---

## Task 7: Infrastructure — DbContext + entity configurations

**Files:**
- Create: `server/src/RetroBoard.Infrastructure/Persistence/BoardDbContext.cs`
- Create: `server/src/RetroBoard.Infrastructure/Persistence/Configurations/BoardConfiguration.cs`
- Create: `server/src/RetroBoard.Infrastructure/Persistence/Configurations/BoardColumnConfiguration.cs`
- Create: `server/src/RetroBoard.Infrastructure/Persistence/Configurations/CardConfiguration.cs`
- Create: `server/src/RetroBoard.Infrastructure/Persistence/Configurations/CardVoteConfiguration.cs`
- Create: `server/src/RetroBoard.Infrastructure/Persistence/Configurations/ParticipantConfiguration.cs`
- Create: `server/src/RetroBoard.Infrastructure/Persistence/Configurations/ParticipantConnectionConfiguration.cs`
- Create: `server/src/RetroBoard.Infrastructure/Time/SystemClock.cs`
- Create: `server/src/RetroBoard.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Write `BoardDbContext`**

`server/src/RetroBoard.Infrastructure/Persistence/BoardDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Domain.Boards;
using RetroBoard.Domain.Cards;
using RetroBoard.Domain.Presence;

namespace RetroBoard.Infrastructure.Persistence;

public class BoardDbContext(DbContextOptions<BoardDbContext> options)
    : DbContext(options), IBoardDbContext
{
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardColumn> BoardColumns => Set<BoardColumn>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CardVote> CardVotes => Set<CardVote>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<ParticipantConnection> ParticipantConnections => Set<ParticipantConnection>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(BoardDbContext).Assembly);
    }
}
```

- [ ] **Step 2: Write entity configurations**

`server/src/RetroBoard.Infrastructure/Persistence/Configurations/BoardConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetroBoard.Domain.Boards;

namespace RetroBoard.Infrastructure.Persistence.Configurations;

public class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> b)
    {
        b.ToTable("boards");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityAlwaysColumn().HasColumnName("id");
        b.Property(x => x.Slug).HasColumnName("slug").IsRequired();
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasMany(x => x.Columns).WithOne().HasForeignKey(c => c.BoardId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Cards).WithOne().HasForeignKey(c => c.BoardId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Participants).WithOne().HasForeignKey(p => p.BoardId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

`server/src/RetroBoard.Infrastructure/Persistence/Configurations/BoardColumnConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetroBoard.Domain.Boards;

namespace RetroBoard.Infrastructure.Persistence.Configurations;

public class BoardColumnConfiguration : IEntityTypeConfiguration<BoardColumn>
{
    public void Configure(EntityTypeBuilder<BoardColumn> b)
    {
        b.ToTable("board_columns");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityAlwaysColumn().HasColumnName("id");
        b.Property(x => x.BoardId).HasColumnName("board_id");
        b.Property(x => x.Position).HasColumnName("position");
        b.Property(x => x.Title).HasColumnName("title").IsRequired();
        b.HasIndex(x => new { x.BoardId, x.Position }).IsUnique();
    }
}
```

`server/src/RetroBoard.Infrastructure/Persistence/Configurations/CardConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetroBoard.Domain.Cards;

namespace RetroBoard.Infrastructure.Persistence.Configurations;

public class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> b)
    {
        b.ToTable("cards");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.BoardId).HasColumnName("board_id");
        b.Property(x => x.ColumnId).HasColumnName("column_id");
        b.Property(x => x.Text).HasColumnName("text").IsRequired();
        b.Property(x => x.Author).HasColumnName("author").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        b.HasIndex(x => new { x.BoardId, x.CreatedAt });
        b.HasOne<Domain.Boards.BoardColumn>().WithMany().HasForeignKey(x => x.ColumnId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Votes).WithOne().HasForeignKey(v => v.CardId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

`server/src/RetroBoard.Infrastructure/Persistence/Configurations/CardVoteConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetroBoard.Domain.Cards;

namespace RetroBoard.Infrastructure.Persistence.Configurations;

public class CardVoteConfiguration : IEntityTypeConfiguration<CardVote>
{
    public void Configure(EntityTypeBuilder<CardVote> b)
    {
        b.ToTable("card_votes");
        b.HasKey(x => new { x.CardId, x.SessionId });
        b.Property(x => x.CardId).HasColumnName("card_id");
        b.Property(x => x.SessionId).HasColumnName("session_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
    }
}
```

`server/src/RetroBoard.Infrastructure/Persistence/Configurations/ParticipantConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetroBoard.Domain.Presence;

namespace RetroBoard.Infrastructure.Persistence.Configurations;

public class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> b)
    {
        b.ToTable("participants");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityAlwaysColumn().HasColumnName("id");
        b.Property(x => x.BoardId).HasColumnName("board_id");
        b.Property(x => x.ParticipantKey).HasColumnName("participant_key").IsRequired();
        b.Property(x => x.DisplayName).HasColumnName("display_name").IsRequired();
        b.Property(x => x.JoinedAt).HasColumnName("joined_at").HasDefaultValueSql("now()");
        b.Property(x => x.LastSeenAt).HasColumnName("last_seen_at").HasDefaultValueSql("now()");
        b.HasIndex(x => new { x.BoardId, x.ParticipantKey }).IsUnique();
        b.HasMany(x => x.Connections).WithOne().HasForeignKey(c => c.ParticipantId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

`server/src/RetroBoard.Infrastructure/Persistence/Configurations/ParticipantConnectionConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetroBoard.Domain.Presence;

namespace RetroBoard.Infrastructure.Persistence.Configurations;

public class ParticipantConnectionConfiguration : IEntityTypeConfiguration<ParticipantConnection>
{
    public void Configure(EntityTypeBuilder<ParticipantConnection> b)
    {
        b.ToTable("participant_connections");
        b.HasKey(x => new { x.ParticipantId, x.ConnectionId });
        b.Property(x => x.ParticipantId).HasColumnName("participant_id");
        b.Property(x => x.ConnectionId).HasColumnName("connection_id");
        b.Property(x => x.SessionId).HasColumnName("session_id").IsRequired();
        b.Property(x => x.ConnectedAt).HasColumnName("connected_at").HasDefaultValueSql("now()");
        b.HasIndex(x => x.ConnectionId);
    }
}
```

- [ ] **Step 3: Write `SystemClock` and Infrastructure DI**

`server/src/RetroBoard.Infrastructure/Time/SystemClock.cs`:

```csharp
using RetroBoard.Application.Common.Abstractions;

namespace RetroBoard.Infrastructure.Time;

public class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
```

`server/src/RetroBoard.Infrastructure/DependencyInjection.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Infrastructure.Persistence;
using RetroBoard.Infrastructure.Time;

namespace RetroBoard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        var connStr = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres not configured");
        services.AddDbContext<BoardDbContext>(opt => opt.UseNpgsql(connStr));
        services.AddScoped<IBoardDbContext>(sp => sp.GetRequiredService<BoardDbContext>());
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }
}
```

- [ ] **Step 4: Build**

```bash
dotnet build server/src/RetroBoard.Infrastructure
```

- [ ] **Step 5: Commit**

```bash
git add server/src/RetroBoard.Infrastructure
git commit -m "feat(infrastructure): add BoardDbContext, configurations, and SystemClock"
```

---

## Task 8: Infrastructure — Initial migration

**Files:**
- Create: `server/src/RetroBoard.Infrastructure/Migrations/<timestamp>_InitialCreate.cs` (generated)
- Modify: `server/src/RetroBoard.Api/Program.cs` (briefly, to make `dotnet ef` work — full Program.cs comes later)

- [ ] **Step 1: Add a minimal `Program.cs` so `dotnet ef` can build the host**

Replace `server/src/RetroBoard.Api/Program.cs` with:

```csharp
using RetroBoard.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
var app = builder.Build();
app.MapGet("/", () => "ok");
app.Run();
```

Add to `server/src/RetroBoard.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=retroboard;Username=retro;Password=retro"
  }
}
```

- [ ] **Step 2: Install `dotnet-ef` and create the migration**

```bash
cd /Users/davitjanjalia/Desktop/retro-board/server
dotnet tool install --global dotnet-ef --version 9.0.0 || dotnet tool update --global dotnet-ef --version 9.0.0
dotnet ef migrations add InitialCreate \
  --project src/RetroBoard.Infrastructure \
  --startup-project src/RetroBoard.Api \
  --output-dir Migrations
```

Expected: `Done. To undo this action, use 'ef migrations remove'`. Files appear under `server/src/RetroBoard.Infrastructure/Migrations/`.

- [ ] **Step 3: Inspect the generated migration**

Open `server/src/RetroBoard.Infrastructure/Migrations/<timestamp>_InitialCreate.cs`. Verify it creates `boards`, `board_columns`, `cards`, `card_votes`, `participants`, `participant_connections` tables with the expected columns/indexes/FKs. No code change expected.

- [ ] **Step 4: Commit**

```bash
git add server/src/RetroBoard.Api/Program.cs \
        server/src/RetroBoard.Api/appsettings.Development.json \
        server/src/RetroBoard.Infrastructure/Migrations
git commit -m "feat(infrastructure): add initial EF migration"
```

---

## Task 9: docker-compose for local Postgres + smoke-test migration

**Files:**
- Create: `docker-compose.yml` (repo root)

- [ ] **Step 1: Write docker-compose.yml**

`/Users/davitjanjalia/Desktop/retro-board/docker-compose.yml`:

```yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_USER: retro
      POSTGRES_PASSWORD: retro
      POSTGRES_DB: retroboard
    ports:
      - "5432:5432"
    volumes:
      - retroboard-pg:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U retro -d retroboard"]
      interval: 5s
      timeout: 5s
      retries: 10
volumes:
  retroboard-pg:
```

- [ ] **Step 2: Bring up Postgres and apply the migration**

```bash
cd /Users/davitjanjalia/Desktop/retro-board
docker compose up -d postgres
# Wait for healthy
until docker compose exec -T postgres pg_isready -U retro -d retroboard; do sleep 1; done
cd server
dotnet ef database update \
  --project src/RetroBoard.Infrastructure \
  --startup-project src/RetroBoard.Api
```

Expected: `Done.` and the database tables exist.

- [ ] **Step 3: Verify schema**

```bash
docker compose exec -T postgres psql -U retro -d retroboard -c "\dt"
```

Expected: `boards`, `board_columns`, `cards`, `card_votes`, `participants`, `participant_connections`, `__EFMigrationsHistory`.

- [ ] **Step 4: Commit**

```bash
cd /Users/davitjanjalia/Desktop/retro-board
git add docker-compose.yml
git commit -m "chore: add docker-compose for local Postgres"
```

---

## Task 10: Application test support

**Files:**
- Create: `server/tests/RetroBoard.Application.Tests/TestSupport/TestDb.cs`
- Create: `server/tests/RetroBoard.Application.Tests/TestSupport/FakeClock.cs`

- [ ] **Step 1: Write `TestDb` (in-memory EF context factory)**

`server/tests/RetroBoard.Application.Tests/TestSupport/TestDb.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using RetroBoard.Infrastructure.Persistence;

namespace RetroBoard.Application.Tests.TestSupport;

public static class TestDb
{
    public static BoardDbContext NewInMemory()
    {
        var opts = new DbContextOptionsBuilder<BoardDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BoardDbContext(opts);
    }
}
```

- [ ] **Step 2: Write `FakeClock`**

`server/tests/RetroBoard.Application.Tests/TestSupport/FakeClock.cs`:

```csharp
using RetroBoard.Application.Common.Abstractions;

namespace RetroBoard.Application.Tests.TestSupport;

public class FakeClock(DateTimeOffset start) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = start;
    public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
}
```

- [ ] **Step 3: Build**

```bash
dotnet build server/tests/RetroBoard.Application.Tests
```

- [ ] **Step 4: Commit**

```bash
git add server/tests/RetroBoard.Application.Tests/TestSupport
git commit -m "test(application): add test db and fake clock helpers"
```

---

## Task 11: Boards — CreateBoardCommand (TDD)

**Files:**
- Create: `server/src/RetroBoard.Application/Boards/Commands/CreateBoard/CreateBoardCommand.cs`
- Create: `server/src/RetroBoard.Application/Boards/Commands/CreateBoard/CreateBoardCommandValidator.cs`
- Create: `server/src/RetroBoard.Application/Boards/Commands/CreateBoard/CreateBoardCommandHandler.cs`
- Create: `server/tests/RetroBoard.Application.Tests/Boards/CreateBoardCommandHandlerTests.cs`

- [ ] **Step 1: Write failing test**

`server/tests/RetroBoard.Application.Tests/Boards/CreateBoardCommandHandlerTests.cs`:

```csharp
using FluentAssertions;
using RetroBoard.Application.Boards.Commands.CreateBoard;
using RetroBoard.Application.Common.Exceptions;
using RetroBoard.Application.Tests.TestSupport;
using Xunit;

namespace RetroBoard.Application.Tests.Boards;

public class CreateBoardCommandHandlerTests
{
    [Fact]
    public async Task Creates_board_with_default_columns_when_none_supplied()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-04-25T10:00:00Z"));
        var handler = new CreateBoardCommandHandler(db, clock);

        var dto = await handler.Handle(new CreateBoardCommand("Sprint 12 Retro", null), default);

        dto.Slug.Should().Be("sprint-12-retro");
        dto.Name.Should().Be("Sprint 12 Retro");
        dto.Columns.Select(c => c.Title).Should().Equal(
            "What went well", "What didn't go well", "Shoutouts", "Action items");
        dto.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task Throws_conflict_when_slug_exists()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var handler = new CreateBoardCommandHandler(db, clock);
        await handler.Handle(new CreateBoardCommand("Retro 1", null), default);

        var act = () => handler.Handle(new CreateBoardCommand("Retro 1", null), default);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Uses_supplied_columns_when_non_empty()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var handler = new CreateBoardCommandHandler(db, clock);

        var dto = await handler.Handle(new CreateBoardCommand("Custom", new[] { "A", "B" }), default);

        dto.Columns.Select(c => c.Title).Should().Equal("A", "B");
        dto.Columns.Select(c => c.Position).Should().Equal(0, 1);
    }
}
```

- [ ] **Step 2: Run test; expect FAIL (types not defined)**

```bash
dotnet test server/tests/RetroBoard.Application.Tests
```

- [ ] **Step 3: Implement command, validator, handler**

`server/src/RetroBoard.Application/Boards/Commands/CreateBoard/CreateBoardCommand.cs`:

```csharp
using MediatR;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Boards.Commands.CreateBoard;

public record CreateBoardCommand(string Name, IReadOnlyList<string>? Columns) : IRequest<BoardDto>;
```

`server/src/RetroBoard.Application/Boards/Commands/CreateBoard/CreateBoardCommandValidator.cs`:

```csharp
using FluentValidation;

namespace RetroBoard.Application.Boards.Commands.CreateBoard;

public class CreateBoardCommandValidator : AbstractValidator<CreateBoardCommand>
{
    public CreateBoardCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleForEach(x => x.Columns).NotEmpty().MaximumLength(80)
            .When(x => x.Columns is not null);
        RuleFor(x => x.Columns).Must(c => c is null || c.Count <= 12)
            .WithMessage("at most 12 columns");
    }
}
```

`server/src/RetroBoard.Application/Boards/Commands/CreateBoard/CreateBoardCommandHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Common.Dtos;
using RetroBoard.Application.Common.Exceptions;
using RetroBoard.Domain.Boards;
using RetroBoard.Domain.Common;

namespace RetroBoard.Application.Boards.Commands.CreateBoard;

public class CreateBoardCommandHandler(IBoardDbContext db, IClock clock)
    : IRequestHandler<CreateBoardCommand, BoardDto>
{
    public async Task<BoardDto> Handle(CreateBoardCommand cmd, CancellationToken ct)
    {
        var slug = Slug.Create(cmd.Name);
        if (await db.Boards.AnyAsync(b => b.Slug == slug, ct))
            throw new ConflictException("Board name already taken, choose another.");

        var titles = (cmd.Columns is { Count: > 0 } ? cmd.Columns : DefaultColumns.Titles).ToList();
        var board = new Board
        {
            Slug = slug,
            Name = cmd.Name,
            CreatedAt = clock.UtcNow,
            Columns = titles.Select((t, i) => new BoardColumn { Position = i, Title = t }).ToList(),
        };
        db.Boards.Add(board);
        await db.SaveChangesAsync(ct);

        return new BoardDto(
            board.Id, board.Slug, board.Name, board.CreatedAt,
            board.Columns
                .OrderBy(c => c.Position)
                .Select(c => new ColumnDto(c.Id, c.Position, c.Title))
                .ToList(),
            Array.Empty<CardDto>());
    }
}
```

- [ ] **Step 4: Run test; expect PASS**

```bash
dotnet test server/tests/RetroBoard.Application.Tests
```

- [ ] **Step 5: Commit**

```bash
git add server/src/RetroBoard.Application/Boards/Commands/CreateBoard \
        server/tests/RetroBoard.Application.Tests/Boards/CreateBoardCommandHandlerTests.cs
git commit -m "feat(application): add CreateBoardCommand with validator and handler"
```

---

## Task 12: Boards — GetBoardQuery / ListBoardsQuery / BoardExistsQuery

**Files:**
- Create: `server/src/RetroBoard.Application/Boards/Queries/GetBoard/GetBoardQuery.cs`
- Create: `server/src/RetroBoard.Application/Boards/Queries/GetBoard/GetBoardQueryHandler.cs`
- Create: `server/src/RetroBoard.Application/Boards/Queries/ListBoards/ListBoardsQuery.cs`
- Create: `server/src/RetroBoard.Application/Boards/Queries/ListBoards/ListBoardsQueryHandler.cs`
- Create: `server/src/RetroBoard.Application/Boards/Queries/BoardExists/BoardExistsQuery.cs`
- Create: `server/src/RetroBoard.Application/Boards/Queries/BoardExists/BoardExistsQueryHandler.cs`
- Create: `server/tests/RetroBoard.Application.Tests/Boards/BoardQueriesTests.cs`

- [ ] **Step 1: Write failing tests**

`server/tests/RetroBoard.Application.Tests/Boards/BoardQueriesTests.cs`:

```csharp
using FluentAssertions;
using RetroBoard.Application.Boards.Commands.CreateBoard;
using RetroBoard.Application.Boards.Queries.BoardExists;
using RetroBoard.Application.Boards.Queries.GetBoard;
using RetroBoard.Application.Boards.Queries.ListBoards;
using RetroBoard.Application.Tests.TestSupport;
using Xunit;

namespace RetroBoard.Application.Tests.Boards;

public class BoardQueriesTests
{
    [Fact]
    public async Task GetBoard_returns_null_when_missing()
    {
        var db = TestDb.NewInMemory();
        var dto = await new GetBoardQueryHandler(db).Handle(new GetBoardQuery("no-such"), default);
        dto.Should().BeNull();
    }

    [Fact]
    public async Task GetBoard_returns_board_with_columns_and_cards()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-04-25T10:00:00Z"));
        await new CreateBoardCommandHandler(db, clock).Handle(new CreateBoardCommand("Retro", null), default);

        var dto = await new GetBoardQueryHandler(db).Handle(new GetBoardQuery("retro"), default);

        dto.Should().NotBeNull();
        dto!.Slug.Should().Be("retro");
        dto.Columns.Should().HaveCount(4);
        dto.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task ListBoards_returns_summaries_newest_first()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-04-25T10:00:00Z"));
        var create = new CreateBoardCommandHandler(db, clock);
        await create.Handle(new CreateBoardCommand("First", null), default);
        clock.Advance(TimeSpan.FromHours(1));
        await create.Handle(new CreateBoardCommand("Second", null), default);

        var list = await new ListBoardsQueryHandler(db).Handle(new ListBoardsQuery(), default);
        list.Select(x => x.Slug).Should().Equal("second", "first");
    }

    [Fact]
    public async Task BoardExists_returns_true_then_false()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        await new CreateBoardCommandHandler(db, clock).Handle(new CreateBoardCommand("Retro", null), default);

        (await new BoardExistsQueryHandler(db).Handle(new BoardExistsQuery("retro"), default)).Should().BeTrue();
        (await new BoardExistsQueryHandler(db).Handle(new BoardExistsQuery("nope"), default)).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run; expect FAIL**

```bash
dotnet test server/tests/RetroBoard.Application.Tests
```

- [ ] **Step 3: Implement queries**

`server/src/RetroBoard.Application/Boards/Queries/GetBoard/GetBoardQuery.cs`:

```csharp
using MediatR;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Boards.Queries.GetBoard;

public record GetBoardQuery(string Slug) : IRequest<BoardDto?>;
```

`server/src/RetroBoard.Application/Boards/Queries/GetBoard/GetBoardQueryHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Boards.Queries.GetBoard;

public class GetBoardQueryHandler(IBoardDbContext db) : IRequestHandler<GetBoardQuery, BoardDto?>
{
    public async Task<BoardDto?> Handle(GetBoardQuery q, CancellationToken ct)
    {
        var board = await db.Boards
            .AsNoTracking()
            .Include(b => b.Columns)
            .Include(b => b.Cards).ThenInclude(c => c.Votes)
            .FirstOrDefaultAsync(b => b.Slug == q.Slug, ct);
        if (board is null) return null;

        var columns = board.Columns.OrderBy(c => c.Position).ToList();
        var positionByColumnId = columns.ToDictionary(c => c.Id, c => c.Position);

        return new BoardDto(
            board.Id, board.Slug, board.Name, board.CreatedAt,
            columns.Select(c => new ColumnDto(c.Id, c.Position, c.Title)).ToList(),
            board.Cards
                .OrderBy(c => c.CreatedAt)
                .Select(c => new CardDto(
                    c.Id, c.ColumnId,
                    positionByColumnId.TryGetValue(c.ColumnId, out var p) ? p : -1,
                    c.Text, c.Author, c.CreatedAt, c.Votes.Count))
                .ToList());
    }
}
```

`server/src/RetroBoard.Application/Boards/Queries/ListBoards/ListBoardsQuery.cs`:

```csharp
using MediatR;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Boards.Queries.ListBoards;

public record ListBoardsQuery : IRequest<IReadOnlyList<BoardSummaryDto>>;
```

`server/src/RetroBoard.Application/Boards/Queries/ListBoards/ListBoardsQueryHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Boards.Queries.ListBoards;

public class ListBoardsQueryHandler(IBoardDbContext db)
    : IRequestHandler<ListBoardsQuery, IReadOnlyList<BoardSummaryDto>>
{
    public async Task<IReadOnlyList<BoardSummaryDto>> Handle(ListBoardsQuery q, CancellationToken ct)
    {
        return await db.Boards
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BoardSummaryDto(b.Id, b.Slug, b.Name, b.CreatedAt))
            .ToListAsync(ct);
    }
}
```

`server/src/RetroBoard.Application/Boards/Queries/BoardExists/BoardExistsQuery.cs`:

```csharp
using MediatR;

namespace RetroBoard.Application.Boards.Queries.BoardExists;

public record BoardExistsQuery(string Slug) : IRequest<bool>;
```

`server/src/RetroBoard.Application/Boards/Queries/BoardExists/BoardExistsQueryHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;

namespace RetroBoard.Application.Boards.Queries.BoardExists;

public class BoardExistsQueryHandler(IBoardDbContext db) : IRequestHandler<BoardExistsQuery, bool>
{
    public Task<bool> Handle(BoardExistsQuery q, CancellationToken ct) =>
        db.Boards.AsNoTracking().AnyAsync(b => b.Slug == q.Slug, ct);
}
```

- [ ] **Step 4: Run; expect PASS**

```bash
dotnet test server/tests/RetroBoard.Application.Tests
```

- [ ] **Step 5: Commit**

```bash
git add server/src/RetroBoard.Application/Boards/Queries server/tests/RetroBoard.Application.Tests/Boards/BoardQueriesTests.cs
git commit -m "feat(application): add board queries (Get, List, Exists)"
```

---

## Task 13: Boards — ImportBoardCommand

**Files:**
- Create: `server/src/RetroBoard.Application/Boards/Commands/ImportBoard/ImportBoardCommand.cs`
- Create: `server/src/RetroBoard.Application/Boards/Commands/ImportBoard/ImportBoardCommandValidator.cs`
- Create: `server/src/RetroBoard.Application/Boards/Commands/ImportBoard/ImportBoardCommandHandler.cs`
- Create: `server/tests/RetroBoard.Application.Tests/Boards/ImportBoardCommandHandlerTests.cs`

- [ ] **Step 1: Write failing test**

`server/tests/RetroBoard.Application.Tests/Boards/ImportBoardCommandHandlerTests.cs`:

```csharp
using FluentAssertions;
using RetroBoard.Application.Boards.Commands.ImportBoard;
using RetroBoard.Application.Tests.TestSupport;
using Xunit;

namespace RetroBoard.Application.Tests.Boards;

public class ImportBoardCommandHandlerTests
{
    [Fact]
    public async Task Imports_board_with_columns_and_cards()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-04-25T10:00:00Z"));
        var handler = new ImportBoardCommandHandler(db, clock);

        var dto = await handler.Handle(new ImportBoardCommand(
            "Imported",
            new[] { "Plus", "Minus" },
            new[]
            {
                new ImportedCard("Yay", "Alice", 0, 3),
                new ImportedCard("Boo", "", 1, 0),
            }), default);

        dto.Slug.Should().Be("imported");
        dto.Columns.Should().HaveCount(2);
        dto.Cards.Should().HaveCount(2);
        dto.Cards.Single(c => c.ColumnIndex == 1).Author.Should().Be("Anonymous");
    }
}
```

- [ ] **Step 2: Run; expect FAIL**

- [ ] **Step 3: Implement**

`server/src/RetroBoard.Application/Boards/Commands/ImportBoard/ImportBoardCommand.cs`:

```csharp
using MediatR;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Boards.Commands.ImportBoard;

public record ImportedCard(string Text, string Author, int ColumnIndex, int Votes);

public record ImportBoardCommand(
    string Name,
    IReadOnlyList<string> Columns,
    IReadOnlyList<ImportedCard> Cards) : IRequest<BoardDto>;
```

`server/src/RetroBoard.Application/Boards/Commands/ImportBoard/ImportBoardCommandValidator.cs`:

```csharp
using FluentValidation;

namespace RetroBoard.Application.Boards.Commands.ImportBoard;

public class ImportBoardCommandValidator : AbstractValidator<ImportBoardCommand>
{
    public ImportBoardCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Columns).NotEmpty();
        RuleForEach(x => x.Columns).NotEmpty().MaximumLength(80);
        RuleForEach(x => x.Cards).ChildRules(c =>
        {
            c.RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
            c.RuleFor(x => x.ColumnIndex).GreaterThanOrEqualTo(0);
            c.RuleFor(x => x.Votes).GreaterThanOrEqualTo(0);
        });
    }
}
```

`server/src/RetroBoard.Application/Boards/Commands/ImportBoard/ImportBoardCommandHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Common.Dtos;
using RetroBoard.Application.Common.Exceptions;
using RetroBoard.Domain.Boards;
using RetroBoard.Domain.Cards;
using RetroBoard.Domain.Common;

namespace RetroBoard.Application.Boards.Commands.ImportBoard;

public class ImportBoardCommandHandler(IBoardDbContext db, IClock clock)
    : IRequestHandler<ImportBoardCommand, BoardDto>
{
    public async Task<BoardDto> Handle(ImportBoardCommand cmd, CancellationToken ct)
    {
        var slug = Slug.Create(cmd.Name);
        if (await db.Boards.AnyAsync(b => b.Slug == slug, ct))
            throw new ConflictException("Board name already taken, choose another.");

        var columns = cmd.Columns.Select((t, i) => new BoardColumn { Position = i, Title = t }).ToList();
        var board = new Board
        {
            Slug = slug,
            Name = cmd.Name,
            CreatedAt = clock.UtcNow,
            Columns = columns,
        };
        db.Boards.Add(board);
        await db.SaveChangesAsync(ct);  // assigns column IDs

        var cards = new List<Card>();
        foreach (var c in cmd.Cards)
        {
            if (c.ColumnIndex < 0 || c.ColumnIndex >= columns.Count) continue;
            var card = new Card
            {
                Id = Guid.NewGuid(),
                BoardId = board.Id,
                ColumnId = columns[c.ColumnIndex].Id,
                Text = c.Text,
                Author = string.IsNullOrWhiteSpace(c.Author) ? "Anonymous" : c.Author,
                CreatedAt = clock.UtcNow,
            };
            for (var v = 0; v < c.Votes; v++)
                card.Votes.Add(new CardVote { CardId = card.Id, SessionId = $"import-{Guid.NewGuid()}", CreatedAt = clock.UtcNow });
            cards.Add(card);
        }
        db.Cards.AddRange(cards);
        await db.SaveChangesAsync(ct);

        var positionByColumnId = columns.ToDictionary(c => c.Id, c => c.Position);
        return new BoardDto(
            board.Id, board.Slug, board.Name, board.CreatedAt,
            columns.Select(c => new ColumnDto(c.Id, c.Position, c.Title)).ToList(),
            cards.Select(c => new CardDto(c.Id, c.ColumnId, positionByColumnId[c.ColumnId],
                c.Text, c.Author, c.CreatedAt, c.Votes.Count)).ToList());
    }
}
```

- [ ] **Step 4: Run; expect PASS**

```bash
dotnet test server/tests/RetroBoard.Application.Tests
```

- [ ] **Step 5: Commit**

```bash
git add server/src/RetroBoard.Application/Boards/Commands/ImportBoard \
        server/tests/RetroBoard.Application.Tests/Boards/ImportBoardCommandHandlerTests.cs
git commit -m "feat(application): add ImportBoardCommand"
```

---

## Task 14: Cards — Notifications

**Files:**
- Create: `server/src/RetroBoard.Application/Cards/Notifications/CardAddedNotification.cs`
- Create: `server/src/RetroBoard.Application/Cards/Notifications/CardDeletedNotification.cs`
- Create: `server/src/RetroBoard.Application/Cards/Notifications/VoteCastNotification.cs`
- Create: `server/src/RetroBoard.Application/Presence/Notifications/PresenceChangedNotification.cs`

- [ ] **Step 1: Write notifications (one file each)**

`server/src/RetroBoard.Application/Cards/Notifications/CardAddedNotification.cs`:

```csharp
using MediatR;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Cards.Notifications;

public record CardAddedNotification(string Slug, CardDto Card) : INotification;
```

`server/src/RetroBoard.Application/Cards/Notifications/CardDeletedNotification.cs`:

```csharp
using MediatR;

namespace RetroBoard.Application.Cards.Notifications;

public record CardDeletedNotification(string Slug, Guid CardId) : INotification;
```

`server/src/RetroBoard.Application/Cards/Notifications/VoteCastNotification.cs`:

```csharp
using MediatR;

namespace RetroBoard.Application.Cards.Notifications;

public record VoteCastNotification(string Slug, Guid CardId, int Votes, string SessionId) : INotification;
```

`server/src/RetroBoard.Application/Presence/Notifications/PresenceChangedNotification.cs`:

```csharp
using MediatR;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Presence.Notifications;

public record PresenceChangedNotification(string Slug, IReadOnlyList<ParticipantDto> Participants) : INotification;
```

- [ ] **Step 2: Build**

```bash
dotnet build server/src/RetroBoard.Application
```

- [ ] **Step 3: Commit**

```bash
git add server/src/RetroBoard.Application/Cards/Notifications \
        server/src/RetroBoard.Application/Presence/Notifications
git commit -m "feat(application): add MediatR notifications"
```

---

## Task 15: Cards — AddCardCommand

**Files:**
- Create: `server/src/RetroBoard.Application/Cards/Commands/AddCard/AddCardCommand.cs`
- Create: `server/src/RetroBoard.Application/Cards/Commands/AddCard/AddCardCommandValidator.cs`
- Create: `server/src/RetroBoard.Application/Cards/Commands/AddCard/AddCardCommandHandler.cs`
- Create: `server/tests/RetroBoard.Application.Tests/Cards/AddCardCommandHandlerTests.cs`

- [ ] **Step 1: Failing test**

`server/tests/RetroBoard.Application.Tests/Cards/AddCardCommandHandlerTests.cs`:

```csharp
using FluentAssertions;
using MediatR;
using NSubstitute;
using RetroBoard.Application.Boards.Commands.CreateBoard;
using RetroBoard.Application.Cards.Commands.AddCard;
using RetroBoard.Application.Cards.Notifications;
using RetroBoard.Application.Common.Exceptions;
using RetroBoard.Application.Tests.TestSupport;
using Xunit;

namespace RetroBoard.Application.Tests.Cards;

public class AddCardCommandHandlerTests
{
    [Fact]
    public async Task Adds_card_returns_dto_publishes_notification()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-04-25T10:00:00Z"));
        var board = await new CreateBoardCommandHandler(db, clock)
            .Handle(new CreateBoardCommand("Retro", null), default);
        var publisher = Substitute.For<IPublisher>();
        var handler = new AddCardCommandHandler(db, clock, publisher);

        var card = await handler.Handle(
            new AddCardCommand("retro", "Awesome", "Alice", 0), default);

        card.Text.Should().Be("Awesome");
        card.Author.Should().Be("Alice");
        card.ColumnIndex.Should().Be(0);
        card.Votes.Should().Be(0);
        await publisher.Received(1).Publish(
            Arg.Is<CardAddedNotification>(n => n.Slug == "retro" && n.Card.Id == card.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Throws_NotFound_when_board_missing()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var handler = new AddCardCommandHandler(db, clock, Substitute.For<IPublisher>());

        var act = () => handler.Handle(new AddCardCommand("nope", "x", "Alice", 0), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Throws_NotFound_when_column_index_out_of_range()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        await new CreateBoardCommandHandler(db, clock).Handle(new CreateBoardCommand("Retro", null), default);
        var handler = new AddCardCommandHandler(db, clock, Substitute.For<IPublisher>());

        var act = () => handler.Handle(new AddCardCommand("retro", "x", "Alice", 99), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

- [ ] **Step 2: Run; expect FAIL**

- [ ] **Step 3: Implement**

`server/src/RetroBoard.Application/Cards/Commands/AddCard/AddCardCommand.cs`:

```csharp
using MediatR;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Cards.Commands.AddCard;

public record AddCardCommand(string Slug, string Text, string Author, int ColumnIndex) : IRequest<CardDto>;
```

`server/src/RetroBoard.Application/Cards/Commands/AddCard/AddCardCommandValidator.cs`:

```csharp
using FluentValidation;

namespace RetroBoard.Application.Cards.Commands.AddCard;

public class AddCardCommandValidator : AbstractValidator<AddCardCommand>
{
    public AddCardCommandValidator()
    {
        RuleFor(x => x.Slug).NotEmpty();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Author).NotEmpty().MaximumLength(120);
        RuleFor(x => x.ColumnIndex).GreaterThanOrEqualTo(0);
    }
}
```

`server/src/RetroBoard.Application/Cards/Commands/AddCard/AddCardCommandHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Cards.Notifications;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Common.Dtos;
using RetroBoard.Application.Common.Exceptions;
using RetroBoard.Domain.Cards;

namespace RetroBoard.Application.Cards.Commands.AddCard;

public class AddCardCommandHandler(IBoardDbContext db, IClock clock, IPublisher publisher)
    : IRequestHandler<AddCardCommand, CardDto>
{
    public async Task<CardDto> Handle(AddCardCommand cmd, CancellationToken ct)
    {
        var board = await db.Boards
            .Include(b => b.Columns)
            .FirstOrDefaultAsync(b => b.Slug == cmd.Slug, ct)
            ?? throw new NotFoundException($"Board '{cmd.Slug}' not found");
        var column = board.Columns.FirstOrDefault(c => c.Position == cmd.ColumnIndex)
            ?? throw new NotFoundException($"Column index {cmd.ColumnIndex} out of range");

        var card = new Card
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            ColumnId = column.Id,
            Text = cmd.Text,
            Author = cmd.Author,
            CreatedAt = clock.UtcNow,
        };
        db.Cards.Add(card);
        await db.SaveChangesAsync(ct);

        var dto = new CardDto(card.Id, card.ColumnId, column.Position,
            card.Text, card.Author, card.CreatedAt, 0);
        await publisher.Publish(new CardAddedNotification(cmd.Slug, dto), ct);
        return dto;
    }
}
```

- [ ] **Step 4: Run; expect PASS**

```bash
dotnet test server/tests/RetroBoard.Application.Tests
```

- [ ] **Step 5: Commit**

```bash
git add server/src/RetroBoard.Application/Cards/Commands/AddCard \
        server/tests/RetroBoard.Application.Tests/Cards/AddCardCommandHandlerTests.cs
git commit -m "feat(application): add AddCardCommand with notification"
```

---

## Task 16: Cards — DeleteCardCommand

**Files:**
- Create: `server/src/RetroBoard.Application/Cards/Commands/DeleteCard/DeleteCardCommand.cs`
- Create: `server/src/RetroBoard.Application/Cards/Commands/DeleteCard/DeleteCardCommandHandler.cs`
- Create: `server/tests/RetroBoard.Application.Tests/Cards/DeleteCardCommandHandlerTests.cs`

- [ ] **Step 1: Failing test**

`server/tests/RetroBoard.Application.Tests/Cards/DeleteCardCommandHandlerTests.cs`:

```csharp
using FluentAssertions;
using MediatR;
using NSubstitute;
using RetroBoard.Application.Boards.Commands.CreateBoard;
using RetroBoard.Application.Cards.Commands.AddCard;
using RetroBoard.Application.Cards.Commands.DeleteCard;
using RetroBoard.Application.Cards.Notifications;
using RetroBoard.Application.Common.Exceptions;
using RetroBoard.Application.Tests.TestSupport;
using Xunit;

namespace RetroBoard.Application.Tests.Cards;

public class DeleteCardCommandHandlerTests
{
    [Fact]
    public async Task Deletes_card_publishes_notification()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        await new CreateBoardCommandHandler(db, clock).Handle(new CreateBoardCommand("Retro", null), default);
        var publisher = Substitute.For<IPublisher>();
        var card = await new AddCardCommandHandler(db, clock, publisher)
            .Handle(new AddCardCommand("retro", "x", "Alice", 0), default);

        await new DeleteCardCommandHandler(db, publisher)
            .Handle(new DeleteCardCommand("retro", card.Id), default);

        db.Cards.Should().BeEmpty();
        await publisher.Received(1).Publish(
            Arg.Is<CardDeletedNotification>(n => n.Slug == "retro" && n.CardId == card.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Throws_NotFound_when_card_missing()
    {
        var db = TestDb.NewInMemory();
        var act = () => new DeleteCardCommandHandler(db, Substitute.For<IPublisher>())
            .Handle(new DeleteCardCommand("retro", Guid.NewGuid()), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

- [ ] **Step 2: Run; expect FAIL**

- [ ] **Step 3: Implement**

`server/src/RetroBoard.Application/Cards/Commands/DeleteCard/DeleteCardCommand.cs`:

```csharp
using MediatR;

namespace RetroBoard.Application.Cards.Commands.DeleteCard;

public record DeleteCardCommand(string Slug, Guid CardId) : IRequest<Unit>;
```

`server/src/RetroBoard.Application/Cards/Commands/DeleteCard/DeleteCardCommandHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Cards.Notifications;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Common.Exceptions;

namespace RetroBoard.Application.Cards.Commands.DeleteCard;

public class DeleteCardCommandHandler(IBoardDbContext db, IPublisher publisher)
    : IRequestHandler<DeleteCardCommand, Unit>
{
    public async Task<Unit> Handle(DeleteCardCommand cmd, CancellationToken ct)
    {
        var card = await db.Cards.FirstOrDefaultAsync(
            c => c.Id == cmd.CardId && c.BoardId == db.Boards
                .Where(b => b.Slug == cmd.Slug).Select(b => b.Id).FirstOrDefault(), ct)
            ?? throw new NotFoundException($"Card {cmd.CardId} not found on board '{cmd.Slug}'");
        db.Cards.Remove(card);
        await db.SaveChangesAsync(ct);
        await publisher.Publish(new CardDeletedNotification(cmd.Slug, cmd.CardId), ct);
        return Unit.Value;
    }
}
```

- [ ] **Step 4: Run; expect PASS**

```bash
dotnet test server/tests/RetroBoard.Application.Tests
```

- [ ] **Step 5: Commit**

```bash
git add server/src/RetroBoard.Application/Cards/Commands/DeleteCard \
        server/tests/RetroBoard.Application.Tests/Cards/DeleteCardCommandHandlerTests.cs
git commit -m "feat(application): add DeleteCardCommand"
```

---

## Task 17: Cards — CastVoteCommand

> Note: handler-layer test uses EF in-memory, which does **not** enforce the composite PK uniqueness in the same way Postgres does. Idempotency is verified at the handler level via an explicit existence check; the database-level concurrency guarantee is verified in the integration tests (Task 25).

**Files:**
- Create: `server/src/RetroBoard.Application/Cards/Commands/CastVote/CastVoteCommand.cs`
- Create: `server/src/RetroBoard.Application/Cards/Commands/CastVote/CastVoteCommandValidator.cs`
- Create: `server/src/RetroBoard.Application/Cards/Commands/CastVote/CastVoteCommandHandler.cs`
- Create: `server/tests/RetroBoard.Application.Tests/Cards/CastVoteCommandHandlerTests.cs`

- [ ] **Step 1: Failing test**

`server/tests/RetroBoard.Application.Tests/Cards/CastVoteCommandHandlerTests.cs`:

```csharp
using FluentAssertions;
using MediatR;
using NSubstitute;
using RetroBoard.Application.Boards.Commands.CreateBoard;
using RetroBoard.Application.Cards.Commands.AddCard;
using RetroBoard.Application.Cards.Commands.CastVote;
using RetroBoard.Application.Cards.Notifications;
using RetroBoard.Application.Common.Exceptions;
using RetroBoard.Application.Tests.TestSupport;
using Xunit;

namespace RetroBoard.Application.Tests.Cards;

public class CastVoteCommandHandlerTests
{
    private async Task<Guid> SeedCardAsync(TestSupport.FakeClock clock, Infrastructure.Persistence.BoardDbContext db)
    {
        await new CreateBoardCommandHandler(db, clock).Handle(new CreateBoardCommand("Retro", null), default);
        var card = await new AddCardCommandHandler(db, clock, Substitute.For<IPublisher>())
            .Handle(new AddCardCommand("retro", "x", "Alice", 0), default);
        return card.Id;
    }

    [Fact]
    public async Task First_vote_returns_voted_true_count_one()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var cardId = await SeedCardAsync(clock, db);
        var pub = Substitute.For<IPublisher>();
        var result = await new CastVoteCommandHandler(db, pub, clock)
            .Handle(new CastVoteCommand("retro", cardId, "sess-1"), default);
        result.Voted.Should().BeTrue();
        result.Votes.Should().Be(1);
        await pub.Received(1).Publish(Arg.Any<VoteCastNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Repeat_vote_same_session_returns_voted_false_count_unchanged()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var cardId = await SeedCardAsync(clock, db);
        var handler = new CastVoteCommandHandler(db, Substitute.For<IPublisher>(), clock);
        await handler.Handle(new CastVoteCommand("retro", cardId, "sess-1"), default);
        var second = await handler.Handle(new CastVoteCommand("retro", cardId, "sess-1"), default);
        second.Voted.Should().BeFalse();
        second.Votes.Should().Be(1);
    }

    [Fact]
    public async Task Throws_NotFound_when_card_missing()
    {
        var db = TestDb.NewInMemory();
        var act = () => new CastVoteCommandHandler(db, Substitute.For<IPublisher>(), new FakeClock(DateTimeOffset.UtcNow))
            .Handle(new CastVoteCommand("retro", Guid.NewGuid(), "s"), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

- [ ] **Step 2: Run; expect FAIL**

- [ ] **Step 3: Implement**

`server/src/RetroBoard.Application/Cards/Commands/CastVote/CastVoteCommand.cs`:

```csharp
using MediatR;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Cards.Commands.CastVote;

public record CastVoteCommand(string Slug, Guid CardId, string SessionId) : IRequest<VoteResultDto>;
```

`server/src/RetroBoard.Application/Cards/Commands/CastVote/CastVoteCommandValidator.cs`:

```csharp
using FluentValidation;

namespace RetroBoard.Application.Cards.Commands.CastVote;

public class CastVoteCommandValidator : AbstractValidator<CastVoteCommand>
{
    public CastVoteCommandValidator()
    {
        RuleFor(x => x.Slug).NotEmpty();
        RuleFor(x => x.CardId).NotEqual(Guid.Empty);
        RuleFor(x => x.SessionId).NotEmpty().MaximumLength(120);
    }
}
```

`server/src/RetroBoard.Application/Cards/Commands/CastVote/CastVoteCommandHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Cards.Notifications;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Common.Dtos;
using RetroBoard.Application.Common.Exceptions;
using RetroBoard.Domain.Cards;

namespace RetroBoard.Application.Cards.Commands.CastVote;

public class CastVoteCommandHandler(IBoardDbContext db, IPublisher publisher, IClock clock)
    : IRequestHandler<CastVoteCommand, VoteResultDto>
{
    public async Task<VoteResultDto> Handle(CastVoteCommand cmd, CancellationToken ct)
    {
        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == cmd.CardId &&
                db.Boards.Any(b => b.Slug == cmd.Slug && b.Id == c.BoardId), ct)
            ?? throw new NotFoundException("Card not found");

        var alreadyVoted = await db.CardVotes
            .AnyAsync(v => v.CardId == cmd.CardId && v.SessionId == cmd.SessionId, ct);

        var inserted = false;
        if (!alreadyVoted)
        {
            try
            {
                db.CardVotes.Add(new CardVote
                {
                    CardId = cmd.CardId,
                    SessionId = cmd.SessionId,
                    CreatedAt = clock.UtcNow,
                });
                await db.SaveChangesAsync(ct);
                inserted = true;
            }
            catch (DbUpdateException)
            {
                // Concurrent insert from same session — composite PK rejected. Treat as already voted.
                inserted = false;
            }
        }

        var votes = await db.CardVotes.CountAsync(v => v.CardId == cmd.CardId, ct);
        if (inserted)
            await publisher.Publish(new VoteCastNotification(cmd.Slug, cmd.CardId, votes, cmd.SessionId), ct);
        return new VoteResultDto(inserted, votes);
    }
}
```

- [ ] **Step 4: Run; expect PASS**

```bash
dotnet test server/tests/RetroBoard.Application.Tests
```

- [ ] **Step 5: Commit**

```bash
git add server/src/RetroBoard.Application/Cards/Commands/CastVote \
        server/tests/RetroBoard.Application.Tests/Cards/CastVoteCommandHandlerTests.cs
git commit -m "feat(application): add CastVoteCommand with idempotent insert"
```

---

## Task 18: Presence — JoinBoard / LeaveBoard / Refresh / Sweep

**Files:**
- Create: `server/src/RetroBoard.Application/Presence/Commands/JoinBoard/JoinBoardCommand.cs`
- Create: `server/src/RetroBoard.Application/Presence/Commands/JoinBoard/JoinBoardCommandHandler.cs`
- Create: `server/src/RetroBoard.Application/Presence/Commands/LeaveBoard/LeaveBoardCommand.cs`
- Create: `server/src/RetroBoard.Application/Presence/Commands/LeaveBoard/LeaveBoardCommandHandler.cs`
- Create: `server/src/RetroBoard.Application/Presence/Commands/RefreshPresence/RefreshPresenceCommand.cs`
- Create: `server/src/RetroBoard.Application/Presence/Commands/RefreshPresence/RefreshPresenceCommandHandler.cs`
- Create: `server/src/RetroBoard.Application/Presence/Commands/SweepStalePresence/SweepStalePresenceCommand.cs`
- Create: `server/src/RetroBoard.Application/Presence/Commands/SweepStalePresence/SweepStalePresenceCommandHandler.cs`
- Create: `server/tests/RetroBoard.Application.Tests/Presence/PresenceCommandsTests.cs`

- [ ] **Step 1: Failing test**

`server/tests/RetroBoard.Application.Tests/Presence/PresenceCommandsTests.cs`:

```csharp
using FluentAssertions;
using MediatR;
using NSubstitute;
using RetroBoard.Application.Boards.Commands.CreateBoard;
using RetroBoard.Application.Presence.Commands.JoinBoard;
using RetroBoard.Application.Presence.Commands.LeaveBoard;
using RetroBoard.Application.Presence.Commands.RefreshPresence;
using RetroBoard.Application.Presence.Commands.SweepStalePresence;
using RetroBoard.Application.Presence.Notifications;
using RetroBoard.Application.Tests.TestSupport;
using Xunit;

namespace RetroBoard.Application.Tests.Presence;

public class PresenceCommandsTests
{
    [Fact]
    public async Task Join_creates_participant_and_connection_publishes_presence()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-04-25T10:00:00Z"));
        await new CreateBoardCommandHandler(db, clock).Handle(new CreateBoardCommand("Retro", null), default);
        var pub = Substitute.For<IPublisher>();

        var result = await new JoinBoardCommandHandler(db, clock, pub)
            .Handle(new JoinBoardCommand("retro", "conn-1", "sess-1", "Alice"), default);

        result.Participants.Should().HaveCount(1);
        result.Participants[0].DisplayName.Should().Be("Alice");
        result.Participants[0].ConnectionCount.Should().Be(1);
        await pub.Received(1).Publish(Arg.Any<PresenceChangedNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Leave_removes_connection_and_participant_when_last()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        await new CreateBoardCommandHandler(db, clock).Handle(new CreateBoardCommand("Retro", null), default);
        var pub = Substitute.For<IPublisher>();
        await new JoinBoardCommandHandler(db, clock, pub)
            .Handle(new JoinBoardCommand("retro", "conn-1", "sess-1", "Alice"), default);

        await new LeaveBoardCommandHandler(db, pub)
            .Handle(new LeaveBoardCommand("retro", "conn-1"), default);

        db.Participants.Should().BeEmpty();
        db.ParticipantConnections.Should().BeEmpty();
    }

    [Fact]
    public async Task Refresh_updates_last_seen_and_connected_at()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-04-25T10:00:00Z"));
        await new CreateBoardCommandHandler(db, clock).Handle(new CreateBoardCommand("Retro", null), default);
        await new JoinBoardCommandHandler(db, clock, Substitute.For<IPublisher>())
            .Handle(new JoinBoardCommand("retro", "conn-1", "sess-1", "Alice"), default);
        clock.Advance(TimeSpan.FromMinutes(1));

        await new RefreshPresenceCommandHandler(db, clock)
            .Handle(new RefreshPresenceCommand("conn-1"), default);

        var conn = db.ParticipantConnections.Single();
        conn.ConnectedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task Sweep_removes_stale_connections_and_participants()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-04-25T10:00:00Z"));
        await new CreateBoardCommandHandler(db, clock).Handle(new CreateBoardCommand("Retro", null), default);
        await new JoinBoardCommandHandler(db, clock, Substitute.For<IPublisher>())
            .Handle(new JoinBoardCommand("retro", "conn-1", "sess-1", "Alice"), default);
        clock.Advance(TimeSpan.FromMinutes(10));

        await new SweepStalePresenceCommandHandler(db, clock, Substitute.For<IPublisher>())
            .Handle(new SweepStalePresenceCommand(TimeSpan.FromMinutes(5)), default);

        db.Participants.Should().BeEmpty();
        db.ParticipantConnections.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run; expect FAIL**

- [ ] **Step 3: Implement**

`server/src/RetroBoard.Application/Presence/Commands/JoinBoard/JoinBoardCommand.cs`:

```csharp
using MediatR;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Presence.Commands.JoinBoard;

public record JoinBoardResult(IReadOnlyList<ParticipantDto> Participants);

public record JoinBoardCommand(string Slug, string ConnectionId, string SessionId, string DisplayName)
    : IRequest<JoinBoardResult>;
```

`server/src/RetroBoard.Application/Presence/Commands/JoinBoard/JoinBoardCommandHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Common.Dtos;
using RetroBoard.Application.Common.Exceptions;
using RetroBoard.Application.Presence.Notifications;
using RetroBoard.Domain.Common;
using RetroBoard.Domain.Presence;

namespace RetroBoard.Application.Presence.Commands.JoinBoard;

public class JoinBoardCommandHandler(IBoardDbContext db, IClock clock, IPublisher publisher)
    : IRequestHandler<JoinBoardCommand, JoinBoardResult>
{
    public async Task<JoinBoardResult> Handle(JoinBoardCommand cmd, CancellationToken ct)
    {
        var board = await db.Boards.FirstOrDefaultAsync(b => b.Slug == cmd.Slug, ct)
            ?? throw new NotFoundException($"Board '{cmd.Slug}' not found");

        var key = ParticipantKeyFactory.Create(cmd.DisplayName);
        var participant = await db.Participants
            .FirstOrDefaultAsync(p => p.BoardId == board.Id && p.ParticipantKey == key, ct);
        if (participant is null)
        {
            participant = new Participant
            {
                BoardId = board.Id,
                ParticipantKey = key,
                DisplayName = cmd.DisplayName,
                JoinedAt = clock.UtcNow,
                LastSeenAt = clock.UtcNow,
            };
            db.Participants.Add(participant);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            participant.LastSeenAt = clock.UtcNow;
            participant.DisplayName = cmd.DisplayName;
        }

        var existing = await db.ParticipantConnections
            .FirstOrDefaultAsync(c => c.ParticipantId == participant.Id && c.ConnectionId == cmd.ConnectionId, ct);
        if (existing is null)
        {
            db.ParticipantConnections.Add(new ParticipantConnection
            {
                ParticipantId = participant.Id,
                ConnectionId = cmd.ConnectionId,
                SessionId = cmd.SessionId,
                ConnectedAt = clock.UtcNow,
            });
        }
        await db.SaveChangesAsync(ct);

        var participants = await LoadParticipantsAsync(db, board.Id, ct);
        await publisher.Publish(new PresenceChangedNotification(cmd.Slug, participants), ct);
        return new JoinBoardResult(participants);
    }

    internal static async Task<IReadOnlyList<ParticipantDto>> LoadParticipantsAsync(
        IBoardDbContext db, long boardId, CancellationToken ct)
    {
        var rows = await db.Participants
            .AsNoTracking()
            .Where(p => p.BoardId == boardId)
            .Select(p => new
            {
                p.ParticipantKey,
                p.DisplayName,
                p.JoinedAt,
                p.LastSeenAt,
                ConnectionCount = p.Connections.Count,
            })
            .ToListAsync(ct);
        return rows
            .Select(r => new ParticipantDto(r.ParticipantKey, r.DisplayName, r.JoinedAt, r.LastSeenAt, r.ConnectionCount))
            .ToList();
    }
}
```

`server/src/RetroBoard.Application/Presence/Commands/LeaveBoard/LeaveBoardCommand.cs`:

```csharp
using MediatR;

namespace RetroBoard.Application.Presence.Commands.LeaveBoard;

public record LeaveBoardCommand(string Slug, string ConnectionId) : IRequest<Unit>;
```

`server/src/RetroBoard.Application/Presence/Commands/LeaveBoard/LeaveBoardCommandHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Presence.Commands.JoinBoard;
using RetroBoard.Application.Presence.Notifications;

namespace RetroBoard.Application.Presence.Commands.LeaveBoard;

public class LeaveBoardCommandHandler(IBoardDbContext db, IPublisher publisher)
    : IRequestHandler<LeaveBoardCommand, Unit>
{
    public async Task<Unit> Handle(LeaveBoardCommand cmd, CancellationToken ct)
    {
        var board = await db.Boards.FirstOrDefaultAsync(b => b.Slug == cmd.Slug, ct);
        if (board is null) return Unit.Value;

        var connections = await db.ParticipantConnections
            .Where(c => c.ConnectionId == cmd.ConnectionId &&
                db.Participants.Any(p => p.Id == c.ParticipantId && p.BoardId == board.Id))
            .ToListAsync(ct);
        if (connections.Count == 0) return Unit.Value;

        db.ParticipantConnections.RemoveRange(connections);
        await db.SaveChangesAsync(ct);

        var emptyParticipants = await db.Participants
            .Where(p => p.BoardId == board.Id && !p.Connections.Any())
            .ToListAsync(ct);
        db.Participants.RemoveRange(emptyParticipants);
        await db.SaveChangesAsync(ct);

        var participants = await JoinBoardCommandHandler.LoadParticipantsAsync(db, board.Id, ct);
        await publisher.Publish(new PresenceChangedNotification(cmd.Slug, participants), ct);
        return Unit.Value;
    }
}
```

`server/src/RetroBoard.Application/Presence/Commands/RefreshPresence/RefreshPresenceCommand.cs`:

```csharp
using MediatR;

namespace RetroBoard.Application.Presence.Commands.RefreshPresence;

public record RefreshPresenceCommand(string ConnectionId) : IRequest<Unit>;
```

`server/src/RetroBoard.Application/Presence/Commands/RefreshPresence/RefreshPresenceCommandHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;

namespace RetroBoard.Application.Presence.Commands.RefreshPresence;

public class RefreshPresenceCommandHandler(IBoardDbContext db, IClock clock)
    : IRequestHandler<RefreshPresenceCommand, Unit>
{
    public async Task<Unit> Handle(RefreshPresenceCommand cmd, CancellationToken ct)
    {
        var conn = await db.ParticipantConnections
            .FirstOrDefaultAsync(c => c.ConnectionId == cmd.ConnectionId, ct);
        if (conn is null) return Unit.Value;
        conn.ConnectedAt = clock.UtcNow;

        var participant = await db.Participants.FirstOrDefaultAsync(p => p.Id == conn.ParticipantId, ct);
        if (participant is not null) participant.LastSeenAt = clock.UtcNow;

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
```

`server/src/RetroBoard.Application/Presence/Commands/SweepStalePresence/SweepStalePresenceCommand.cs`:

```csharp
using MediatR;

namespace RetroBoard.Application.Presence.Commands.SweepStalePresence;

public record SweepStalePresenceCommand(TimeSpan StaleAfter) : IRequest<Unit>;
```

`server/src/RetroBoard.Application/Presence/Commands/SweepStalePresence/SweepStalePresenceCommandHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Presence.Commands.JoinBoard;
using RetroBoard.Application.Presence.Notifications;

namespace RetroBoard.Application.Presence.Commands.SweepStalePresence;

public class SweepStalePresenceCommandHandler(IBoardDbContext db, IClock clock, IPublisher publisher)
    : IRequestHandler<SweepStalePresenceCommand, Unit>
{
    public async Task<Unit> Handle(SweepStalePresenceCommand cmd, CancellationToken ct)
    {
        var threshold = clock.UtcNow - cmd.StaleAfter;
        var stale = await db.ParticipantConnections
            .Where(c => c.ConnectedAt < threshold)
            .ToListAsync(ct);
        if (stale.Count == 0) return Unit.Value;

        var affectedParticipantIds = stale.Select(c => c.ParticipantId).Distinct().ToList();
        db.ParticipantConnections.RemoveRange(stale);
        await db.SaveChangesAsync(ct);

        var emptyParticipants = await db.Participants
            .Where(p => affectedParticipantIds.Contains(p.Id) && !p.Connections.Any())
            .ToListAsync(ct);
        var affectedBoards = emptyParticipants.Select(p => p.BoardId).Distinct().ToList();
        if (!affectedBoards.Any())
            affectedBoards = await db.Participants
                .Where(p => affectedParticipantIds.Contains(p.Id))
                .Select(p => p.BoardId)
                .Distinct()
                .ToListAsync(ct);

        db.Participants.RemoveRange(emptyParticipants);
        await db.SaveChangesAsync(ct);

        foreach (var boardId in affectedBoards)
        {
            var slug = await db.Boards.Where(b => b.Id == boardId).Select(b => b.Slug).FirstOrDefaultAsync(ct);
            if (slug is null) continue;
            var participants = await JoinBoardCommandHandler.LoadParticipantsAsync(db, boardId, ct);
            await publisher.Publish(new PresenceChangedNotification(slug, participants), ct);
        }
        return Unit.Value;
    }
}
```

- [ ] **Step 4: Run; expect PASS**

```bash
dotnet test server/tests/RetroBoard.Application.Tests
```

- [ ] **Step 5: Commit**

```bash
git add server/src/RetroBoard.Application/Presence \
        server/tests/RetroBoard.Application.Tests/Presence
git commit -m "feat(application): add presence commands (Join/Leave/Refresh/Sweep)"
```

---

## Task 19: Api — Hub typed client interface and BoardHub

**Files:**
- Create: `server/src/RetroBoard.Api/Hubs/BoardHubClient.cs`
- Create: `server/src/RetroBoard.Api/Hubs/BoardHub.cs`

- [ ] **Step 1: Write hub client interface**

`server/src/RetroBoard.Api/Hubs/BoardHubClient.cs`:

```csharp
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Api.Hubs;

public interface IBoardHubClient
{
    Task CardAdded(CardDto card);
    Task CardDeleted(Guid cardId);
    Task VoteCast(Guid cardId, int votes, string sessionId);
    Task PresenceChanged(IReadOnlyList<ParticipantDto> participants);
}
```

- [ ] **Step 2: Write `BoardHub`**

`server/src/RetroBoard.Api/Hubs/BoardHub.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.SignalR;
using RetroBoard.Application.Common.Dtos;
using RetroBoard.Application.Presence.Commands.JoinBoard;
using RetroBoard.Application.Presence.Commands.LeaveBoard;
using RetroBoard.Application.Presence.Commands.RefreshPresence;

namespace RetroBoard.Api.Hubs;

public class BoardHub(IMediator mediator) : Hub<IBoardHubClient>
{
    private const string GroupsKey = "groups";

    public static string GroupName(string slug) => $"board:{slug}";

    public async Task<IReadOnlyList<ParticipantDto>> JoinBoard(string slug, string sessionId, string displayName)
    {
        var result = await mediator.Send(new JoinBoardCommand(slug, Context.ConnectionId, sessionId, displayName));
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(slug));
        var groups = (HashSet<string>?)Context.Items[GroupsKey] ?? new HashSet<string>();
        groups.Add(slug);
        Context.Items[GroupsKey] = groups;
        return result.Participants;
    }

    public async Task LeaveBoard(string slug)
    {
        await mediator.Send(new LeaveBoardCommand(slug, Context.ConnectionId));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(slug));
        if (Context.Items[GroupsKey] is HashSet<string> groups)
            groups.Remove(slug);
    }

    public Task Heartbeat() =>
        mediator.Send(new RefreshPresenceCommand(Context.ConnectionId));

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items[GroupsKey] is HashSet<string> groups)
        {
            foreach (var slug in groups.ToList())
                await mediator.Send(new LeaveBoardCommand(slug, Context.ConnectionId));
        }
        await base.OnDisconnectedAsync(exception);
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build server/src/RetroBoard.Api
```

- [ ] **Step 4: Commit**

```bash
git add server/src/RetroBoard.Api/Hubs
git commit -m "feat(api): add BoardHub and typed client interface"
```

---

## Task 20: Api — Notification handlers (broadcast to SignalR)

**Files:**
- Create: `server/src/RetroBoard.Api/Realtime/CardAddedNotificationHandler.cs`
- Create: `server/src/RetroBoard.Api/Realtime/CardDeletedNotificationHandler.cs`
- Create: `server/src/RetroBoard.Api/Realtime/VoteCastNotificationHandler.cs`
- Create: `server/src/RetroBoard.Api/Realtime/PresenceChangedNotificationHandler.cs`

- [ ] **Step 1: Write the four handlers**

`server/src/RetroBoard.Api/Realtime/CardAddedNotificationHandler.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.SignalR;
using RetroBoard.Api.Hubs;
using RetroBoard.Application.Cards.Notifications;

namespace RetroBoard.Api.Realtime;

public class CardAddedNotificationHandler(IHubContext<BoardHub, IBoardHubClient> hub)
    : INotificationHandler<CardAddedNotification>
{
    public Task Handle(CardAddedNotification n, CancellationToken ct) =>
        hub.Clients.Group(BoardHub.GroupName(n.Slug)).CardAdded(n.Card);
}
```

`server/src/RetroBoard.Api/Realtime/CardDeletedNotificationHandler.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.SignalR;
using RetroBoard.Api.Hubs;
using RetroBoard.Application.Cards.Notifications;

namespace RetroBoard.Api.Realtime;

public class CardDeletedNotificationHandler(IHubContext<BoardHub, IBoardHubClient> hub)
    : INotificationHandler<CardDeletedNotification>
{
    public Task Handle(CardDeletedNotification n, CancellationToken ct) =>
        hub.Clients.Group(BoardHub.GroupName(n.Slug)).CardDeleted(n.CardId);
}
```

`server/src/RetroBoard.Api/Realtime/VoteCastNotificationHandler.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.SignalR;
using RetroBoard.Api.Hubs;
using RetroBoard.Application.Cards.Notifications;

namespace RetroBoard.Api.Realtime;

public class VoteCastNotificationHandler(IHubContext<BoardHub, IBoardHubClient> hub)
    : INotificationHandler<VoteCastNotification>
{
    public Task Handle(VoteCastNotification n, CancellationToken ct) =>
        hub.Clients.Group(BoardHub.GroupName(n.Slug)).VoteCast(n.CardId, n.Votes, n.SessionId);
}
```

`server/src/RetroBoard.Api/Realtime/PresenceChangedNotificationHandler.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.SignalR;
using RetroBoard.Api.Hubs;
using RetroBoard.Application.Presence.Notifications;

namespace RetroBoard.Api.Realtime;

public class PresenceChangedNotificationHandler(IHubContext<BoardHub, IBoardHubClient> hub)
    : INotificationHandler<PresenceChangedNotification>
{
    public Task Handle(PresenceChangedNotification n, CancellationToken ct) =>
        hub.Clients.Group(BoardHub.GroupName(n.Slug)).PresenceChanged(n.Participants);
}
```

- [ ] **Step 2: Build**

```bash
dotnet build server/src/RetroBoard.Api
```

- [ ] **Step 3: Commit**

```bash
git add server/src/RetroBoard.Api/Realtime
git commit -m "feat(api): add SignalR broadcast notification handlers"
```

---

## Task 21: Api — Presence sweeper background service

**Files:**
- Create: `server/src/RetroBoard.Api/BackgroundServices/PresenceSweeperService.cs`

- [ ] **Step 1: Write background service**

`server/src/RetroBoard.Api/BackgroundServices/PresenceSweeperService.cs`:

```csharp
using MediatR;
using RetroBoard.Application.Presence.Commands.SweepStalePresence;

namespace RetroBoard.Api.BackgroundServices;

public class PresenceSweeperService(IServiceScopeFactory scopeFactory, ILogger<PresenceSweeperService> logger)
    : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(new SweepStalePresenceCommand(StaleAfter), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Presence sweep tick failed");
            }
            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build server/src/RetroBoard.Api
```

- [ ] **Step 3: Commit**

```bash
git add server/src/RetroBoard.Api/BackgroundServices
git commit -m "feat(api): add presence sweeper background service"
```

---

## Task 22: Api — Controllers + ProblemDetails mapping + Program.cs

**Files:**
- Create: `server/src/RetroBoard.Api/Controllers/BoardsController.cs`
- Create: `server/src/RetroBoard.Api/Controllers/CardsController.cs`
- Create: `server/src/RetroBoard.Api/Controllers/VotesController.cs`
- Modify: `server/src/RetroBoard.Api/Program.cs`
- Create: `server/src/RetroBoard.Api/appsettings.json`
- Modify: `server/src/RetroBoard.Api/appsettings.Development.json`

- [ ] **Step 1: Write controllers**

`server/src/RetroBoard.Api/Controllers/BoardsController.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;
using RetroBoard.Application.Boards.Commands.CreateBoard;
using RetroBoard.Application.Boards.Commands.ImportBoard;
using RetroBoard.Application.Boards.Queries.BoardExists;
using RetroBoard.Application.Boards.Queries.GetBoard;
using RetroBoard.Application.Boards.Queries.ListBoards;

namespace RetroBoard.Api.Controllers;

[ApiController]
[Route("api/boards")]
public class BoardsController(IMediator mediator) : ControllerBase
{
    public record CreateBoardRequest(string Name, IReadOnlyList<string>? Columns);
    public record ImportBoardRequest(
        string Name,
        IReadOnlyList<string> Columns,
        IReadOnlyList<ImportedCard> Cards);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBoardRequest req, CancellationToken ct)
    {
        var dto = await mediator.Send(new CreateBoardCommand(req.Name, req.Columns), ct);
        return CreatedAtAction(nameof(Get), new { slug = dto.Slug }, dto);
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportBoardRequest req, CancellationToken ct)
    {
        var dto = await mediator.Send(new ImportBoardCommand(req.Name, req.Columns, req.Cards), ct);
        return CreatedAtAction(nameof(Get), new { slug = dto.Slug }, dto);
    }

    [HttpGet]
    public Task<IReadOnlyList<Application.Common.Dtos.BoardSummaryDto>> List(CancellationToken ct) =>
        mediator.Send(new ListBoardsQuery(), ct);

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug, CancellationToken ct)
    {
        var dto = await mediator.Send(new GetBoardQuery(slug), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpHead("{slug}")]
    public async Task<IActionResult> Head(string slug, CancellationToken ct) =>
        await mediator.Send(new BoardExistsQuery(slug), ct) ? Ok() : NotFound();
}
```

`server/src/RetroBoard.Api/Controllers/CardsController.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;
using RetroBoard.Application.Cards.Commands.AddCard;
using RetroBoard.Application.Cards.Commands.DeleteCard;

namespace RetroBoard.Api.Controllers;

[ApiController]
[Route("api/boards/{slug}/cards")]
public class CardsController(IMediator mediator) : ControllerBase
{
    public record AddCardRequest(string Text, string Author, int ColumnIndex);

    [HttpPost]
    public async Task<IActionResult> Add(string slug, [FromBody] AddCardRequest req, CancellationToken ct)
    {
        var dto = await mediator.Send(new AddCardCommand(slug, req.Text, req.Author, req.ColumnIndex), ct);
        return CreatedAtAction(null, new { slug, cardId = dto.Id }, dto);
    }

    [HttpDelete("{cardId:guid}")]
    public async Task<IActionResult> Delete(string slug, Guid cardId, CancellationToken ct)
    {
        await mediator.Send(new DeleteCardCommand(slug, cardId), ct);
        return NoContent();
    }
}
```

`server/src/RetroBoard.Api/Controllers/VotesController.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;
using RetroBoard.Application.Cards.Commands.CastVote;

namespace RetroBoard.Api.Controllers;

[ApiController]
[Route("api/boards/{slug}/cards/{cardId:guid}/votes")]
public class VotesController(IMediator mediator) : ControllerBase
{
    public record CastVoteRequest(string SessionId);

    [HttpPost]
    public Task<Application.Common.Dtos.VoteResultDto> Cast(
        string slug, Guid cardId, [FromBody] CastVoteRequest req, CancellationToken ct) =>
        mediator.Send(new CastVoteCommand(slug, cardId, req.SessionId), ct);
}
```

- [ ] **Step 2: Replace `Program.cs` with the full host**

`server/src/RetroBoard.Api/Program.cs`:

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
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
```

- [ ] **Step 3: Update `appsettings.json` and `appsettings.Development.json`**

`server/src/RetroBoard.Api/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

`server/src/RetroBoard.Api/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore.SignalR": "Information"
    }
  },
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=retroboard;Username=retro;Password=retro"
  }
}
```

- [ ] **Step 4: Build and run smoke test**

```bash
cd /Users/davitjanjalia/Desktop/retro-board
docker compose up -d postgres
cd server
dotnet build
dotnet run --project src/RetroBoard.Api &
APIPID=$!
sleep 5
curl -s -X POST http://localhost:5000/api/boards \
  -H 'content-type: application/json' \
  -d '{"name":"Smoke Test"}' | jq
curl -s http://localhost:5000/api/boards | jq
kill $APIPID
```

Expected: `201 Created` for create with `slug=smoke-test`, list returns the board.
(Default port may be 5000 or 5001 depending on local config; check console output if curl returns connection refused.)

- [ ] **Step 5: Commit**

```bash
git add server/src/RetroBoard.Api
git commit -m "feat(api): add controllers, host, problem details, and CORS"
```

---

## Task 23: Api integration tests — Postgres fixture and ApiFactory

**Files:**
- Create: `server/tests/RetroBoard.Api.Tests/TestSupport/PostgresFixture.cs`
- Create: `server/tests/RetroBoard.Api.Tests/TestSupport/ApiFactory.cs`

- [ ] **Step 1: Write `PostgresFixture` (xUnit collection fixture)**

`server/tests/RetroBoard.Api.Tests/TestSupport/PostgresFixture.cs`:

```csharp
using Testcontainers.PostgreSql;
using Xunit;

namespace RetroBoard.Api.Tests.TestSupport;

public class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithUsername("retro")
        .WithPassword("retro")
        .WithDatabase("retroboard_test")
        .Build();

    public string ConnectionString => Container.GetConnectionString();

    public Task InitializeAsync() => Container.StartAsync();
    public Task DisposeAsync() => Container.StopAsync();
}

[CollectionDefinition(nameof(PostgresCollection))]
public class PostgresCollection : ICollectionFixture<PostgresFixture> { }
```

- [ ] **Step 2: Write `ApiFactory`**

`server/tests/RetroBoard.Api.Tests/TestSupport/ApiFactory.cs`:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RetroBoard.Infrastructure.Persistence;

namespace RetroBoard.Api.Tests.TestSupport;

public class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = connectionString,
            });
        });
        builder.ConfigureServices(services =>
        {
            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BoardDbContext>();
            db.Database.Migrate();
        });
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build server/tests/RetroBoard.Api.Tests
```

- [ ] **Step 4: Commit**

```bash
git add server/tests/RetroBoard.Api.Tests/TestSupport
git commit -m "test(api): add Postgres fixture and WebApplicationFactory"
```

---

## Task 24: Api integration — REST endpoint tests

**Files:**
- Create: `server/tests/RetroBoard.Api.Tests/Endpoints/BoardsEndpointsTests.cs`
- Create: `server/tests/RetroBoard.Api.Tests/Endpoints/CardsEndpointsTests.cs`

- [ ] **Step 1: Write tests**

`server/tests/RetroBoard.Api.Tests/Endpoints/BoardsEndpointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RetroBoard.Api.Tests.TestSupport;
using RetroBoard.Application.Common.Dtos;
using Xunit;

namespace RetroBoard.Api.Tests.Endpoints;

[Collection(nameof(PostgresCollection))]
public class BoardsEndpointsTests(PostgresFixture pg)
{
    [Fact]
    public async Task Create_then_get_then_list()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();

        var create = await http.PostAsJsonAsync("/api/boards", new { name = "Endpoints Retro" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await create.Content.ReadFromJsonAsync<BoardDto>())!;
        dto.Slug.Should().Be("endpoints-retro");

        var get = await http.GetFromJsonAsync<BoardDto>($"/api/boards/{dto.Slug}");
        get!.Id.Should().Be(dto.Id);

        var list = await http.GetFromJsonAsync<List<BoardSummaryDto>>("/api/boards");
        list!.Should().Contain(s => s.Slug == "endpoints-retro");
    }

    [Fact]
    public async Task Duplicate_create_returns_409()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();

        await http.PostAsJsonAsync("/api/boards", new { name = "Dup Retro" });
        var dup = await http.PostAsJsonAsync("/api/boards", new { name = "Dup Retro" });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Get_missing_returns_404()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();
        var resp = await http.GetAsync("/api/boards/no-such-board");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Head_returns_404_then_200()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();

        (await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/api/boards/headtest"))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        await http.PostAsJsonAsync("/api/boards", new { name = "HeadTest" });
        (await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/api/boards/headtest"))).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }
}
```

`server/tests/RetroBoard.Api.Tests/Endpoints/CardsEndpointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RetroBoard.Api.Tests.TestSupport;
using RetroBoard.Application.Common.Dtos;
using Xunit;

namespace RetroBoard.Api.Tests.Endpoints;

[Collection(nameof(PostgresCollection))]
public class CardsEndpointsTests(PostgresFixture pg)
{
    [Fact]
    public async Task Add_then_delete_card_then_vote()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();

        await http.PostAsJsonAsync("/api/boards", new { name = "Cards Retro" });

        var add = await http.PostAsJsonAsync(
            "/api/boards/cards-retro/cards",
            new { text = "First", author = "Alice", columnIndex = 0 });
        add.StatusCode.Should().Be(HttpStatusCode.Created);
        var card = (await add.Content.ReadFromJsonAsync<CardDto>())!;

        var vote = await http.PostAsJsonAsync(
            $"/api/boards/cards-retro/cards/{card.Id}/votes",
            new { sessionId = "sess-1" });
        vote.StatusCode.Should().Be(HttpStatusCode.OK);
        var voteResult = (await vote.Content.ReadFromJsonAsync<VoteResultDto>())!;
        voteResult.Voted.Should().BeTrue();
        voteResult.Votes.Should().Be(1);

        var del = await http.DeleteAsync($"/api/boards/cards-retro/cards/{card.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
```

- [ ] **Step 2: Run integration tests**

```bash
dotnet test server/tests/RetroBoard.Api.Tests
```

Expected: all tests PASS (Testcontainers will pull `postgres:16` on first run).

- [ ] **Step 3: Commit**

```bash
git add server/tests/RetroBoard.Api.Tests/Endpoints
git commit -m "test(api): add REST endpoint integration tests"
```

---

## Task 25: Vote concurrency integration test

**Files:**
- Create: `server/tests/RetroBoard.Api.Tests/Realtime/VoteConcurrencyTests.cs`

- [ ] **Step 1: Write the concurrency test**

`server/tests/RetroBoard.Api.Tests/Realtime/VoteConcurrencyTests.cs`:

```csharp
using System.Net.Http.Json;
using FluentAssertions;
using RetroBoard.Api.Tests.TestSupport;
using RetroBoard.Application.Common.Dtos;
using Xunit;

namespace RetroBoard.Api.Tests.Realtime;

[Collection(nameof(PostgresCollection))]
public class VoteConcurrencyTests(PostgresFixture pg)
{
    [Fact]
    public async Task Same_session_voting_in_parallel_records_one_vote()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();
        await http.PostAsJsonAsync("/api/boards", new { name = "Race Retro" });
        var add = await http.PostAsJsonAsync("/api/boards/race-retro/cards",
            new { text = "Hot", author = "A", columnIndex = 0 });
        var card = (await add.Content.ReadFromJsonAsync<CardDto>())!;

        const int parallelism = 25;
        var tasks = Enumerable.Range(0, parallelism)
            .Select(_ => http.PostAsJsonAsync(
                $"/api/boards/race-retro/cards/{card.Id}/votes",
                new { sessionId = "same-sess" }))
            .ToArray();
        await Task.WhenAll(tasks);

        var board = await http.GetFromJsonAsync<BoardDto>("/api/boards/race-retro");
        board!.Cards.Single(c => c.Id == card.Id).Votes.Should().Be(1);
    }

    [Fact]
    public async Task Different_sessions_voting_in_parallel_record_each_vote()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();
        await http.PostAsJsonAsync("/api/boards", new { name = "Race2 Retro" });
        var add = await http.PostAsJsonAsync("/api/boards/race2-retro/cards",
            new { text = "Hot", author = "A", columnIndex = 0 });
        var card = (await add.Content.ReadFromJsonAsync<CardDto>())!;

        const int parallelism = 25;
        var tasks = Enumerable.Range(0, parallelism)
            .Select(i => http.PostAsJsonAsync(
                $"/api/boards/race2-retro/cards/{card.Id}/votes",
                new { sessionId = $"sess-{i}" }))
            .ToArray();
        await Task.WhenAll(tasks);

        var board = await http.GetFromJsonAsync<BoardDto>("/api/boards/race2-retro");
        board!.Cards.Single(c => c.Id == card.Id).Votes.Should().Be(parallelism);
    }
}
```

- [ ] **Step 2: Run; expect PASS**

```bash
dotnet test server/tests/RetroBoard.Api.Tests --filter "FullyQualifiedName~VoteConcurrencyTests"
```

- [ ] **Step 3: Commit**

```bash
git add server/tests/RetroBoard.Api.Tests/Realtime/VoteConcurrencyTests.cs
git commit -m "test(api): add vote concurrency integration tests"
```

---

## Task 26: SignalR broadcast integration test

**Files:**
- Create: `server/tests/RetroBoard.Api.Tests/TestSupport/SignalRTestClient.cs`
- Create: `server/tests/RetroBoard.Api.Tests/Realtime/SignalRBroadcastTests.cs`

- [ ] **Step 1: Write helper + tests**

`server/tests/RetroBoard.Api.Tests/TestSupport/SignalRTestClient.cs`:

```csharp
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;

namespace RetroBoard.Api.Tests.TestSupport;

public static class SignalRTestClient
{
    public static HubConnection Build(ApiFactory factory)
    {
        var server = factory.Server;
        var connection = new HubConnectionBuilder()
            .WithUrl($"{server.BaseAddress}hubs/board",
                opts =>
                {
                    opts.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    opts.WebSocketFactory = (_, _) => throw new NotSupportedException("Use long polling in tests");
                    opts.SkipNegotiation = false;
                    opts.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                })
            .Build();
        return connection;
    }
}
```

`server/tests/RetroBoard.Api.Tests/Realtime/SignalRBroadcastTests.cs`:

```csharp
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using RetroBoard.Api.Tests.TestSupport;
using RetroBoard.Application.Common.Dtos;
using Xunit;

namespace RetroBoard.Api.Tests.Realtime;

[Collection(nameof(PostgresCollection))]
public class SignalRBroadcastTests(PostgresFixture pg)
{
    [Fact]
    public async Task CardAdded_received_by_subscribed_client()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();
        await http.PostAsJsonAsync("/api/boards", new { name = "RT Retro" });

        await using var hub = SignalRTestClient.Build(factory);
        var tcs = new TaskCompletionSource<CardDto>();
        hub.On<CardDto>("CardAdded", c => tcs.TrySetResult(c));
        await hub.StartAsync();
        await hub.InvokeAsync<List<ParticipantDto>>("JoinBoard", "rt-retro", "sess-1", "Alice");

        await http.PostAsJsonAsync("/api/boards/rt-retro/cards",
            new { text = "Hello", author = "Alice", columnIndex = 0 });

        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.Text.Should().Be("Hello");
    }

    [Fact]
    public async Task PresenceChanged_received_when_second_client_joins()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();
        await http.PostAsJsonAsync("/api/boards", new { name = "Pres Retro" });

        await using var alice = SignalRTestClient.Build(factory);
        var presenceCount = new TaskCompletionSource<int>();
        alice.On<List<ParticipantDto>>("PresenceChanged", list =>
        {
            if (list.Count >= 2) presenceCount.TrySetResult(list.Count);
        });
        await alice.StartAsync();
        await alice.InvokeAsync<List<ParticipantDto>>("JoinBoard", "pres-retro", "sess-a", "Alice");

        await using var bob = SignalRTestClient.Build(factory);
        await bob.StartAsync();
        await bob.InvokeAsync<List<ParticipantDto>>("JoinBoard", "pres-retro", "sess-b", "Bob");

        var count = await presenceCount.Task.WaitAsync(TimeSpan.FromSeconds(5));
        count.Should().BeGreaterThanOrEqualTo(2);
    }
}
```

- [ ] **Step 2: Run**

```bash
dotnet test server/tests/RetroBoard.Api.Tests --filter "FullyQualifiedName~SignalRBroadcastTests"
```

- [ ] **Step 3: Commit**

```bash
git add server/tests/RetroBoard.Api.Tests/TestSupport/SignalRTestClient.cs \
        server/tests/RetroBoard.Api.Tests/Realtime/SignalRBroadcastTests.cs
git commit -m "test(api): add SignalR broadcast integration tests"
```

---

## Task 27: Final verification — full suite + manual smoke

- [ ] **Step 1: Run full test suite**

```bash
cd /Users/davitjanjalia/Desktop/retro-board/server
dotnet test
```

Expected: every test in `RetroBoard.Domain.Tests`, `RetroBoard.Application.Tests`, `RetroBoard.Api.Tests` passes. Zero warnings.

- [ ] **Step 2: Manual smoke against running API**

```bash
cd /Users/davitjanjalia/Desktop/retro-board
docker compose up -d postgres
cd server
dotnet run --project src/RetroBoard.Api &
APIPID=$!
sleep 5
curl -s -X POST http://localhost:5000/api/boards -H 'content-type: application/json' \
    -d '{"name":"Final Smoke"}' | jq
curl -s http://localhost:5000/api/boards/final-smoke | jq
kill $APIPID
```

Expected: 201 then 200 with the board including default columns.

- [ ] **Step 3: Open Swagger and click through**

```bash
cd /Users/davitjanjalia/Desktop/retro-board/server
dotnet run --project src/RetroBoard.Api
```

Open `http://localhost:5000/swagger` in a browser. Verify all endpoints are listed and `Try it out` works for `POST /api/boards`. Stop the server.

- [ ] **Step 4: Final commit if anything was tweaked**

```bash
cd /Users/davitjanjalia/Desktop/retro-board
git status
# If clean: backend complete, no commit. Otherwise:
# git add ... && git commit -m "chore: final cleanup"
```
