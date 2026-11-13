![Hlibz.EntityFrameworkCore.ModelRules icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.EntityFrameworkCore.ModelRules/main/docs/icon.png)

# Hlibz.EntityFrameworkCore.ModelRules

[![NuGet](https://img.shields.io/nuget/v/Hlibz.EntityFrameworkCore.ModelRules.svg)](https://www.nuget.org/packages/Hlibz.EntityFrameworkCore.ModelRules)
[![CI](https://github.com/hlib-zinchenko/Hlibz.EntityFrameworkCore.ModelRules/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.EntityFrameworkCore.ModelRules/actions/workflows/ci.yml)

Declarative rules that check a finished EF Core model against your team's conventions. A model
that breaks one fails to build, at startup or in a unit test, and the error lists every violation
at once, each with a rule ID and how to fix it. EF Core 10 and 11; provider-neutral.

```bash
dotnet add package Hlibz.EntityFrameworkCore.ModelRules
```

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
    configurationBuilder.UseModelRules(rules => rules
        .NoShadowProperties()
        .NamesFollow(NamingStyle.SnakeCase)
        .DecimalsHavePrecision()
        .StringsHaveMaxLength()
        .NoCascadeDeleteAcrossAggregates<IAggregateRoot>()
        .NoRedundantIndexes());
```

Then one test, so a violation fails CI rather than an app startup. It builds the model without a
database, and it is where the rules run when the app uses a compiled model:

```csharp
[Fact]
public void Model_follows_rules()
{
    using BillingDbContext context = new(options);
    ModelRuleVerifier.Verify(context);
}
```

| ID | Rule | ID | Rule |
|---|---|---|---|
| MR001 | `NoShadowProperties()` | MR008 | `NoCascadeDeleteAcrossAggregates(isRoot)` |
| MR002 | `NamesFollow(style, scope)` | MR009 | `MaxIdentifierLength(max, scope)` |
| MR003 | `DecimalsHavePrecision()` | MR010 | `EntitiesHaveQueryFilter<TMarker>()` |
| MR004 | `StringsHaveMaxLength()` | MR011 | `NoClientSideDeleteBehaviors()` |
| MR005 | `NullabilityMatchesClr()` | MR012 | `NoNavigationsAcrossAggregates(isRoot)` |
| MR006 | `EnumsStoredAsStrings()` | MR013 | `AggregateRootsHaveConcurrencyToken(isRoot)` |
| MR007 | `SingleSchema(schema?)` | MR014 | `NoRedundantIndexes()` |

* Each rule is opt-in and independent; rule IDs are never renumbered or reused.
* Opt entity types or members out per rule (`.DecimalsHavePrecision(except => except
  .Property<Invoice>(x => x.Total))`) or out of every rule (`.Except(except => except
  .Entity<OutboxMessage>())`). Add your own rules by implementing `IModelRule`.
* `VerifyAll(() => new BillingDbContext(...), ...)` checks several contexts in one test.

Full documentation and a sample: [github.com/hlib-zinchenko/Hlibz.EntityFrameworkCore.ModelRules](https://github.com/hlib-zinchenko/Hlibz.EntityFrameworkCore.ModelRules).
