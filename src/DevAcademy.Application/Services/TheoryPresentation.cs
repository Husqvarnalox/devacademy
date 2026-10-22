using DevAcademy.Domain;

namespace DevAcademy.Application.Services;

/// <param name="Sections">Section keys in the order the section switch shows them.</param>
/// <param name="Initial">The section the lesson opens on.</param>
public sealed record TheoryPlan(IReadOnlyList<string> Sections, string Initial);

/// <summary>
/// Chooses how a lesson's theory is presented from the learner's prior knowledge and chosen depth. Nothing is ever
/// removed: "everything on one page" still shows every section, and every section in <see cref="TheoryPlan.Sections"/>
/// stays one click away.
/// <list type="bullet">
/// <item>Absolute beginner: content order, opens on the first section; the <c>programmer</c> summary moves to the end.</item>
/// <item>Language beginner: opens on <c>programmer</c> (a compact summary for people who know another language) when the
/// lesson has one, and puts it first.</item>
/// <item>Intermediate and advanced: like the language beginner, and without a <c>programmer</c> section opens on
/// <c>precise</c> instead of the plain-language introduction.</item>
/// <item>Depth Quick hides <c>deep-dive</c> from the switch (it stays in the full page); Standard and Deep keep it.</item>
/// </list>
/// </summary>
public static class TheoryPresentation
{
    public const string Programmer = "programmer";
    public const string DeepDive = "deep-dive";
    public const string Precise = "precise";

    public static TheoryPlan Plan(IReadOnlyList<string> sections, int priorKnowledge, LearningDepth depth)
    {
        var level = PriorKnowledge.Clamp(priorKnowledge);
        var list = sections.Where(s => depth != LearningDepth.Quick || s != DeepDive).ToList();
        if (list.Count == 0) return new TheoryPlan([], sections.FirstOrDefault() ?? "");

        var hasProgrammer = list.Remove(Programmer);
        if (hasProgrammer)
        {
            if (level == PriorKnowledge.AbsoluteBeginner) list.Add(Programmer);
            else list.Insert(0, Programmer);
        }

        var initial = level switch
        {
            PriorKnowledge.AbsoluteBeginner => list[0],
            _ when hasProgrammer => Programmer,
            >= PriorKnowledge.Intermediate when list.Contains(Precise) => Precise,
            _ => list[0],
        };
        return new TheoryPlan(list, initial);
    }
}
