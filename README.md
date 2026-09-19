# Hlibz.EntityFrameworkCore.ModelRules

Enforce your team's conventions on an EF Core model. The model fails to build, at startup or in a
unit test, when an entity breaks one:

- **No shadow properties.** No foreign keys invented by convention because a key property was
  missing or misnamed.
- **Naming.** Tables, columns (complex types included), keys, foreign keys and indexes all
  follow snake_case, or whichever style you pick.
- **Column facets.** Decimals have a precision, strings have a max length, and C# nullability
  matches the column's nullability.
- **Enums stored as strings.**
- **One schema per DbContext.** Useful in a modular monolith.
- **No cascade deletes across aggregate roots.**

[![CI](https://github.com/hlib-zinchenko/Hlibz.EntityFrameworkCore.ModelRules/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.EntityFrameworkCore.ModelRules/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Hlibz.EntityFrameworkCore.ModelRules.svg)](https://www.nuget.org/packages/Hlibz.EntityFrameworkCore.ModelRules)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Hlibz.EntityFrameworkCore.ModelRules.svg)](https://www.nuget.org/packages/Hlibz.EntityFrameworkCore.ModelRules)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/hlib-zinchenko/Hlibz.EntityFrameworkCore.ModelRules/blob/main/LICENSE)

```text
Hlibz.EntityFrameworkCore.ModelRules.ModelRuleViolationException: The EF Core model has 3 model rule violations:
  - MR001 NoShadowProperties: Post.BlogId: shadow foreign key created by convention for the relationship to Blog. Add a 'BlogId' property to the entity, or configure the relationship with HasForeignKey(...) pointing at an existing one.
  - MR003 DecimalsHavePrecision: Currency.UsdRate: decimal column has no precision, so its store type falls back to the provider's default (unconstrained numeric on PostgreSQL, decimal(18,2) with silent truncation on SQL Server). Configure HasPrecision(precision, scale).
  - MR008 NoCascadeDeleteAcrossAggregates: Country.Currency: deleting a Currency cascades to Country, a separate aggregate root. Configure OnDelete(DeleteBehavior.Restrict) (or SetNull for an optional relationship).
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
dotnet add package Hlibz.EntityFrameworkCore.ModelRules
```

## Quick start

Register the rules in your context's `ConfigureConventions`:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.UseModelRules(rules => rules
        .NoShadowProperties()
        .NamesFollow(NamingStyle.SnakeCase)
        .DecimalsHavePrecision()
        .StringsHaveMaxLength()
        .NullabilityMatchesClr()
        .EnumsStoredAsStrings()
        .SingleSchema("billing")
        .NoCascadeDeleteAcrossAggregates<IAggregateRoot>()
        .MaxIdentifierLength(63));
}
```

The rules run every time EF Core builds the model: once per process on first use of the context,
and whenever `dotnet ef migrations add` builds it. A broken rule throws a
`ModelRuleViolationException` that lists every violation, not just the first.

Then add one test, so a violation fails CI rather than an app startup:

```csharp
[Fact]
public void Model_follows_rules()
{
    using BillingDbContext context = new(options);
    ModelRules.Verify(context);
}
```

`Verify` builds the full design-time model, which runs the rules registered above. It throws if
the context has no rules registered at all, so it can never pass by accident. Building a model
never opens a connection, so the test needs no database.

## Rules

| ID | Rule | Checks |
|---|---|---|
| MR001 | `NoShadowProperties()` | Every mapped property has a CLR property or field behind it. TPH discriminators, owned types' synthetic keys and SQL Server temporal period columns are allowed. |
| MR002 | `NamesFollow(style, scope)` | Every schema, table, view, column, key, foreign key and index name matches `SnakeCase`, `UpperSnakeCase`, `LowerCase`, `CamelCase`, `PascalCase`, or a custom `Regex`. |
| MR003 | `DecimalsHavePrecision()` | Every column stored as `decimal`, including value objects converted to one, has a precision or an explicit column type. |
| MR004 | `StringsHaveMaxLength()` | Every column stored as `string`, including enums and value objects converted to one, has a max length or an explicit column type. |
| MR005 | `NullabilityMatchesClr()` | A `string` property isn't nullable in the database, and a `string?` property isn't `NOT NULL`. Same for `Nullable<T>`. |
| MR006 | `EnumsStoredAsStrings()` | No enum is stored as its underlying number. |
| MR007 | `SingleSchema(schema?)` | Every table and view is in `schema`. Without a schema, every table uses the schema most tables already use. |
| MR008 | `NoCascadeDeleteAcrossAggregates(isRoot)` | No relationship between two aggregate roots deletes by cascade. Identify roots with a marker type (`<IAggregateRoot>`) or a predicate. |
| MR009 | `MaxIdentifierLength(max, scope)` | No identifier is longer than the database allows: 63 on PostgreSQL, 128 on SQL Server. EF Core shortens the names it generates, but not names you configure explicitly. |

A few details:

- **Naming checks the result, not the derivation.** It doesn't matter whether a name came from
  [EFCore.NamingConventions](https://github.com/efcore/EFCore.NamingConventions), `HasColumnName`
  or an EF default. Only the final name's shape is checked, so `ip_address` and `line2text` both
  pass as snake_case.
- **Column facet rules look at what's stored.** A `Money` value object converted to `decimal`
  needs a precision. An enum stored as a string needs a max length. Set these once for every
  enum with `Properties<Enum>().HaveConversion<string>().HaveMaxLength(50)` in
  `ConfigureConventions`, which also satisfies MR006.
- **JSON columns are skipped by the column facet rules.** That covers owned types mapped with
  `ToJson()` and JSON complex types on EF Core 10. Their properties aren't columns. Naming still
  checks the JSON container column itself.
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
        .Where(v => v.EntityClrType.Namespace == "MyApp.Legacy"))
    .Except(except => except
        .Entity<OutboxMessage>()                      // an entity type, in every rule
        .Entity("BlogTag")));                         // a shared-type entity, by name
```

