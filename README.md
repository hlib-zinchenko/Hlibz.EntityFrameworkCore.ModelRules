# Hlibz.EntityFrameworkCore.ModelRules

Enforce your team's conventions on an EF Core model. The model fails to build, at startup or in a
unit test, when an entity breaks one:

- **No shadow properties.** No foreign keys invented by convention because a key property was
  missing or misnamed.
- **Naming.** Tables, columns (complex types included), keys, foreign keys, indexes, check
  constraints, sequences and functions all follow snake_case, or whichever style you pick.
- **Column facets.** Decimals have a precision, strings have a max length, and C# nullability
  matches the column's nullability.
- **Enums stored as strings.**
- **One schema per DbContext.** Useful in a modular monolith.
- **Aggregate boundaries.** Aggregate roots refer to each other by key only, never delete each
  other by cascade, and each has a concurrency token.
- **Query filters.** Every soft-deletable or tenant-owned entity has one.
- **Delete behaviors the database enforces.** No `ClientSetNull` or `ClientCascade`, which only
  affect entities EF Core happens to be tracking.
- **No redundant indexes.**
- **Identifier length.** No table, column or constraint name is longer than your database allows.

[![CI](https://github.com/hlib-zinchenko/Hlibz.EntityFrameworkCore.ModelRules/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.EntityFrameworkCore.ModelRules/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Hlibz.EntityFrameworkCore.ModelRules.svg)](https://www.nuget.org/packages/Hlibz.EntityFrameworkCore.ModelRules)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Hlibz.EntityFrameworkCore.ModelRules.svg)](https://www.nuget.org/packages/Hlibz.EntityFrameworkCore.ModelRules)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/hlib-zinchenko/Hlibz.EntityFrameworkCore.ModelRules/blob/main/LICENSE)

```text
Hlibz.EntityFrameworkCore.ModelRules.ModelRuleViolationException: The EF Core model has 3 model rule violations:
  - MR001 NoShadowProperties: Post.BlogId: shadow foreign key created by convention for the relationship to Blog. Add a property named 'BlogId' to the entity, or configure the relationship with HasForeignKey(...) pointing at an existing one.
  - MR003 DecimalsHavePrecision: Currency.UsdRate: decimal column has no precision, so its store type falls back to the provider's default (unconstrained numeric on PostgreSQL, decimal(18,2) with silent truncation on SQL Server). Configure HasPrecision(precision, scale).
  - MR008 NoCascadeDeleteAcrossAggregates: Country.Currency: deleting Currency rows cascades to Country, a separate aggregate root. Configure OnDelete(DeleteBehavior.Restrict) (or SetNull for an optional relationship).
```

## Why

EF Core's conventions are forgiving on purpose. A misnamed foreign key property becomes a shadow
column. A decimal with no precision gets the provider's default: unconstrained on PostgreSQL,
truncated to two decimal places on SQL Server. A reference from one aggregate root to another
deletes by cascade. None of these fail, and none of them show up until you read the migration
carefully, or until production data is gone.

The idea for these rules came from another project of mine. It started on a very tight schedule,
where shipping fast came first and there was little room to stop and tidy up. Conventions slipped
along the way. The team meant to use snake_case names, but the database-side configuration was
often missing. Strings had no length limits, and shadow properties turned up all over the model.

I needed a way to find all of those problems at once and fix them in one go. This was February
2025, before I used agentic coding tools, but AI chatbots were already around. So I built a first
prototype from ChatGPT's suggestions, ran it against the project, and fixed everything it found.

Then I noticed the prototype worked the same way code style rules do. It restricts you a little,
and in exchange you stop spending attention on these details and can think about more important
things. So it came with me to my next project. Eventually I decided it deserved to be shared with
the community. Before publishing, I reworked it with [Claude Code](https://claude.com/claude-code).

EF Core gives you the hooks for checks like these, but no ready-made rules. This package is those
rules.

## Install

```bash
dotnet add package Hlibz.EntityFrameworkCore.ModelRules --prerelease
```

Until 1.0.0 ships, only preview versions are published, so `--prerelease` is required. With
central package management, set the version explicitly in `Directory.Packages.props`:

```xml
<PackageVersion Include="Hlibz.EntityFrameworkCore.ModelRules" Version="1.0.0-preview.3" />
```

## Quick start

Register the rules in your context's `ConfigureConventions`:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    // Optional: turn EF Core's ClientSetNull default into SetNull, so MR011 doesn't
    // flag every optional relationship you haven't configured.
    configurationBuilder.ConfigureClientSetNullAs(DeleteBehavior.SetNull);

    configurationBuilder.UseModelRules(rules => rules
        .NoShadowProperties()
        .NamesFollow(NamingStyle.SnakeCase)
        .DecimalsHavePrecision()
        .StringsHaveMaxLength()
        .NullabilityMatchesClr()
        .EnumsStoredAsStrings()
        .SingleSchema("billing")
        .NoCascadeDeleteAcrossAggregates<IAggregateRoot>()
        .MaxIdentifierLength(63)
        .EntitiesHaveQueryFilter<ISoftDeletable>()
        .NoClientSideDeleteBehaviors()
        .NoNavigationsAcrossAggregates<IAggregateRoot>()
        .AggregateRootsHaveConcurrencyToken<IAggregateRoot>()
        .NoRedundantIndexes());
}
```

Pick the rules that match your conventions; each one is independent. `IAggregateRoot` and
`ISoftDeletable` stand for your own marker types. Every rule that takes one also accepts a
`Func<Type, bool>` predicate instead, for roots a marker can't express, such as types deriving
from a generic `AggregateRoot<TId>` base class.

The rules run every time EF Core builds the model: once per process on first use of the context,
and whenever `dotnet ef migrations add` builds it. A broken rule throws a
`ModelRuleViolationException` that lists every violation, not just the first. A context that
loads a [compiled model](#how-it-works) never builds one at runtime, so there the rules only run
from the test below.

Then add one test, so a violation fails CI rather than an app startup:

```csharp
[Fact]
public void Model_follows_rules()
{
    using BillingDbContext context = new(options);
    ModelRuleVerifier.Verify(context);
}
```

With several contexts, as in a modular monolith, check them all in one test. `VerifyAll` takes
a factory for each context, so constructors that take services besides the options work too.
It checks every context, then throws once, listing the violations grouped by context:

```csharp
[Fact]
public void Models_follow_rules() =>
    ModelRuleVerifier.VerifyAll(
        () => new BillingDbContext(Options<BillingDbContext>(), new FakeDateTimeProvider()),
        () => new CatalogDbContext(Options<CatalogDbContext>(), new FakeDateTimeProvider()),
        () => new ShippingDbContext(Options<ShippingDbContext>()));

// The same provider and plugins as the app. The connection string is never used.
private static DbContextOptions<TContext> Options<TContext>()
    where TContext : DbContext =>
    new DbContextOptionsBuilder<TContext>()
        .UseNpgsql("Host=unused")
        .UseSnakeCaseNamingConvention()
        .Options;
```

If the tests already build the app's service provider, resolve each context from it instead,
with the same dependencies as the app:
`() => scope.ServiceProvider.GetRequiredService<BillingDbContext>()`.

A context whose constructor takes only its options can also be passed as just the options:
`ModelRuleVerifier.Verify(Options<ShippingDbContext>())`, or several at once with `VerifyAll`.

`Verify` builds the full design-time model, which runs the rules registered above. It throws if
the context has no rules registered at all, so it can never pass by accident. Building a model
never opens a connection, so the test needs no database.

Want to see this against a real database before wiring it into your own project? The
[samples project][samples] has a bookstore model that passes every rule, a Docker Compose
PostgreSQL instance, and a checked-in migration - plus instructions for breaking a rule and
watching `dotnet ef migrations add` fail with every violation listed:

[samples]: https://github.com/hlib-zinchenko/Hlibz.EntityFrameworkCore.ModelRules/tree/main/samples/Hlibz.EntityFrameworkCore.ModelRules.Samples

```bash
docker compose -f samples/Hlibz.EntityFrameworkCore.ModelRules.Samples/docker-compose.yml up -d
dotnet run --project samples/Hlibz.EntityFrameworkCore.ModelRules.Samples
```

## Rules

| ID | Rule | Checks |
|---|---|---|
| MR001 | `NoShadowProperties()` | Every mapped property has a CLR property or field behind it. TPH discriminators, owned types' synthetic keys and SQL Server temporal period columns are allowed. |
| MR002 | `NamesFollow(style, scope)` | Every schema, table, view, column, key, foreign key, index, check constraint, sequence and database function name matches `SnakeCase`, `UpperSnakeCase`, `LowerCase`, `CamelCase`, `PascalCase`, or a custom `Regex`. |
| MR003 | `DecimalsHavePrecision()` | Every column stored as `decimal`, including value objects converted to one, has a precision or an explicit column type. |
| MR004 | `StringsHaveMaxLength()` | Every column stored as `string`, including enums and value objects converted to one, has a max length or an explicit column type. |
| MR005 | `NullabilityMatchesClr()` | A `string` property isn't nullable in the database, and a `string?` property isn't `NOT NULL`. Same for `Nullable<T>`. |
| MR006 | `EnumsStoredAsStrings()` | No enum is stored as its underlying number. |
| MR007 | `SingleSchema(schema?)` | Every table, view, sequence and database function is in `schema`. Without a schema, everything uses the schema most tables already use. |
| MR008 | `NoCascadeDeleteAcrossAggregates(isRoot)` | No relationship between two aggregate roots deletes by cascade. Identify roots with a marker type (`<IAggregateRoot>`) or a predicate. |
| MR009 | `MaxIdentifierLength(max, scope)` | No identifier is longer than the database allows: 63 on PostgreSQL, 128 on SQL Server. EF Core shortens the names it generates, and sequence names, but not other names you configure explicitly. |
| MR010 | `EntitiesHaveQueryFilter<TMarker>()` | Every entity type implementing the marker (e.g. `ISoftDeletable` or `ITenantOwned`) has a query filter, on the root of its hierarchy. |
| MR011 | `NoClientSideDeleteBehaviors()` | No relationship uses `ClientSetNull` (EF Core's default for optional relationships) or `ClientCascade`. Both leave the database constraint at NO ACTION, so deleting a principal fails whenever its dependents aren't loaded. Add `ConfigureClientSetNullAs(DeleteBehavior.SetNull)` to fix every unconfigured optional relationship at once. |
| MR012 | `NoNavigationsAcrossAggregates(isRoot)` | No navigation leads from one aggregate root to another. Roots refer to each other by key. |
| MR013 | `AggregateRootsHaveConcurrencyToken(isRoot)` | Every aggregate root has a row version or another concurrency token. |
| MR014 | `NoRedundantIndexes()` | No index is a leading prefix of another index or key on the same table. Filtered indexes and indexes with provider-specific settings are skipped. |

What each rule reports, one example per rule:

```text
MR001 NoShadowProperties: Post.BlogId: shadow foreign key created by convention for the relationship to Blog. Add a property named 'BlogId' to the entity, or configure the relationship with HasForeignKey(...) pointing at an existing one.
MR002 NamesFollow: Post.BlogId: foreign key name 'FK_Post_Blog_BlogId' is not snake_case.
MR003 DecimalsHavePrecision: Blog.Rating: decimal column has no precision, so its store type falls back to the provider's default (unconstrained numeric on PostgreSQL, decimal(18,2) with silent truncation on SQL Server). Configure HasPrecision(precision, scale).
MR004 StringsHaveMaxLength: Post.Title: string column has no max length. Configure HasMaxLength(...), or HasColumnType(...) if an unbounded type is intended.
MR005 NullabilityMatchesClr: Blog.Subtitle: C# type is nullable but the column is NOT NULL. Make the C# type non-nullable, or remove IsRequired().
MR006 EnumsStoredAsStrings: Blog.Status: enum BlogStatus is stored as its underlying number. Configure HasConversion<string>(), or convert every enum at once with configurationBuilder.Properties<Enum>().HaveConversion<string>().
MR007 SingleSchema: State: mapped to schema 'geo', but the model's tables belong in 'billing'.
MR008 NoCascadeDeleteAcrossAggregates: Country.Currency: deleting Currency rows cascades to Country, a separate aggregate root. Configure OnDelete(DeleteBehavior.Restrict) (or SetNull for an optional relationship).
MR009 MaxIdentifierLength: Country.Currency: foreign key name 'FK_Country_Currency_CurrencyId' is 30 characters long; the limit is 12.
MR010 EntitiesHaveQueryFilter: Comment: implements ISoftDeletable but has no query filter. Configure HasQueryFilter(...).
MR011 NoClientSideDeleteBehaviors: Post.BlogId: deleting Blog rows sets the foreign key to null only on Post rows EF Core is tracking; the database constraint does nothing, so the delete fails when any other row still refers to it. Configure OnDelete(DeleteBehavior.SetNull), or OnDelete(DeleteBehavior.Restrict) to forbid it.
MR012 NoNavigationsAcrossAggregates: Country.Currency: navigation to Currency, a separate aggregate root. Reference it by key only: keep the foreign key property, remove the navigation, and load Currency through its own repository.
MR013 AggregateRootsHaveConcurrencyToken: Country: aggregate root has no concurrency token, so concurrent updates silently overwrite each other. Add a row version (IsRowVersion(), or xmin on PostgreSQL), or mark a property IsConcurrencyToken().
MR014 NoRedundantIndexes: State.Id: index 'IX_states_Id' (Id) has the same columns as primary key 'PK_states' (Id), which serves the same lookups. Remove it.
```

A few details:

- **Naming checks the result, not the derivation.** It doesn't matter whether a name came from
  [EFCore.NamingConventions](https://github.com/efcore/EFCore.NamingConventions), `HasColumnName`
  or an EF default. Only the final name's shape is checked, so `ip_address` and `line2text` both
  pass as snake_case.
- **Column facet rules look at what's stored.** A `Money` value object converted to `decimal`
  needs a precision. An enum stored as a string needs a max length. Set these once for every
  enum with `Properties<Enum>().HaveConversion<string>().HaveMaxLength(50)` in
  `ConfigureConventions`, which also satisfies MR006.
- **Sequences and functions have no entity type.** Their violations carry a `Target` such as
  `sequence sales.order_numbers` and a `null` `EntityClrType`. That includes the sequence EF
  Core creates for a TPC hierarchy's keys. Under TPC, every concrete table's own foreign key
  and index names are checked.
- **`scope` narrows MR002 and MR009** to some kinds of identifier, e.g.
  `NamingScope.Tables | NamingScope.Columns`. `NamingScope.All`, the default, also covers kinds
  that later versions add.
- **JSON columns are skipped by the column facet rules.** That covers owned types mapped with
  `ToJson()` and JSON complex types on EF Core 10. Their properties aren't columns. Naming still
  checks the JSON container column itself.
- **MR011 fires on every optional relationship you haven't configured**, because
  `ClientSetNull` is EF Core's default. `ConfigureClientSetNullAs(DeleteBehavior.SetNull)` in
  `ConfigureConventions` switches them all at once. It runs after `OnModelCreating` and before
  the rules, whichever you call first. A relationship configured with `OnDelete(...)` keeps its
  behavior, so an explicit `ClientSetNull` is still reported. SQL Server rejects `SetNull` where
  it would create multiple cascade paths. Pass `Restrict` there, or configure those relationships
  explicitly.
- **MR010 checks that a filter exists, not what it filters.** An entity type that needs two
  (soft delete and tenant) passes with one.
- **An explicit column type counts as a deliberate choice.** For example, `HasColumnType("text")`
  satisfies MR004.

## Exclusions

Every rule takes an optional callback to opt entity types or members out of that rule. `Except`
opts them out of every rule:

```csharp
configurationBuilder.UseModelRules(rules => rules
    .DecimalsHavePrecision(except => except
        .Property<Invoice>(x => x.Total)              // a property
        .Property<Blog>(x => x.Address.Latitude))     // a property of a complex type
    .NoShadowProperties(except => except
        .Property<AuditEntry>("PeriodStart"))         // a shadow property, by name
    .NoCascadeDeleteAcrossAggregates<IAggregateRoot>(except => except
        .Property<Order>(x => x.Customer))            // a navigation
    .StringsHaveMaxLength(except => except
        .Where(v => v.EntityClrType?.Namespace == "MyApp.Legacy"))
    .Except(except => except
        .Entity<OutboxMessage>()                      // an entity type, in every rule
        .Entity("BlogTag")));                         // a shared-type entity, by name
```

An exclusion for an entity type also covers the types derived from it.

## Checking without registering

To check rules only in tests, without registering them on the context, pass them in directly. Each
method also accepts `DbContextOptions` in place of a context, for a context whose constructor
takes only its options:

```csharp
ModelRuleVerifier.Verify(context, rules => rules.DecimalsHavePrecision());   // throws

IReadOnlyList<ModelRuleViolation> violations =
    ModelRuleVerifier.Validate(context, rules => rules.NamesFollow(NamingStyle.SnakeCase));
```

Each `ModelRuleViolation` carries a `RuleId`, a `RuleName`, a `Target` (e.g. `Blog.Address.City`)
and a `Message`. A violation on an entity type also carries its `EntityClrType`, `EntityTypeName`
and `MemberPath` (dotted through complex properties, e.g. `Address.City`). All three are `null`
for a violation on something that isn't an entity type, such as a sequence.

## Custom rules

Implement `IModelRule` and add it with `Add`. Use your own ID prefix so your rules never collide
with built-in ones:

```csharp
public sealed class TablesArePluralRule : IModelRule
{
    public string Id => "APP001";
    public string Name => "TablesArePlural";

    public IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model) =>
        model.GetEntityTypes()
            .Where(e => e.GetTableName() is { } table && !table.EndsWith('s'))
            .Select(e => new ModelRuleViolation(this, e, null, "table name should be plural."));
}

configurationBuilder.UseModelRules(rules => rules.Add(new TablesArePluralRule()));
```

To report something that doesn't belong to an entity type, such as a sequence or the model as a
whole, pass a target description instead of an entity type:

```csharp
public IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model) =>
    model.GetSequences()
        .Where(s => s.Schema != "billing")
        .Select(s => new ModelRuleViolation(this, $"sequence {s.Name}", "belongs in 'billing'."));
