# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

A small NuGet package of declarative rules that check a finished EF Core model against a team's
conventions (shadow properties, naming, decimal precision, string lengths, nullability, enum
storage, schemas, cascade deletes between aggregate roots, identifier lengths). Rules run while EF
builds the model (`UseModelRules` in `ConfigureConventions`) or from a test (`ModelRules.Verify`).

## Requires

* .NET 10 SDK, plus the .NET 8 and 9 runtimes to run every test target.

## Commands

```bash
# Build everything
dotnet build

# Run all tests (all three target frameworks)
dotnet test --project tests/Hlibz.EntityFrameworkCore.ModelRules.Tests

# One target framework only
dotnet test --project tests/Hlibz.EntityFrameworkCore.ModelRules.Tests -f net10.0

# Integration tests against real databases (needs Docker; pulls Postgres, SQL Server, MySQL)
dotnet test --project tests/Hlibz.EntityFrameworkCore.ModelRules.IntegrationTests

# Pack the library locally
dotnet pack src/Hlibz.EntityFrameworkCore.ModelRules -c Release -o ./nupkg
```

Test projects use Microsoft Testing Platform (`test.runner` in `global.json`), so `dotnet test`
needs `--project <path>` rather than a bare directory argument.

Testcontainers 4.15 talks Docker API 1.44+. With an older local Docker Engine (e.g. 24.x, API
1.43) it fails with "client version 1.44 is too new". Update Docker, or prefix the command with
`DOCKER_API_VERSION=1.43`. CI's runners have a current Docker.

## Architecture

- **Multi-targeting = one EF Core major per TFM.** `Directory.Build.props` sets
  `net8.0;net9.0;net10.0` for every project, and `Directory.Packages.props` pins
  `Microsoft.EntityFrameworkCore.Relational` (and the test-only Npgsql provider) per TFM:
  8.0.x on net8.0, 9.0.x on net9.0, 10.0.x on net10.0. So each build of the library is compiled
  and tested against its own EF major, and the package's dependency group for each TFM pulls the
  matching EF. Use `#if NET10_0_OR_GREATER` for APIs that only exist in newer EF majors (see
  `ModelWalker.IsJson` and the JSON complex-type container column in `ModelIdentifiers`). Keep the
  pinned patch versions current: a known-vulnerable transitive package fails the build, because
  `TreatWarningsAsErrors` turns NuGet audit warnings into errors.
