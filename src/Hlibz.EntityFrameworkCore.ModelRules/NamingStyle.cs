namespace Hlibz.EntityFrameworkCore.ModelRules;

/// <summary>
/// A database identifier style checked by <see cref="ModelRulesBuilder.NamesFollow(NamingStyle, NamingScope, Action{ModelRuleExclusions}?)"/>.
/// Styles check the shape of a name, not how it was derived, so acronyms and digits are fine as
/// long as the result fits (e.g. <c>ip_address</c> and <c>line2text</c> are both snake_case).
/// </summary>
public enum NamingStyle
{
    /// <summary>
    /// Lowercase words separated by single underscores: <c>blog_posts</c>, <c>ip_address</c>.
    /// Matches EFCore.NamingConventions' <c>UseSnakeCaseNamingConvention()</c>.
    /// </summary>
    SnakeCase,

    /// <summary>
    /// Uppercase words separated by single underscores: <c>BLOG_POSTS</c>. Matches
    /// EFCore.NamingConventions' <c>UseUpperSnakeCaseNamingConvention()</c>.
    /// </summary>
    UpperSnakeCase,

    /// <summary>
    /// Lowercase letters and digits only, no separators: <c>blogposts</c>. Matches
    /// EFCore.NamingConventions' <c>UseLowerCaseNamingConvention()</c>.
    /// </summary>
    LowerCase,

    /// <summary>
    /// Letters and digits starting lowercase: <c>blogPosts</c>. Matches EFCore.NamingConventions'
    /// <c>UseCamelCaseNamingConvention()</c>.
    /// </summary>
    CamelCase,

    /// <summary>
    /// Letters and digits starting uppercase: <c>BlogPosts</c>. EF Core's own default names don't
    /// fully fit this style - constraint names such as <c>PK_Blogs</c> and complex-type columns
    /// such as <c>Address_City</c> contain underscores - so narrow the
    /// <see cref="NamingScope"/> or configure those names explicitly.
    /// </summary>
    PascalCase,
}
