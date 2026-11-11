# pomodoro-tasker-equantic-ui

Web sibling of `../pomodoro-tasker`, built only with eQuantic UI (C#/.NET 10, preview pinned in
`global.json`). One user, SQLite on the server, no login. Interface in PT-BR.

## Commands

```bash
dotnet run --project src/PomodoroTasker.Web     # the app (TASKER_DB=path to choose the database)
dotnet watch --project src/PomodoroTasker.Web   # hot reload
dotnet build                                    # also transpiles the client; EQxxxx errors come from eqc
dotnet test                                     # Core: domain and SQL against in-memory SQLite
```

## Layout

- `src/PomodoroTasker.Core/` — plain .NET: `Day`, `Pomodoro`, `Recurrence`, `Reports`, `Repo` (all SQL),
  `Db`, `Migrations/*.sql`. No eQuantic reference.
- `src/PomodoroTasker.Web/` — `Pages/`, `Components/`, `Server/Api.cs` (server side of every
  action), `Views.cs` (what actions answer), `Theme/TaskerTheme.cs`, `Strings.cs`, `Bus.cs`.
- `tests/PomodoroTasker.Core.Tests/` — xUnit port of the desktop app's tests.

## Conventions

- **Only eQuantic UI.** No JavaScript, no CSS file, no `HtmlElement` escape hatch, no other UI package.
- **Domain logic stays in Core and runs on the server.** A component draws and calls a
  `[ServerAction]`, which is a one-line call into `Api`. Labels that need day arithmetic are
  worded on the server and travel in the view model.
- **Core mirrors `../pomodoro-tasker/src/lib/`**: same SQL, same migration files, same test
  cases. A rule changed here is changed there, or the difference is written in the README table.
- New SQL goes in `Repo.cs` with a test. Schema changes are new numbered files in `Migrations/`.
- A Day is a local date string `YYYY-MM-DD`; go through `Day`.
- Every `Api` call goes through `Api.Run`, which takes the gate and closes missed days first.
- The timer state is the `timer` setting; the client derives the clock from `EndsAt`, never counts down.
- A component with `[ServerAction]` methods has no constructor parameters (the server's
  container builds it); it takes inputs through settable properties and copies them in `AdoptConfig`.
- App type names must not match a framework type name (`ListView`, `UI`, `Menu`, ...): the
  client build names files by type.
- Code, comments, commits and docs in English. Interface strings in PT-BR, all in `Strings.cs`.
- Use the glossary terms exactly (Today, Backlog, Loose list, Occurrence, Rollover, Snapshot,
  Pomodoro, Tomato): `~/workspace/docs/pomodoro-tasker/CONTEXT.md`.

## Critical files

- `global.json` — the eQuantic.UI.Sdk version
- `src/PomodoroTasker.Core/Repo.cs`, `Pomodoro.cs` — data layer and timer machine
- `src/PomodoroTasker.Web/Server/Api.cs` — boot (day close), timer orchestration, view models
- `src/PomodoroTasker.Web/AppShell.cs`, `Components/TimerBlock.cs` — the frame of every page

## Canonical docs

Product intent is the original's: `~/workspace/docs/pomodoro-tasker/` (PRD, glossary, mockups).
What differs here: `~/workspace/docs/pomodoro-tasker-equantic-ui/README.md` and this repo's README.
