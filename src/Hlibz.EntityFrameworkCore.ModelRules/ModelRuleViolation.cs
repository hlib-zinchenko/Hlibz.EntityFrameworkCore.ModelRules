using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules;

/// <summary>
/// One place where the model breaks a rule.
/// </summary>
public sealed class ModelRuleViolation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModelRuleViolation"/> class.
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
    /// Gets the display name of the entity type the violation belongs to.
    /// </summary>
    public string EntityTypeName { get; }

    /// <summary>
    /// Gets the CLR type of the entity type the violation belongs to. For shared-type entity types
    /// (e.g. implicit many-to-many join tables) this is the property-bag type, such as
    /// <c>Dictionary&lt;string, object&gt;</c>.
    /// </summary>
    public Type EntityClrType { get; }

    /// <summary>
    /// Gets the offending member relative to the entity type, dotted through complex properties
    /// (e.g. <c>Address.City</c>), or <see langword="null"/> for an entity-level violation.
    /// </summary>
    public string? MemberPath { get; }

    /// <summary>
    /// Gets what is wrong and how to fix it.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the entity type name, plus the member path when there is one, e.g.
    /// <c>Blog.Address.City</c>.
    /// </summary>
    public string Target => MemberPath is null ? EntityTypeName : $"{EntityTypeName}.{MemberPath}";

    /// <summary>
    /// Gets the entity type's full model name, used to match name-based exclusions.
    /// </summary>
    internal string EntityTypeModelName { get; }

    /// <inheritdoc />
    public override string ToString() => $"{RuleId} {RuleName}: {Target}: {Message}";
}
