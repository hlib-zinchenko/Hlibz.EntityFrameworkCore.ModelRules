using System.Text.RegularExpressions;

using Hlibz.EntityFrameworkCore.ModelRules.Internal;
using Hlibz.EntityFrameworkCore.ModelRules.Rules;

namespace Hlibz.EntityFrameworkCore.ModelRules;

/// <summary>
/// Chooses which rules to enforce, and what each one lets through. Every rule method takes an
/// optional <see cref="ModelRuleExclusions"/> callback for opting individual entity types or
/// members out of that rule; <see cref="Except"/> opts them out of every rule.
/// </summary>
public sealed class ModelRulesBuilder
{
    private readonly List<(IModelRule Rule, ModelRuleExclusions Exclusions)> _rules = [];
    private readonly ModelRuleExclusions _globalExclusions = new();

    internal ModelRulesBuilder()
    {
    }

    /// <summary>
    /// MR001: every mapped property has a CLR property or field behind it. Catches foreign keys EF
    /// invents by convention when a relationship's key property is missing or misnamed. TPH
    /// discriminators, owned types' synthetic keys and SQL Server temporal period columns are
    /// allowed.
    /// </summary>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder NoShadowProperties(Action<ModelRuleExclusions>? except = null) =>
        Add(new NoShadowPropertiesRule(), except);

    /// <summary>
    /// MR002: every database identifier in <paramref name="scope"/> - schemas, tables, views,
    /// columns (complex-type columns included), key, foreign key, index and check constraint
    /// names, sequences and database functions - matches <paramref name="style"/>. Checks the
    /// final names, however they were produced (a naming convention plugin,
    /// <c>HasColumnName</c>, EF defaults).
    /// </summary>
    /// <param name="style">The naming style every identifier must match.</param>
    /// <param name="scope">Which kinds of identifiers to check.</param>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="style"/> isn't a defined value, or <paramref name="scope"/> is
    /// <see cref="NamingScope.None"/>.
    /// </exception>
    public ModelRulesBuilder NamesFollow(
        NamingStyle style,
        NamingScope scope = NamingScope.All,
        Action<ModelRuleExclusions>? except = null)
    {
        ThrowIfNone(scope);
        return Add(NamesFollowRule.For(style, scope), except);
    }

