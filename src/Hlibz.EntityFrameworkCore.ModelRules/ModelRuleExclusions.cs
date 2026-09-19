using System.Linq.Expressions;

namespace Hlibz.EntityFrameworkCore.ModelRules;

/// <summary>
/// Opt-outs for a rule (or, via <see cref="ModelRulesBuilder.Except"/>, for every rule). A
/// violation matching any exclusion is dropped.
/// </summary>
public sealed class ModelRuleExclusions
{
    private readonly List<Func<ModelRuleViolation, bool>> _matchers = [];

    /// <summary>
    /// Excludes every violation on <typeparamref name="TEntity"/> and on entity types derived from
    /// it, including violations on its members.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to exclude.</typeparam>
    /// <returns>The same instance, for chaining.</returns>
    public ModelRuleExclusions Entity<TEntity>()
    {
        _matchers.Add(violation => typeof(TEntity).IsAssignableFrom(violation.EntityClrType));
        return this;
    }

    /// <summary>
    /// Excludes every violation on the entity type with the given name, including violations on
    /// its members. Matches the model name (e.g. <c>MyApp.Blog</c> or a shared-type name such as
    /// <c>BlogTag</c>), the display name, or the CLR type's short name.
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
    /// navigation, or a property of a complex type (<c>b =&gt; b.Address.City</c>).
    /// </summary>
    /// <typeparam name="TEntity">The entity type that has the member.</typeparam>
    /// <param name="member">The member access expression, e.g. <c>b =&gt; b.Price</c>.</param>
    /// <returns>The same instance, for chaining.</returns>
    public ModelRuleExclusions Property<TEntity>(Expression<Func<TEntity, object?>> member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return Property<TEntity>(GetMemberPath(member));
    }

    /// <summary>
    /// Excludes violations on one member of <typeparamref name="TEntity"/> by name. Use this for
    /// members with no CLR property, such as shadow properties, or dotted paths through complex
    /// properties (<c>Address.City</c>).
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
            string.Equals(violation.MemberPath, memberPath, StringComparison.Ordinal)
            && (typeof(TEntity).IsAssignableFrom(violation.EntityClrType)
                || violation.EntityClrType.IsAssignableFrom(typeof(TEntity))));
        return this;
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
        string.Equals(violation.EntityTypeModelName, name, StringComparison.Ordinal)
        || string.Equals(violation.EntityTypeName, name, StringComparison.Ordinal)
        || string.Equals(violation.EntityClrType.Name, name, StringComparison.Ordinal);

    private static string GetMemberPath(LambdaExpression lambda)
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
                nameof(lambda));
        }

        return string.Join('.', segments);
    }
}
