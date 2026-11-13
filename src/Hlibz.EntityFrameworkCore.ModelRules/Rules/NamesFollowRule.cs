using System.Text.RegularExpressions;

using Hlibz.EntityFrameworkCore.ModelRules.Internal;

using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Rules;

/// <summary>
/// MR002: every database identifier in scope matches a naming style. The built-in styles end
/// with <c>\z</c> rather than <c>$</c>, which would also accept a trailing newline.
/// </summary>
internal sealed class NamesFollowRule(Regex pattern, string styleName, NamingScope scope)
    : ModelRule("MR002", "NamesFollow")
{
    // The built-in patterns are linear, so this is never reached; it only bounds the worst case.
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    public static NamesFollowRule For(NamingStyle style, NamingScope scope) =>
        style switch
        {
            NamingStyle.SnakeCase => new NamesFollowRule(
                new Regex(@"^[a-z][a-z0-9]*(_[a-z0-9]+)*\z", RegexOptions.CultureInvariant, MatchTimeout),
                "snake_case",
                scope),
            NamingStyle.UpperSnakeCase => new NamesFollowRule(
                new Regex(@"^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*\z", RegexOptions.CultureInvariant, MatchTimeout),
                "UPPER_SNAKE_CASE",
                scope),
            NamingStyle.LowerCase => new NamesFollowRule(
                new Regex(@"^[a-z][a-z0-9]*\z", RegexOptions.CultureInvariant, MatchTimeout),
                "lowercase",
                scope),
            NamingStyle.CamelCase => new NamesFollowRule(
                new Regex(@"^[a-z][a-zA-Z0-9]*\z", RegexOptions.CultureInvariant, MatchTimeout),
                "camelCase",
                scope),
            NamingStyle.PascalCase => new NamesFollowRule(
                new Regex(@"^[A-Z][a-zA-Z0-9]*\z", RegexOptions.CultureInvariant, MatchTimeout),
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
                    identifier,
                    $"{identifier.Kind} name '{identifier.Name}' is not {styleName}.");
            }
        }
    }
}
