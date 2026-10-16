using CommunityToolkit.Mvvm.ComponentModel;

namespace DevAcademy.Desktop.ViewModels;

public sealed partial class EditorFileViewModel(string path, string content) : ObservableObject
{
    public string Path { get; } = path;
    public string Name => System.IO.Path.GetFileName(Path);

    [ObservableProperty] private string _content = content;
}
