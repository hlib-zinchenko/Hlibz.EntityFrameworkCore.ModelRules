using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules;

/// <summary>
/// One place where the model breaks a rule: either on an entity type (or one of its members), or
/// on something that belongs to the model as a whole, such as a sequence.
/// </summary>
public sealed class ModelRuleViolation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModelRuleViolation"/> class for a violation on
    /// an entity type or one of its members.
    /// </summary>
    /// <param name="rule">The rule that found the violation.</param>
    /// <param name="entityType">
    /// The entity type the violation belongs to. For a property of a complex type, pass the entity
    /// type that contains the complex property; for an inherited property, the type that declares
    /// it.
    /// </param>
    /// <param name="memberPath">
    /// The offending member relative to <paramref name="entityType"/>, dotted through complex
    /// properties (e.g. <c>Address.City</c>), or <see langword="null"/> when the violation is
    /// about the entity type itself (e.g. its table name).
    /// </param>
    /// <param name="message">What is wrong and how to fix it.</param>
    public ModelRuleViolation(
        IModelRule rule,
        IReadOnlyEntityType entityType,
        string? memberPath,
        string message)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        RuleId = rule.Id;
        RuleName = rule.Name;
        EntityTypeName = entityType.DisplayName();
        EntityTypeModelName = entityType.Name;
        EntityClrType = entityType.ClrType;
        MemberPath = memberPath;
        Target = memberPath is null ? EntityTypeName : $"{EntityTypeName}.{memberPath}";
        Message = message;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelRuleViolation"/> class for a violation on
    /// something that belongs to the model rather than to an entity type, such as a sequence, a
    /// mapped database function, or the model as a whole.
    /// </summary>
    /// <remarks>
    /// Entity-based exclusions (<see cref="ModelRuleExclusions.Entity{TEntity}"/>,
    /// <see cref="ModelRuleExclusions.Entity(string)"/> and the <c>Property</c> overloads) never
    /// match such a violation. Use <see cref="ModelRuleExclusions.Where"/> to exclude one.
    /// </remarks>
    /// <param name="rule">The rule that found the violation.</param>
    /// <param name="target">
    /// What the violation is about, naming its kind, e.g. <c>sequence sales.order_numbers</c> or
    /// <c>model</c>.
    /// </param>
    /// <param name="message">What is wrong and how to fix it.</param>
    public ModelRuleViolation(IModelRule rule, string target, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        RuleId = rule.Id;
        RuleName = rule.Name;
        Target = target;
        Message = message;
    }

    /// <summary>
    /// Gets the identifier of the rule that found the violation, e.g. <c>MR003</c>.
    /// </summary>
    public string RuleId { get; }

    /// <summary>
    /// Gets the name of the rule that found the violation, e.g. <c>DecimalsHavePrecision</c>.
    /// </summary>
    public string RuleName { get; }

    /// <summary>
    /// Gets the display name of the entity type the violation belongs to, or
    /// <see langword="null"/> for a violation that isn't about an entity type (e.g. a sequence).
    /// </summary>
    public string? EntityTypeName { get; }

    /// <summary>
    /// Gets the CLR type of the entity type the violation belongs to, or <see langword="null"/>
    /// for a violation that isn't about an entity type (e.g. a sequence). For shared-type entity
    /// types (e.g. implicit many-to-many join tables) this is the property-bag type, such as
    /// <c>Dictionary&lt;string, object&gt;</c>.
    /// </summary>
    public Type? EntityClrType { get; }

    /// <summary>
    /// Gets the offending member relative to the entity type, dotted through complex properties
    /// (e.g. <c>Address.City</c>), or <see langword="null"/> for a violation on the entity type
    /// itself or one that isn't about an entity type.
    /// </summary>
    public string? MemberPath { get; }

    /// <summary>
    /// Gets what is wrong and how to fix it.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets what the violation is about: the entity type name plus the member path when there is
    /// one (e.g. <c>Blog.Address.City</c>), or, for a violation that isn't about an entity type,
    /// the target its rule reported (e.g. <c>sequence sales.order_numbers</c>).
    /// </summary>
    public string Target { get; }

    /// <summary>
    /// Gets the entity type's full model name, used to match name-based exclusions.
    /// </summary>
    internal string? EntityTypeModelName { get; }

    /// <inheritdoc />
    public override string ToString() => $"{RuleId} {RuleName}: {Target}: {Message}";
}
