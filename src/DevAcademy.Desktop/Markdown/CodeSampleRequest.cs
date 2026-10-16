namespace DevAcademy.Desktop.Markdown;

/// <summary>A ```lang run block the learner asked to open in the scratch editor.</summary>
public sealed record CodeSampleRequest(string LanguageId, string Code);
