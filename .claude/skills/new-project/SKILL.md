---
name: new-project
description: Add a new C# project (and its matching test project) to the Surl solution following repository conventions. Use when asked to create, scaffold, or add a project, library, console app, or web app to the solution.
---
# Add a project to the Surl solution

Every project sits in a category folder, its `.UnitTests` twin beside it
(`CLAUDE.md`, "Repository layout"; ADR-0079). Below, `<Category>` is that folder, e.g.
`Protocols/Mail`, `Cryptography` or `Foundation`. There is no `src/` and no `tests/` —
do not create them.

1. Confirm the name, template and category. Libraries are `Surl.<Area>.UnitLibrary`
   (protocols `Surl.Protocol.<Name>.UnitLibrary`); the template is `classlib` unless it
   is the `Surl.Console` executable. Pick the category by the rules in `CLAUDE.md`,
   "Repository layout"; a new category needs an ADR.
2. If no solution file exists at the repository root, create one:
   `dotnet new sln --name Surl --format slnx`
3. Create the production project in its category:
   `dotnet new classlib --name Surl.<Area>.UnitLibrary --output <Category>/Surl.<Area>.UnitLibrary --framework net10.0`
4. Create the matching test project beside it:
   `dotnet new mstest --name Surl.<Area>.UnitTests --output <Category>/Surl.<Area>.UnitTests --framework net10.0`
5. Add both to the solution in the solution folder named after the category:
   `dotnet sln Surl.slnx add --solution-folder <Category> <Category>/Surl.<Area>.UnitLibrary <Category>/Surl.<Area>.UnitTests`
   Then check `Surl.slnx`: the folder is `/<Category>/` and its projects are in ordinal order.
6. Reference production from test:
   `dotnet add <Category>/Surl.<Area>.UnitTests reference <Category>/Surl.<Area>.UnitLibrary`
7. For a protocol server, also reference the contracts, and nothing else horizontal beyond the libraries in ADR-0002 decision 3's table, as later ADRs amend it (read the table; it is the list):
   `dotnet add <Category>/Surl.Protocol.<Name>.UnitLibrary reference Foundation/Surl.Protocol.Abstractions.UnitLibrary`
8. Strip any `Version` attributes and settings duplicated by `Directory.Build.props` /
   `Directory.Packages.props` (create those at the root if missing).
9. Add `<Category>/Surl.<Area>.UnitLibrary/CLAUDE.md` with a short purpose statement and any
   project-specific rules.
10. Leave ahead-of-time compilation alone. `Directory.Build.props` sets
    `IsAotCompatible` for every project that is not `*.UnitTests`, and its
    `VerifyAotCompatibility` target fails the build if a project overrides it, so a new
    project is AOT-checked the moment it exists. Never add `IsAotCompatible` or
    `PublishAot` to a new csproj, and never set `IsAotCompatible` on a test project -
    MSTest finds tests by reflection and the AOT analyzers forbid it.
11. Run `dotnet build` and `dotnet test`; both must pass before finishing.
