using System.Text.RegularExpressions;

using Hlibz.EntityFrameworkCore.ModelRules.Internal;

using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Rules;

/// <summary>
/// MR002: every database identifier in scope matches a naming style.
/// </summary>
internal sealed class NamesFollowRule(Regex pattern, string styleName, NamingScope scope)
    : ModelRule("MR002", "NamesFollow")
{
    public static NamesFollowRule For(NamingStyle style, NamingScope scope) =>
        style switch
        {
            NamingStyle.SnakeCase => new NamesFollowRule(
                new Regex("^[a-z][a-z0-9]*(_[a-z0-9]+)*$", RegexOptions.CultureInvariant),
                "snake_case",
                scope),
            NamingStyle.UpperSnakeCase => new NamesFollowRule(
                new Regex("^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$", RegexOptions.CultureInvariant),
                "UPPER_SNAKE_CASE",
                scope),
            NamingStyle.LowerCase => new NamesFollowRule(
                new Regex("^[a-z][a-z0-9]*$", RegexOptions.CultureInvariant),
                "lowercase",
                scope),
            NamingStyle.CamelCase => new NamesFollowRule(
                new Regex("^[a-z][a-zA-Z0-9]*$", RegexOptions.CultureInvariant),
                "camelCase",
                scope),
            NamingStyle.PascalCase => new NamesFollowRule(
                new Regex("^[A-Z][a-zA-Z0-9]*$", RegexOptions.CultureInvariant),
                "PascalCase",
                scope),
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, null),
        };

    public override IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model)
    {
        foreach (ModelIdentifier identifier in ModelIdentifiers.Collect(model))
        {
            if ((scope & identifier.Scope) != 0 && !pattern.IsMatch(identifier.Name))
            {
                yield return Violation(
                    identifier.EntityType,
                    identifier.MemberPath,
                    $"{identifier.Kind} name '{identifier.Name}' is not {styleName}.");
            }
        }
    }
}