```

Exclusions work on custom rules the same way they do on built-in ones. Entity and member
exclusions only match violations on entity types; exclude anything else with
`Where(v => v.Target == "sequence invoice_numbers")`.

## How it works

`UseModelRules` adds a model-finalizing convention. It runs after all of EF Core's own
conventions, including naming plugins and constraint-name shortening, but while the model still
carries its full design-time metadata. A convention added the usual way on the finalized side
would run after EF Core converts the model to its slimmed-down runtime form. Rules only use EF
Core's public read-only metadata API (`IReadOnlyModel`), so a rule sees the same model whether it
runs at startup or from `ModelRuleVerifier.Verify`.

**Compiled models.** A context that uses a compiled model (`dotnet ef dbcontext optimize`) never
builds its model at runtime, so conventions don't run and the rules never fire at startup.
`ModelRuleVerifier.Verify(context)` still builds the design-time model, which runs them, so with
a compiled model the test is where the rules are enforced. `dotnet ef dbcontext optimize` and
`migrations add` also build the design-time model, so they fail on a broken rule too.

## Compatibility

- EF Core 8, 9 and 10. The package has one build per EF Core major (`net8.0`, `net9.0`,
  `net10.0`), and CI tests each one.
- Provider-neutral: rules only read EF Core's relational metadata. The test suite builds the same
  models on PostgreSQL (Npgsql), SQL Server, SQLite and MySQL (Oracle's `MySql.EntityFrameworkCore`
  on EF Core 8-10; Pomelo on EF Core 8-9, since Pomelo has no EF Core 10 release yet) and
  checks that every provider reports exactly the same violations.
- Provider features that add shadow properties by design are allowed, e.g. SQL Server temporal
  tables' period columns.
- Integration tests run against real PostgreSQL, SQL Server and MySQL in Docker. They check
  that a model passing the rules really creates only snake_case tables, columns, constraints and
  indexes, and that a reported name is the one the database creates.
- Some rules mean less on some databases. SQLite ignores precision, max length and schemas.
  MySQL always names a primary key `PRIMARY`, whatever MR002 checked in the model. MR009's limit
  is yours to pick: 63 on PostgreSQL, 64 on MySQL, 128 on SQL Server.

## Versioning

The package follows [semantic versioning](https://semver.org), and rule IDs are permanent: an ID
is never renumbered or reused.

A rule you register keeps improving, so a minor or patch release may make it report violations
it missed before, such as a kind of identifier `NamingScope.All` didn't cover yet. The fix is
always to follow the rule, or exclude the case. What only a major version may do:

- remove or rename public API, or change a rule ID's meaning;
- make a rule reject a model that follows the convention it states;
- change what a rule requires, e.g. tighten what counts as a valid snake_case name.

New rules are always opt-in: nothing starts checking your model until you register it.

## License

MIT