An exclusion for an entity type also covers the types derived from it.

## Checking without registering

To check rules only in tests, without registering them on the context, pass them in directly:

```csharp
ModelRules.Verify(context, rules => rules.DecimalsHavePrecision());   // throws

IReadOnlyList<ModelRuleViolation> violations =
    ModelRules.Validate(context, rules => rules.NamesFollow(NamingStyle.SnakeCase));
```

Each `ModelRuleViolation` carries a `RuleId`, a `RuleName`, the `EntityClrType`, a `MemberPath`
(dotted through complex properties, e.g. `Address.City`) and a `Message`.

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

Exclusions work on custom rules the same way they do on built-in ones.

## How it works

`UseModelRules` adds a model-finalizing convention. It runs after all of EF Core's own
conventions, including naming plugins and constraint-name shortening, but while the model still
carries its full design-time metadata. A convention added the usual way on the finalized side
would run after EF Core converts the model to its slimmed-down runtime form. Rules only use EF
Core's public read-only metadata API (`IReadOnlyModel`), so a rule sees the same model whether it
runs at startup or from `ModelRules.Verify`.

**Compiled models.** A context that uses a compiled model (`dotnet ef dbcontext optimize`) never
builds its model at runtime, so the startup check never runs. `ModelRules.Verify(context)` still
builds the design-time model, which is why the test is worth keeping.

## Compatibility

- EF Core 8, 9 and 10. The package has one build per EF Core major (`net8.0`, `net9.0`,
  `net10.0`), and CI tests each one.
- Provider-neutral: rules only read EF Core's relational metadata. The test suite builds the same
  models on PostgreSQL (Npgsql), SQL Server, SQLite and MySQL (Oracle's `MySql.EntityFrameworkCore`
  on EF Core 8-10; Pomelo on EF Core 8-9, since Pomelo has no EF Core 10 release yet) and
  checks that every provider reports exactly the same violations.
- Provider features that add shadow properties by design are allowed, e.g. SQL Server temporal
  tables' period columns.
- Some rules mean less on some databases. SQLite ignores precision, max length and schemas, and
  MR009's limit is yours to pick: 63 on PostgreSQL, 64 on MySQL, 128 on SQL Server.

## License

MIT
