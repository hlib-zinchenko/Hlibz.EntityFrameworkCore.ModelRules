# Sample: a model with violations

A small bookstore model - `Author`, `Book`, `Customer`, `Order` - registered with every built-in
rule, that deliberately breaks each one exactly once. Run it to see what a
`ModelRuleViolationException` actually looks like:

```bash
dotnet run --project samples/Hlibz.EntityFrameworkCore.ModelRules.Samples
```

Building the model never opens a database connection, so nothing needs to be running first.

`Catalog/Entities.cs` and `Catalog/CatalogDbContext.cs` comment each deliberate mistake with the
rule it breaks. `Book` is where most of them live: no `AuthorId` property, a `decimal` with no
precision, a `string` with no max length, an enum with no string conversion, a nullable C# property
mapped as NOT NULL, and a primary key name well past PostgreSQL's 63-character limit. `Order` adds
a schema that disagrees with the rest of the model, and a cascade delete to `Customer`, another
aggregate root.

One violation is worth noticing on its own: `Book`'s missing `AuthorId` doesn't just trip MR001. EF
Core still needs *some* column for that foreign key, so it invents a shadow property named
`AuthorId` and a matching `FK_books_authors_AuthorId` constraint and `IX_books_AuthorId` index, all
in EF's own PascalCase - which then trips MR002 three more times. One missing property, four
reported violations. That's the shape of the problem NoShadowProperties exists to catch: a shadow
property never goes through the naming convention the rest of the model follows.

See the root [README](../../README.md) for how to fix each rule, register only the ones you want,
and exclude specific entities or members from a rule.
