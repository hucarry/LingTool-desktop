using Photino.NET;

namespace ToolHub.App;

internal interface IFileDialogService
{
    string? ShowPythonPicker(PhotinoWindow window, string? defaultPath);

    string? ShowFilePicker(
        PhotinoWindow window,
        string title,
        string? defaultPath,
        string? filter,
        string? purpose
    );
}