    /// <summary>
    /// MR002: every database identifier in <paramref name="scope"/> matches a custom
    /// <paramref name="pattern"/>. Anchor the pattern (<c>^...$</c>) to match whole names.
    /// </summary>
    /// <param name="pattern">The pattern every identifier must match.</param>
    /// <param name="scope">Which kinds of identifiers to check.</param>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="scope"/> is <see cref="NamingScope.None"/>.
    /// </exception>
    public ModelRulesBuilder NamesFollow(
        Regex pattern,
        NamingScope scope = NamingScope.All,
        Action<ModelRuleExclusions>? except = null)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ThrowIfNone(scope);
        return Add(new NamesFollowRule(pattern, $"matching /{pattern}/", scope), except);
    }

    /// <summary>
    /// MR003: every column stored as <see cref="decimal"/> - value objects converted to one
    /// included - has an explicit precision, via <c>HasPrecision</c>, a <c>HavePrecision</c>
    /// convention, or an explicit column type.
    /// </summary>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder DecimalsHavePrecision(Action<ModelRuleExclusions>? except = null) =>
        Add(new DecimalsHavePrecisionRule(), except);

    /// <summary>
    /// MR004: every column stored as <see cref="string"/> - enums and value objects converted to
    /// one included - has a max length, or an explicit column type when an unbounded type such as
    /// <c>text</c> is intended.
    /// </summary>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder StringsHaveMaxLength(Action<ModelRuleExclusions>? except = null) =>
        Add(new StringsHaveMaxLengthRule(), except);

    /// <summary>
    /// MR005: every property's C# nullability agrees with whether its column allows NULL - e.g. a
    /// <c>string</c> (not <c>string?</c>) property configured with <c>IsRequired(false)</c>.
    /// Members in code without nullable annotations are skipped.
    /// </summary>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder NullabilityMatchesClr(Action<ModelRuleExclusions>? except = null) =>
        Add(new NullabilityMatchesClrRule(), except);

    /// <summary>
    /// MR006: every enum property is stored as a string rather than its underlying number, and
    /// so is every element of a primitive collection of enums (<c>List&lt;Status&gt;</c>,
    /// <c>Status[]</c>), in a column or inside a JSON document (<c>ToJson()</c>). An enum the
    /// provider maps to a database enum type, such as a PostgreSQL enum, passes, except a single
    /// enum inside a JSON owned type, which Npgsql writes as a number.
    /// </summary>
    /// <remarks>
    /// <c>configurationBuilder.Properties&lt;Enum&gt;().HaveConversion&lt;string&gt;()</c>
    /// converts enum properties but not collection elements; convert those with
    /// <c>PrimitiveCollection(x =&gt; x.Statuses).ElementType().HasConversion&lt;string&gt;()</c>.
    /// </remarks>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder EnumsStoredAsStrings(Action<ModelRuleExclusions>? except = null) =>
        Add(new EnumsStoredAsStringsRule(), except);

    /// <summary>
    /// MR007: every table, view, sequence and database function lives in one schema. With
    /// <paramref name="schema"/>, that schema; without it, whichever schema most tables already
    /// use.
    /// </summary>
    /// <remarks>
    /// A table with no schema of its own is in the model's default schema
    /// (<c>HasDefaultSchema</c>). Without one, the database's default applies (<c>dbo</c>,
    /// <c>public</c>), which the model doesn't name, so <c>SingleSchema("dbo")</c> needs
    /// <c>modelBuilder.HasDefaultSchema("dbo")</c>.
    /// </remarks>
    /// <param name="schema">The schema every table must use, or <see langword="null"/> to only
    /// require that they all agree.</param>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="schema"/> is empty or whitespace.
    /// </exception>
    public ModelRulesBuilder SingleSchema(
        string? schema = null,
        Action<ModelRuleExclusions>? except = null)
    {
        if (schema is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        }

        return Add(new SingleSchemaRule(schema), except);
    }

    /// <summary>
    /// MR008: no relationship between two aggregate roots deletes by cascade. EF Core cascades
    /// every required relationship by default, so a reference from one root to another (an order
    /// to its customer, a country to its currency) turns deleting the referenced root into a
    /// mass delete.
    /// </summary>
    /// <param name="isAggregateRoot">Tells whether an entity CLR type is an aggregate root.</param>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder NoCascadeDeleteAcrossAggregates(
        Func<Type, bool> isAggregateRoot,
        Action<ModelRuleExclusions>? except = null)
    {
        ArgumentNullException.ThrowIfNull(isAggregateRoot);
        return Add(new NoCascadeDeleteAcrossAggregatesRule(isAggregateRoot), except);
    }

    /// <summary>
    /// MR008: no relationship between two aggregate roots deletes by cascade, where an aggregate
    /// root is any entity type assignable to <typeparamref name="TAggregateRoot"/> (typically a
    /// marker interface or base class).
    /// </summary>
    /// <typeparam name="TAggregateRoot">The aggregate root marker type.</typeparam>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder NoCascadeDeleteAcrossAggregates<TAggregateRoot>(
        Action<ModelRuleExclusions>? except = null) =>
        NoCascadeDeleteAcrossAggregates(
            type => typeof(TAggregateRoot).IsAssignableFrom(type),
            except);

    /// <summary>
    /// MR009: no database identifier in <paramref name="scope"/> is longer than
    /// <paramref name="maxLength"/> characters (63 on PostgreSQL, 128 on SQL Server).
    /// </summary>
    /// <param name="maxLength">The longest identifier allowed.</param>
    /// <param name="scope">Which kinds of identifiers to check.</param>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxLength"/> isn't positive, or <paramref name="scope"/> is
    /// <see cref="NamingScope.None"/>.
    /// </exception>
    public ModelRulesBuilder MaxIdentifierLength(
        int maxLength,
        NamingScope scope = NamingScope.All,
        Action<ModelRuleExclusions>? except = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);
        ThrowIfNone(scope);
        return Add(new MaxIdentifierLengthRule(maxLength, scope), except);
    }

    /// <summary>
    /// MR010: every entity type assignable to <typeparamref name="TMarker"/> has a query filter,
    /// e.g. every <c>ISoftDeletable</c> or <c>ITenantOwned</c> entity. Forgetting one on a new
    /// entity type leaks soft-deleted rows, or another tenant's data, into every query. The filter
    /// must be on the root of the entity type's hierarchy, since EF Core only applies it there.
    /// </summary>
    /// <remarks>
    /// Checks that a filter exists, not what it filters: an entity type needing two filters (soft
    /// delete and tenant) passes as long as it has one.
    /// </remarks>
    /// <typeparam name="TMarker">The marker type whose entity types need a filter.</typeparam>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder EntitiesHaveQueryFilter<TMarker>(
        Action<ModelRuleExclusions>? except = null) =>
        Add(
            new EntitiesHaveQueryFilterRule(
                type => typeof(TMarker).IsAssignableFrom(type),
                $"implements {typeof(TMarker).Name}"),
            except);

    /// <summary>
    /// MR010: every entity type <paramref name="requiresFilter"/> selects has a query filter, on
    /// the root of its hierarchy.
    /// </summary>
    /// <param name="requiresFilter">Tells whether an entity CLR type needs a query filter.</param>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder EntitiesHaveQueryFilter(
        Func<Type, bool> requiresFilter,
        Action<ModelRuleExclusions>? except = null)
    {
        ArgumentNullException.ThrowIfNull(requiresFilter);
        return Add(new EntitiesHaveQueryFilterRule(requiresFilter, "needs a query filter"), except);
    }

    /// <summary>
    /// MR011: no relationship uses <c>DeleteBehavior.ClientSetNull</c> (EF Core's default for
    /// optional relationships) or <c>DeleteBehavior.ClientCascade</c>. Both leave the database
    /// constraint at NO ACTION and only affect dependents EF Core is tracking, so deleting a
    /// principal whose dependents aren't loaded - or deleting from SQL or <c>ExecuteDelete</c> -
    /// fails with a foreign key violation.
    /// </summary>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder NoClientSideDeleteBehaviors(
        Action<ModelRuleExclusions>? except = null) =>
        Add(new NoClientSideDeleteBehaviorsRule(), except);

    /// <summary>
    /// MR012: no navigation leads from one aggregate root to another. Aggregates reference each
    /// other by key only; a navigation such as <c>order.Customer</c> invites loading and changing
    /// two aggregates in one unit of work. Navigations from a root to its own children are fine.
    /// </summary>
    /// <param name="isAggregateRoot">Tells whether an entity CLR type is an aggregate root.</param>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder NoNavigationsAcrossAggregates(
        Func<Type, bool> isAggregateRoot,
        Action<ModelRuleExclusions>? except = null)
    {
        ArgumentNullException.ThrowIfNull(isAggregateRoot);
        return Add(new NoNavigationsAcrossAggregatesRule(isAggregateRoot), except);
    }

    /// <summary>
    /// MR012: no navigation leads from one aggregate root to another, where an aggregate root is
    /// any entity type assignable to <typeparamref name="TAggregateRoot"/>.
    /// </summary>
    /// <typeparam name="TAggregateRoot">The aggregate root marker type.</typeparam>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder NoNavigationsAcrossAggregates<TAggregateRoot>(
        Action<ModelRuleExclusions>? except = null) =>
        NoNavigationsAcrossAggregates(
            type => typeof(TAggregateRoot).IsAssignableFrom(type),
            except);

    /// <summary>
    /// MR013: every aggregate root has a concurrency token (a row version, PostgreSQL's
    /// <c>xmin</c>, or any property marked <c>IsConcurrencyToken()</c>), so two requests updating
    /// the same aggregate can't silently overwrite each other. In a hierarchy of roots, a token on
    /// the topmost root covers every derived type.
    /// </summary>
    /// <param name="isAggregateRoot">Tells whether an entity CLR type is an aggregate root.</param>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder AggregateRootsHaveConcurrencyToken(
        Func<Type, bool> isAggregateRoot,
        Action<ModelRuleExclusions>? except = null)
    {
        ArgumentNullException.ThrowIfNull(isAggregateRoot);
        return Add(new AggregateRootsHaveConcurrencyTokenRule(isAggregateRoot), except);
    }

    /// <summary>
    /// MR013: every aggregate root has a concurrency token, where an aggregate root is any entity
    /// type assignable to <typeparamref name="TAggregateRoot"/>.
    /// </summary>
    /// <typeparam name="TAggregateRoot">The aggregate root marker type.</typeparam>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder AggregateRootsHaveConcurrencyToken<TAggregateRoot>(
        Action<ModelRuleExclusions>? except = null) =>
        AggregateRootsHaveConcurrencyToken(
            type => typeof(TAggregateRoot).IsAssignableFrom(type),
            except);

    /// <summary>
    /// MR014: no index is made redundant by another index or key on the same table whose columns
    /// start with the same columns in the same order, e.g. an index on <c>(customer_id)</c> next
    /// to one on <c>(customer_id, created_at)</c>. Filtered indexes and indexes with
    /// provider-specific settings are never compared, and a unique index only counts as redundant
    /// when it duplicates another unique index or key exactly.
    /// </summary>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder NoRedundantIndexes(Action<ModelRuleExclusions>? except = null) =>
        Add(new NoRedundantIndexesRule(), except);

    /// <summary>
    /// Adds a custom rule.
    /// </summary>
    /// <param name="rule">The rule to add.</param>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder Add(IModelRule rule, Action<ModelRuleExclusions>? except = null)
    {
        ArgumentNullException.ThrowIfNull(rule);

        ModelRuleExclusions exclusions = new();
        except?.Invoke(exclusions);
        _rules.Add((rule, exclusions));
        return this;
    }

    /// <summary>
    /// Opts entity types or members out of every rule.
    /// </summary>
    /// <param name="except">The opt-outs to apply to every rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder Except(Action<ModelRuleExclusions> except)
    {
        ArgumentNullException.ThrowIfNull(except);
        except(_globalExclusions);
        return this;
    }

    /// <summary>
    /// A rule with no identifiers in scope could never report anything, so it would pass
    /// without checking the model.
    /// </summary>
    private static void ThrowIfNone(NamingScope scope)
    {
        if (scope == NamingScope.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scope),
                scope,
                "The scope has no kinds of identifiers in it, so the rule would check nothing.");
        }
    }

    internal static ModelRuleSet Build(Action<ModelRulesBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        ModelRulesBuilder builder = new();
        configure(builder);
        return new ModelRuleSet([.. builder._rules], builder._globalExclusions);
    }
}
