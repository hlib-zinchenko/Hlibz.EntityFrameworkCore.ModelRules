using System.Linq.Expressions;

namespace Hlibz.EntityFrameworkCore.ModelRules;

/// <summary>
/// Opt-outs for a rule (or, via <see cref="ModelRulesBuilder.Except"/>, for every rule). A
/// violation matching any exclusion is dropped.
/// </summary>
/// <remarks>
/// The entity and member exclusions only match violations on entity types. A violation on
/// something else, such as a sequence, can only be excluded with <see cref="Where"/>.
/// </remarks>
public sealed class ModelRuleExclusions
{
    private readonly List<Func<ModelRuleViolation, bool>> _matchers = [];

    /// <summary>
    /// Excludes every violation on <typeparamref name="TEntity"/> and on entity types derived from
    /// it, including violations on its members and on the types it owns (<c>OwnsOne</c>,
    /// <c>OwnsMany</c>).
    /// </summary>
    /// <typeparam name="TEntity">The entity type to exclude.</typeparam>
    /// <returns>The same instance, for chaining.</returns>
    public ModelRuleExclusions Entity<TEntity>()
    {
        _matchers.Add(violation =>
            violation.EntityClrType is { } entityClrType
            && (typeof(TEntity).IsAssignableFrom(entityClrType)
                || violation.Owners.Any(owner => typeof(TEntity).IsAssignableFrom(owner.ClrType))));
        return this;
    }

    /// <summary>
    /// Excludes every violation on the entity type with the given name, including violations on
    /// its members and on the types it owns. Matches the model name (e.g. <c>MyApp.Blog</c> or a
    /// shared-type name such as <c>BlogTag</c>), the display name, or the CLR type's short name.
    /// </summary>
    /// <param name="entityTypeName">The entity type name to exclude.</param>
    /// <returns>The same instance, for chaining.</returns>
    public ModelRuleExclusions Entity(string entityTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityTypeName);
        _matchers.Add(violation => MatchesEntityName(violation, entityTypeName));
        return this;
    }

    /// <summary>
    /// Excludes violations on one member of <typeparamref name="TEntity"/>: a property, a
    /// navigation, or a property of a complex or owned type (<c>b =&gt; b.Address.City</c>).
    /// </summary>
    /// <typeparam name="TEntity">The entity type that has the member.</typeparam>
    /// <param name="member">The member access expression, e.g. <c>b =&gt; b.Price</c>.</param>
    /// <returns>The same instance, for chaining.</returns>
    public ModelRuleExclusions Property<TEntity>(Expression<Func<TEntity, object?>> member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return Property<TEntity>(GetMemberPath(member, nameof(member)));
    }

    /// <summary>
    /// Excludes violations on one member of <typeparamref name="TEntity"/> by name. Use this for
    /// members with no CLR property, such as shadow properties, or dotted paths through complex
    /// properties or owned types (<c>Address.City</c>). An owned type's own violations, such as
    /// its table name, are on the path of its navigation (<c>Address</c>).
    /// </summary>
    /// <typeparam name="TEntity">The entity type that has the member.</typeparam>
    /// <param name="memberPath">The member name or dotted member path.</param>
    /// <returns>The same instance, for chaining.</returns>
    public ModelRuleExclusions Property<TEntity>(string memberPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberPath);

        // A member declared on a base type is reported against that base type, so excluding it via
        // a derived type must match the base too - and excluding it via a base type must match
        // derived types that re-map it.
        _matchers.Add(violation =>
            violation.EntityClrType is { } entityClrType
            && ((string.Equals(violation.MemberPath, memberPath, StringComparison.Ordinal)
                 && IsRelated(entityClrType))
                || violation.Owners.Any(owner =>
                    string.Equals(owner.MemberPath, memberPath, StringComparison.Ordinal)
                    && IsRelated(owner.ClrType))));
        return this;

        static bool IsRelated(Type clrType) =>
            typeof(TEntity).IsAssignableFrom(clrType) || clrType.IsAssignableFrom(typeof(TEntity));
    }

    /// <summary>
    /// Excludes every violation the predicate returns <see langword="true"/> for.
    /// </summary>
    /// <param name="predicate">The predicate to test each violation with.</param>
    /// <returns>The same instance, for chaining.</returns>
    public ModelRuleExclusions Where(Func<ModelRuleViolation, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _matchers.Add(predicate);
        return this;
    }

    internal bool Matches(ModelRuleViolation violation) =>
        _matchers.Exists(matcher => matcher(violation));

    private static bool MatchesEntityName(ModelRuleViolation violation, string name) =>
        violation.EntityClrType is { } entityClrType
        && (MatchesName(
                violation.EntityTypeModelName,
                violation.EntityTypeName,
                entityClrType,
                name)
            || violation.Owners.Any(owner =>
                MatchesName(owner.ModelName, owner.DisplayName, owner.ClrType, name)));

    private static bool MatchesName(
        string? modelName,
        string? displayName,
        Type clrType,
        string name) =>
        string.Equals(modelName, name, StringComparison.Ordinal)
        || string.Equals(displayName, name, StringComparison.Ordinal)
        || string.Equals(clrType.Name, name, StringComparison.Ordinal);

    private static string GetMemberPath(LambdaExpression lambda, string paramName)
    {
        Expression body = lambda.Body;
        while (body is UnaryExpression
               {
                   NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked,
               } unary)
        {
            body = unary.Operand;
        }

        Stack<string> segments = new();
        while (body is MemberExpression memberExpression)
        {
            segments.Push(memberExpression.Member.Name);
            body = memberExpression.Expression!;
        }

        if (body is not ParameterExpression || segments.Count == 0)
        {
            throw new ArgumentException(
                $"'{lambda}' is not a member access expression such as 'e => e.Property' or "
                + "'e => e.ComplexProperty.Property'.",
                paramName);
        }

        return string.Join('.', segments);
    }
}
