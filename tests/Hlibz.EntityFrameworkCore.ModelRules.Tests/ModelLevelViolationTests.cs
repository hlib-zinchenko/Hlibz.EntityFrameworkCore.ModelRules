using Microsoft.EntityFrameworkCore.Metadata;

namespace Hlibz.EntityFrameworkCore.ModelRules.Tests;

/// <summary>
/// Violations about something other than an entity type, reported through the target-based
/// <see cref="ModelRuleViolation"/> constructor.
/// </summary>
public sealed class ModelLevelViolationTests
{
    private const string SequenceTarget = "sequence sales.OrderNumbers";

    [Fact]
    public void Add_WithModelLevelRule_ReportsViolationWithoutEntityType()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            ModelWithSequence,
            rules => rules.Add(new SequencesAreLowerCaseRule()));

        ModelRuleViolation violation = Assert.Single(violations);
        Assert.Equal(SequenceTarget, violation.Target);
        Assert.Null(violation.EntityClrType);
        Assert.Null(violation.EntityTypeName);
        Assert.Null(violation.MemberPath);
        Assert.Equal(
            "X002 SequencesAreLowerCase: sequence sales.OrderNumbers: must be lowercase.",
            violation.ToString());
    }

    [Fact]
    public void EntityExclusions_WithModelLevelViolation_DoNotMatchIt()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            ModelWithSequence,
            rules => rules
                .Add(
                    new SequencesAreLowerCaseRule(),
                    except => except
                        .Entity<object>()
                        .Entity("OrderNumbers")
                        .Property<object>("OrderNumbers"))
                .Except(except => except.Entity<object>()));

        Assert.Equal(SequenceTarget, Assert.Single(violations).Target);
    }

    [Fact]
    public void Where_WithModelLevelTarget_ExcludesViolation()
    {
        IReadOnlyList<ModelRuleViolation> violations = TestDbContext.Validate(
            ModelWithSequence,
            rules => rules.Add(
                new SequencesAreLowerCaseRule(),
                except => except.Where(violation => violation.Target == SequenceTarget)));

        Assert.Empty(violations);
    }

    [Fact]
    public void Verify_WithModelLevelViolation_ListsItInExceptionMessage()
    {
        using TestDbContext context = new(
            ModelWithSequence,
            conventions => conventions.UseModelRules(
                rules => rules.Add(new SequencesAreLowerCaseRule())));

        ModelRuleViolationException exception =
            Assert.Throws<ModelRuleViolationException>(() => ModelRuleVerifier.Verify(context));

        Assert.Contains(
            "  - X002 SequencesAreLowerCase: sequence sales.OrderNumbers: must be lowercase.",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithBlankTarget_ThrowsArgumentException(string target)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new ModelRuleViolation(new SequencesAreLowerCaseRule(), target, "message"));

        Assert.Equal("target", exception.ParamName);
    }

    private static void ModelWithSequence(ModelBuilder model)
    {
        Models.Blog(model);
        model.HasSequence<long>("OrderNumbers", "sales");
    }

    private sealed class SequencesAreLowerCaseRule : IModelRule
    {
        public string Id => "X002";

        public string Name => "SequencesAreLowerCase";

        public IEnumerable<ModelRuleViolation> Validate(IReadOnlyModel model) =>
            model.GetSequences()
                .Where(sequence => sequence.Name != sequence.Name.ToLowerInvariant())
                .Select(sequence => new ModelRuleViolation(
                    this,
                    $"sequence {sequence.Schema}.{sequence.Name}",
                    "must be lowercase."));
    }
}
