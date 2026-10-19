using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dolly.ImageLoader.Core.Enums;
using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Core.Models;
using Microsoft.Win32;

namespace Dolly.ImageLoader.Wpf.Shared.ViewModels;

public partial class ImageSlotViewModel : ObservableObject
{
    private readonly IImageProcessingService _imageProcessingService;

    [ObservableProperty]
    private string? currentAssetName;

    partial void OnCurrentAssetNameChanged(string? value)
    {
        OnPropertyChanged(nameof(CurrentAssetDisplayText));
        OnPropertyChanged(nameof(CanClear));
        OnPropertyChanged(nameof(CanRevert));
    }

    [ObservableProperty]
    private string? currentImagePath;

    partial void OnCurrentImagePathChanged(string? value)
    {
        if (PendingAction == PendingActionType.None)
        {
            PreviewImage = LoadImage(value);
        }

        OnPropertyChanged(nameof(CanClear));
        OnPropertyChanged(nameof(CanRevert));
    }

    [ObservableProperty]
    private string? pendingSourceFilePath;

    partial void OnPendingSourceFilePathChanged(string? value)
    {
        OnPropertyChanged(nameof(PendingActionText));
        OnPropertyChanged(nameof(HasPendingChange));
        OnPropertyChanged(nameof(CanClear));
        OnPropertyChanged(nameof(CanRevert));
    }

    [ObservableProperty]
    private PendingActionType pendingAction;

    partial void OnPendingActionChanged(PendingActionType value)
    {
        OnPropertyChanged(nameof(PendingActionText));
        OnPropertyChanged(nameof(HasPendingChange));
        OnPropertyChanged(nameof(CanClear));
        OnPropertyChanged(nameof(CanRevert));

        switch (value)
        {
            case PendingActionType.None:
                PreviewImage = LoadImage(CurrentImagePath);
                break;

            case PendingActionType.Replace:
                PreviewImage = LoadImage(PendingSourceFilePath);
                break;

            case PendingActionType.Clear:
                PreviewImage = null;
                break;
        }
    }

    [ObservableProperty]
    private bool hasWarning;

    [ObservableProperty]
    private string? warningMessage;

    [ObservableProperty]
    private ImageSource? previewImage;

    [ObservableProperty]
    private bool hasValidationError;

    partial void OnHasValidationErrorChanged(bool value)
    {
        OnPropertyChanged(nameof(HasPendingChange));
        OnPropertyChanged(nameof(CanRevert));
    }

    [ObservableProperty]
    private string? validationMessage;

    public ImageSlotDefinition Definition { get; }

    public string DisplayLabel => Definition.DisplayLabel;

    public string CurrentAssetDisplayText =>
        string.IsNullOrWhiteSpace(CurrentAssetName)
            ? "Current: (none)"
            : $"Current: {CurrentAssetName}";

    public string PendingActionText =>
        PendingAction switch
        {
            PendingActionType.Replace => $"Pending: Replace with {Path.GetFileName(PendingSourceFilePath)}",
            PendingActionType.Clear => "Pending: Clear image link",
            _ => "Pending: No changes"
        };

    public bool HasPendingChange =>
        !HasValidationError &&
        PendingAction != PendingActionType.None;

    public bool CanClear =>
        !string.IsNullOrWhiteSpace(CurrentAssetName) ||
        !string.IsNullOrWhiteSpace(PendingSourceFilePath) ||
        PendingAction == PendingActionType.Clear;

    public bool CanRevert =>
        PendingAction != PendingActionType.None ||
        HasValidationError;

    public ImageSlotViewModel(
        ImageSlotDefinition definition,
        IImageProcessingService imageProcessingService)
    {
        Definition = definition;
        _imageProcessingService = imageProcessingService;
    }

    [RelayCommand]
    private async Task ReplaceImageAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = $"Select image for {DisplayLabel}",
            Filter = "JPEG Images (*.jpg;*.jpeg)|*.jpg;*.jpeg",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var selectedPath = dialog.FileName;

        var validation = await _imageProcessingService.ValidateImageAsync(selectedPath, CancellationToken.None);
        if (!validation.IsValid)
        {
            HasValidationError = true;
            ValidationMessage = validation.Message;

            PendingSourceFilePath = null;
            PendingAction = PendingActionType.None;
            PreviewImage = LoadImage(CurrentImagePath);
            RevertImageCommand.NotifyCanExecuteChanged();
            return;
        }

        HasValidationError = false;
        ValidationMessage = null;

        if (validation.HasWarning)
        {
            HasWarning = true;
            WarningMessage = validation.WarningMessage;
        }

        PendingSourceFilePath = selectedPath;
        PendingAction = PendingActionType.Replace;

        RevertImageCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void ClearImage()
    {
        ResetPendingState(PendingActionType.Clear);
    }

    [RelayCommand(CanExecute = nameof(CanRevert))]
    private void RevertImage()
    {
        ResetPendingState(PendingActionType.None);
    }

    private void ResetPendingState(PendingActionType actionType)
    {
        HasValidationError = false;
        ValidationMessage = null;

        PendingSourceFilePath = null;
        PendingAction = actionType;

        ClearImageCommand.NotifyCanExecuteChanged();
        RevertImageCommand.NotifyCanExecuteChanged();
    }

    public ImageSlotState ToState()
    {
        return new ImageSlotState
        {
            Definition = Definition,
            CurrentAssetName = CurrentAssetName,
            CurrentImagePath = CurrentImagePath,
            PendingSourceFilePath = PendingSourceFilePath,
            PendingAction = PendingAction,
            HasWarning = HasWarning,
            WarningMessage = WarningMessage
        };
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is nameof(CurrentAssetName)
            or nameof(CurrentImagePath)
            or nameof(PendingSourceFilePath)
            or nameof(PendingAction)
            or nameof(HasValidationError))
        {
            ClearImageCommand.NotifyCanExecuteChanged();
            RevertImageCommand.NotifyCanExecuteChanged();
        }
    }

    private static BitmapImage? LoadImage(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(filePath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();

            return image;
        }
        catch
        {
            return null;
        }
    }
}
