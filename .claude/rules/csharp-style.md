---
paths:
  - "Surl.*/**/*.cs"
---
# C# style rules

- Follow `.editorconfig`; the format hook enforces whitespace automatically.
- Prefer records for immutable data transfer types, classes for behavior.
- Use primary constructors for simple dependency injection.
- Prefer `is null` / `is not null` over `== null`.
- Guard public method arguments with `ArgumentNullException.ThrowIfNull`.
- One public type per file; file name matches the type name.
- Keep methods short; extract a private method before nesting past two levels.
- XML documentation comments on every public member of a library project.
