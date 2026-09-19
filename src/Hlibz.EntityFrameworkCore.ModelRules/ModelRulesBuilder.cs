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
    /// discriminators and owned types' synthetic keys are allowed.
    /// </summary>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder NoShadowProperties(Action<ModelRuleExclusions>? except = null) =>
        Add(new NoShadowPropertiesRule(), except);

    /// <summary>
    /// MR002: every database identifier in <paramref name="scope"/> - schemas, tables, views,
    /// columns (complex-type columns included), key, foreign key and index names - matches
    /// <paramref name="style"/>. Checks the final names, however they were produced (a naming
    /// convention plugin, <c>HasColumnName</c>, EF defaults).
    /// </summary>
    /// <param name="style">The naming style every identifier must match.</param>
    /// <param name="scope">Which kinds of identifiers to check.</param>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder NamesFollow(
        NamingStyle style,
        NamingScope scope = NamingScope.All,
        Action<ModelRuleExclusions>? except = null) =>
        Add(NamesFollowRule.For(style, scope), except);

    /// <summary>
    /// MR002: every database identifier in <paramref name="scope"/> matches a custom
    /// <paramref name="pattern"/>. Anchor the pattern (<c>^...$</c>) to match whole names.
    /// </summary>
    /// <param name="pattern">The pattern every identifier must match.</param>
    /// <param name="scope">Which kinds of identifiers to check.</param>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder NamesFollow(
        Regex pattern,
        NamingScope scope = NamingScope.All,
        Action<ModelRuleExclusions>? except = null)
    {
        ArgumentNullException.ThrowIfNull(pattern);
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
    /// MR006: every enum property is stored as a string rather than its underlying number.
    /// </summary>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder EnumsStoredAsStrings(Action<ModelRuleExclusions>? except = null) =>
        Add(new EnumsStoredAsStringsRule(), except);

    /// <summary>
    /// MR007: every table and view lives in one schema. With <paramref name="schema"/>, that
    /// schema; without it, whichever schema most tables already use.
    /// </summary>
    /// <param name="schema">The schema every table must use, or <see langword="null"/> to only
    /// require that they all agree.</param>
    /// <param name="except">Optional opt-outs for this rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    public ModelRulesBuilder SingleSchema(
        string? schema = null,
        Action<ModelRuleExclusions>? except = null) =>
        Add(new SingleSchemaRule(schema), except);

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
    public ModelRulesBuilder MaxIdentifierLength(
        int maxLength,
        NamingScope scope = NamingScope.All,
        Action<ModelRuleExclusions>? except = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);
        return Add(new MaxIdentifierLengthRule(maxLength, scope), except);
    }

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

    internal static ModelRuleSet Build(Action<ModelRulesBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        ModelRulesBuilder builder = new();
        configure(builder);
        return new ModelRuleSet([.. builder._rules], builder._globalExclusions);
    }
}
