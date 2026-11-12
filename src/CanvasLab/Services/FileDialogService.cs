using System.IO;
using CanvasLab.Models;
using Microsoft.Win32;

namespace CanvasLab.Services;

public sealed class FileDialogService : IFileDialogService
{
    public string? ChooseImageToOpen()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Image",
            Filter = "Supported images|*.jpg;*.jpeg;*.png;*.webp|JPEG images|*.jpg;*.jpeg|PNG images|*.png|WebP images|*.webp",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            DereferenceLinks = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public SaveImageSelection? ChooseImageToSave(string sourceFileName, ImageFileFormat format)
    {
        var extension = format switch { ImageFileFormat.Jpeg => ".jpg", ImageFileFormat.Png => ".png", _ => ".webp" };
        var filter = format switch
        {
            ImageFileFormat.Jpeg => "JPEG image|*.jpg;*.jpeg",
            ImageFileFormat.Png => "PNG image|*.png",
            _ => "WebP image|*.webp"
        };
        var sourceStem = Path.GetFileNameWithoutExtension(sourceFileName);
        var dialog = new SaveFileDialog
        {
            Title = "Save Image",
            FileName = $"{sourceStem}-smaller{extension}",
            DefaultExt = extension,
            AddExtension = true,
            Filter = filter,
            OverwritePrompt = true,
            CheckPathExists = true
        };
        if (dialog.ShowDialog() != true) return null;
        return new SaveImageSelection(dialog.FileName, File.Exists(dialog.FileName));
    }
}

