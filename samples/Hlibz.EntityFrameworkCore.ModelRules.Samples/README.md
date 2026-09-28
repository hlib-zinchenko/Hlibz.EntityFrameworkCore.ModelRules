# Sample: a bookstore model, a real database, and a real failure

`CatalogDbContext` (`Author`, `Book`, `Customer`, `Order` in `Catalog/`) is registered with every
built-in rule and follows every one of them: snake_case names, decimal precision, string max
lengths, C# nullability matching column nullability, the enum stored as a string, one schema, no
cascade delete between `Order` and `Customer` (both aggregate roots), and no identifier over 63
characters. `Migrations/InitialCreate` is the migration that model produces, and
`docker-compose.yml` runs the PostgreSQL it targets.

This sample exists to show that the rules don't just run at `dotnet run`: they run at
`dotnet ef migrations add` too, because that command builds the model the same way. Break a rule
and the command fails before writing a migration file, with every violation listed - not just the
first.

## Run it clean

```bash
docker compose -f samples/Hlibz.EntityFrameworkCore.ModelRules.Samples/docker-compose.yml up -d

dotnet run --project samples/Hlibz.EntityFrameworkCore.ModelRules.Samples
# Building CatalogDbContext's model...
# Model passes every rule.

dotnet ef database update \
  --project samples/Hlibz.EntityFrameworkCore.ModelRules.Samples \
  --startup-project samples/Hlibz.EntityFrameworkCore.ModelRules.Samples
```

`database update` applies the checked-in `InitialCreate` migration. Look at what actually landed
in Postgres:

```bash
docker compose -f samples/Hlibz.EntityFrameworkCore.ModelRules.Samples/docker-compose.yml \
  exec postgres psql -U catalog -d catalog -c "\d catalog.books"
```

## Break it

Undo a fix or two in `Catalog/CatalogDbContext.cs` - for example, drop `.HasMaxLength(300)` from
`Book.Title` and `.HasPrecision(10, 2)` from `Book.Price` - then try to add a migration:

```bash
dotnet ef migrations add BrokenDemo \
  --project samples/Hlibz.EntityFrameworkCore.ModelRules.Samples \
  --startup-project samples/Hlibz.EntityFrameworkCore.ModelRules.Samples
```

It fails immediately, with both violations reported at once:

```text
Unable to create a 'DbContext' of type 'CatalogDbContext'. The exception 'The EF Core model has 2 model rule violations:
  - MR003 DecimalsHavePrecision: Book.Price: decimal column has no precision, so its store type falls back to the provider's default (unconstrained numeric on PostgreSQL, decimal(18,2) with silent truncation on SQL Server). Configure HasPrecision(precision, scale).
  - MR004 StringsHaveMaxLength: Book.Title: string column has no max length. Configure HasMaxLength(...), or HasColumnType(...) if an unbounded type is intended.' was thrown while attempting to create an instance.
```

No `BrokenDemo` migration file gets written - `dotnet ef migrations list` still shows only
`InitialCreate`. Put the two fixes back (or `git checkout` the file) and both `dotnet run` and
`dotnet ef migrations add` are clean again.

Worth trying too: revert the `AuthorId` property on `Book` back to nothing, so EF Core has to
invent a shadow foreign key again. One missing property, but the shadow column, its foreign key and
its index are all named in EF's own PascalCase, so it trips MR001 once and MR002 three more times -
a good illustration of why NoShadowProperties exists: a shadow property never goes through the
naming convention the rest of the model follows.

## Requires

- Docker, for `docker-compose.yml`'s PostgreSQL container. Mapped to host port **5433**, not 5432,
  since 5432 is a common default that's often already taken.
- The `dotnet-ef` tool (`dotnet tool install --global dotnet-ef` if `dotnet ef --version` doesn't
  resolve).

See the root [README](../../README.md) for how to fix each rule, register only the ones you want,
and exclude specific entities or members from a rule.
