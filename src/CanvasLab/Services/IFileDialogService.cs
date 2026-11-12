using CanvasLab.Models;

namespace CanvasLab.Services;

public interface IFileDialogService
{
    string? ChooseImageToOpen();
    SaveImageSelection? ChooseImageToSave(string sourceFileName, ImageFileFormat format);
}

