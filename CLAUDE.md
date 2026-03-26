# C# Coding Practices

## General

- Target the latest stable C# language version (`<LangVersion>latest</LangVersion>`)
- Prefer `sealed` classes unless inheritance is explicitly needed
- Prefer `readonly` fields over mutable state
- Never use top-level statements — always declare an explicit `Program` class with `static async Task Main(string[] args)`
- One class per file; filename must match the class name

## Naming

| Symbol | Convention | Example |
|---|---|---|
| Classes, Methods, Properties | PascalCase | `SolutionContext`, `BuildHost` |
| Private fields | `_camelCase` | `_serverProcess` |
| Parameters & locals | camelCase | `solutionPath`, `parentPid` |
| Constants | PascalCase | `DefaultPort` |
| Interfaces | `I` prefix + PascalCase | `ISolutionProvider` |

## Code Style

- Use `var` when the type is obvious from the right-hand side; use explicit types otherwise
- Use expression bodies for simple single-expression members
- Use primary constructors (C# 12+) for simple dependency injection
- Prefer `string.IsNullOrWhiteSpace` over `== null || == ""`
- Always use `sealed` on `override` methods that should not be further overridden
- Avoid magic numbers and strings — extract to `private const` or `private static readonly`

## Null Safety

- Enable nullable reference types (`<Nullable>enable</Nullable>`) on all new projects
- Never use `!` (null-forgiving) unless you have verified the value cannot be null and can explain why
- Prefer null-coalescing (`??`, `??=`) and null-conditional (`?.`) operators over null checks

## Async

- Every async method must return `Task` or `Task<T>` — never `async void` (except event handlers)
- Always suffix async methods with `Async` (e.g. `StartServerAsync`)
- Always pass and respect `CancellationToken` in async methods
- Never use `.Result` or `.Wait()` — always `await`
- Use `ConfigureAwait(false)` in library/non-UI code

## Logging

- **Never** use `Debug.WriteLine`, `Trace.WriteLine`, or `Console.WriteLine` for diagnostics
- **MCPServer (.NET 8):** inject `ILogger<T>` via constructor; the ASP.NET Core host registers it automatically
- **VSIX (.NET Framework 4.7.2):** use `VsOutputLogger` which wraps `IVsOutputWindowPane` (creates a named "MCP Server" pane in the VS Output window)
- Use structured log messages with named placeholders: `_logger.LogInformation("Server started on {Port}", port)`
- Choose the correct level:
  - `LogDebug` — verbose / development detail
  - `LogInformation` — normal operational flow
  - `LogWarning` — recoverable / unexpected but non-fatal
  - `LogError` — failures; always pass the exception object so the stack trace is preserved

## Error Handling

- Never swallow exceptions silently with an empty `catch` block — at minimum log the error
- Catch specific exceptions, not `Exception` unless re-throwing or at a top-level boundary
- Use `finally` to release resources; prefer `using` declarations over manual `Dispose` calls

## Formatting

- Do not use extra spaces or tabs to manually align code — no column-aligning of assignments, declarations, or parameters
- ReSharper handles formatting; write code naturally and let the tool enforce style

## Braces

- Always use curly braces for every block body — `if`, `else`, `for`, `foreach`, `while`, `using`, `try`, `catch`, `finally` — even when the body is a single statement
- Never omit braces for one-liners; this prevents accidental bugs when adding a second statement later

## Classes & Structure

- Split responsibilities into focused methods — no method should exceed ~30 lines
- Order class members: constants → fields → constructors → public methods → private methods
- Mark classes and members with XML doc comments (`<summary>`) on all public and internal API

## Projects

- VSIX extension projects target `.NET Framework 4.7.2` — never use nullable syntax or APIs unavailable on that TFM without a `#if` guard
- MCPServer and any new helper projects target `.NET 8` or later
- Avoid cross-project references between .NET Framework and .NET 8 projects — use process boundaries (stdin/stdout, HTTP, named pipes) instead
