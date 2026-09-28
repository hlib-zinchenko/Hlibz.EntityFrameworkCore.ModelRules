using Hlibz.EntityFrameworkCore.ModelRules;
using Hlibz.EntityFrameworkCore.ModelRules.Samples.Catalog;

Console.WriteLine("Building CatalogDbContext's model...");
Console.WriteLine();

using CatalogDbContext context = new();

try
{
    // Same call the "Quick start" test in the README uses: builds the full design-time model,
    // which runs every rule registered in ConfigureConventions.
    ModelRules.Verify(context);
    Console.WriteLine("Model passes every rule.");
}
catch (ModelRuleViolationException exception)
{
    Console.WriteLine(exception.Message);
}