- **Where rules run.** `ModelConfigurationBuilderExtensions.UseModelRules` (in the
  `Microsoft.EntityFrameworkCore` namespace, for discoverability) adds
  `Internal/ModelRulesConvention`, an `IModelFinalizingConvention`. It has to be *finalizing*:
  conventions added through `ModelConfigurationBuilder.Conventions` are appended after EF's own,
  and on the finalized side that means after `RuntimeModelConvention` has already swapped in the
  slimmed-down `RuntimeModel`. While finalizing, the model is the full design-time `Model`, the
  same instance `IDesignTimeModel` hands out later, and every other convention (naming plugins,
  `SharedTableConvention`'s name shortening) has already run. The convention records models that
  passed in a `ConditionalWeakTable`, so `ModelRules.Verify(DbContext)` can tell "rules passed"
  apart from "no rules registered".
- **Rules take `IReadOnlyModel`.** That's the interface common to the finalizing
  `IConventionModel` and the design-time `IModel`, so the same rule code runs in both places. Only
  use read-only metadata APIs in rules.
- `Rules/` holds the built-in rules (`internal sealed`, one class each, `ModelRule` base with
  id/name). IDs `MR001`–`MR009` are public contract: never renumber or reuse one.
- `Internal/ModelWalker` enumerates scalar properties, complex-type properties included, and
  reports each against the entity type that declares it (or contains its complex property), with
  a dotted `MemberPath` (`Address.City`). `DeclaredProperties` gives each property exactly once
  for property rules. `AllProperties` includes inherited ones, used per store object.
- `Internal/ModelIdentifiers` collects every database identifier exactly once: schemas, tables,
  views, columns, and key/FK/index names. It deduplicates by store object, because TPH, table
  splitting and owned types map several entity types to one table, and it attributes table names
  to the entity type that introduces the mapping. JSON-mapped owned types contribute only their
  container column. MR002 (naming) and MR009 (identifier length) both use it.
- `ModelRuleExclusions` match violations by entity CLR type (`Entity<T>` covers derived types),
  by member path (`Property<T>` also matches members declared on a base type of `T`), by name,
  or by predicate. Per-rule exclusions and global `Except` exclusions are both applied in
  `Internal/ModelRuleSet`.
- `tests/` builds real models with no database: building a model never opens the connection.
  Tests use Npgsql by default. `ProviderCompatibilityTests` builds the same models on every
  `TestProvider` (Npgsql, SQL Server, SQLite, Oracle MySQL, and Pomelo on net8.0/net9.0 only)
  and asserts identical violations, so provider-specific behavior shows up there first. SQL
  Server temporal period columns are an example: `NoShadowPropertiesRule` recognizes them
  through SQL Server's annotation names, so the library never references a provider package.
  `TestDbContext` swaps in a per-instance `IModelCacheKeyFactory`, so every test gets its own
  model. Test entities are in `Entities.cs` and shared model configurations in `Models.cs`.
  Test names follow `Subject_WithCondition_ExpectedOutcome`, where the subject is the rule or
  API under test, e.g. `NamesFollow_WithJsonOwnedType_ChecksOnlyContainerColumn` or
  `Verify_WithoutRegisteredRules_ThrowsInsteadOfPassingSilently`. `Vehicle`/`Car` deliberately
  sort derived-before-base to catch attribution that depends on `GetEntityTypes()` order.
- `tests/Hlibz.EntityFrameworkCore.ModelRules.IntegrationTests` (net10.0 / EF Core 10 only) runs
  the rules against real PostgreSQL, SQL Server and MySQL in Docker via Testcontainers. It
  proves that what the rules see in the model is what `EnsureCreated` actually produces, by
  reading each database's own catalog. It also covers each database's defaults and quirks:
  Postgres truncating long names, `numeric` vs `decimal(18,2)`, SQL Server temporal period
  columns, and MySQL always naming primary keys `PRIMARY`. `DatabaseFixture` (one container per
  test class) gives each context a fresh database. The shared tests live in the generic
  `DatabaseSchemaTests<TFixture>`, and one derived class per provider adds its own. Entities,
  models and rule sets are compiled in from the unit test project (`Entities.cs`, `Models.cs`,
  `RuleSets.cs`) rather than duplicated. CI runs it as a separate `integration-tests` job; the
  release workflow doesn't run it.

## Releasing

`.github/workflows/release.yml` runs on a pushed `vX.Y.Z` tag. It builds and tests, packs
`src/Hlibz.EntityFrameworkCore.ModelRules` with `-p:Version` taken from the tag, and publishes
via NuGet Trusted Publishing (OIDC, no API key; see
https://learn.microsoft.com/nuget/nuget-org/trusted-publishing). The job uses the `release` GitHub
Environment, which must match the Environment on the nuget.org trusted-publishing policy. It also
needs a `${{ secrets.NUGET_USER }}` repo secret holding the nuget.org profile username (not the
email).

To publish, don't bump anything in the csproj (the tag drives the version). Just run
`git tag vX.Y.Z && git push origin vX.Y.Z`.

## Conventions

- File-scoped namespaces, explicit types (no `var`), 4-space indent, LF, UTF-8 (no BOM), 100-char
  lines. See `.editorconfig`.
- `Directory.Packages.props` manages package versions centrally. csproj files reference packages
  without a `Version=` attribute.
- No `Microsoft.SourceLink.GitHub` package reference: the .NET SDK (8+) includes Source Link
  itself, and the 8.0.0 package pulls a vulnerable `Microsoft.Build.Tasks.Git`.
- No StyleCop/SonarAnalyzer: this is a small single-purpose package, kept lightweight on purpose.
  `TreatWarningsAsErrors` is still on, and `GenerateDocumentationFile` means every public member
  needs an XML doc comment.
