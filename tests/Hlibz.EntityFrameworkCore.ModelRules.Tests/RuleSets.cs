namespace Hlibz.EntityFrameworkCore.ModelRules.Tests;

/// <summary>
/// Rule sets shared by the provider-compatibility and database integration tests.
/// </summary>
internal static class RuleSets
{
    /// <summary>
    /// Every built-in rule except MaxIdentifierLength, whose limit is provider-specific by design.
    /// </summary>
    public static void AllProviderNeutral(ModelRulesBuilder rules) =>
        rules
            .NoShadowProperties()
            .NamesFollow(NamingStyle.SnakeCase)
            .DecimalsHavePrecision()
            .StringsHaveMaxLength()
            .NullabilityMatchesClr()
            .EnumsStoredAsStrings()
            .SingleSchema()
            .NoCascadeDeleteAcrossAggregates<IAggregateRoot>();
}
