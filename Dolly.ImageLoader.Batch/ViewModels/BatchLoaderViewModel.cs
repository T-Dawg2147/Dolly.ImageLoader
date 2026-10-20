using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dolly.ImageLoader.Core.Constants;
using Dolly.ImageLoader.Core.Enums;
using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Core.Models;
using Dolly.ImageLoader.Core.Options;
using Dolly.ImageLoader.Wpf.Shared.Comparers;
using Dolly.ImageLoader.Wpf.Shared.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace Dolly.ImageLoader.Batch.ViewModels;

public partial class BatchLoaderViewModel : ObservableObject
{
    private readonly IProductRepository _productRepository;
    private readonly IAssetReferenceRepository _assetReferenceRepository;
    private readonly IBadImageRepository _badImageRepository;
    private readonly IImageSlotViewModelFactory _imageSlotViewModelFactory;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly IImageNormalizationService _imageNormalizationService;
    private readonly ISaveWorkflowService _saveWorkflowService;
    private readonly PathOptions _pathOptions;
    private readonly ILogger<BatchLoaderViewModel> _logger;

    private readonly Dictionary<string, BatchTargetEditSession> _sessions = new();
    private readonly Dictionary<string, BatchProductNodeViewModel> _nodesByKey = new();

    [ObservableProperty]
    private string _groupSearchText = string.Empty;

    [ObservableProperty]
    private bool _isGroupPassThrough;

    [ObservableProperty]
    private bool _isBusy;

    partial void OnIsBusyChanged(bool value)
    {
        SaveAllCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanSaveAll));
        OnPropertyChanged(nameof(IsInteractionLocked));
    }

    [ObservableProperty]
    private bool _isLoadingGroup;

    partial void OnIsLoadingGroupChanged(bool value) => RefreshInteractionState();

    [ObservableProperty]
    private bool _isAddingImages;

    partial void OnIsAddingImagesChanged(bool value) => RefreshInteractionState();

    [ObservableProperty]
    private bool _isAssigningImage;

    partial void OnIsAssigningImageChanged(bool value) => RefreshInteractionState();

    [ObservableProperty]
    private bool _isSaving;

    partial void OnIsSavingChanged(bool value) => RefreshInteractionState();

    [ObservableProperty]
    private string _statusMessage = "Ready.";

    [ObservableProperty]
    private BatchProductNodeViewModel? _selectedTreeNode;

    partial void OnSelectedTreeNodeChanged(BatchProductNodeViewModel? oldValue, BatchProductNodeViewModel? newValue)
    {
        if (oldValue?.Product is not null)
        {
            CaptureCurrentNodeState(oldValue);
        }

        if (newValue?.Product is not null)
        {
            _ = LoadSelectedNodeSlotsAsync(newValue);
        }
        else
        {
            SelectedTargetMode = null;
            SelectedProduct = null;
            SelectedPrimarySlot = null;
            SelectedSecondarySlots.Clear();
            SelectedSlot = null;
            RaiseSecondarySlotPropertiesChanged();
        }
    }

    [ObservableProperty]
    private ProductContext? _selectedProduct;

    [ObservableProperty]
    private string? _selectedTargetMode;

    [ObservableProperty]
    private ImageSlotViewModel? _selectedPrimarySlot;

    [ObservableProperty]
    private ImageSlotViewModel? _selectedSlot;

    public ObservableCollection<BatchProductNodeViewModel> ProductTree { get; } = new();
    public ObservableCollection<ImageSlotViewModel> SelectedSecondarySlots { get; } = new();
    public ObservableCollection<BatchImagePoolItemViewModel> ImagePool { get; } = new();

    public ImageSlotViewModel? SecondarySlot1 => GetSecondarySlot(0);
    public ImageSlotViewModel? SecondarySlot2 => GetSecondarySlot(1);
    public ImageSlotViewModel? SecondarySlot3 => GetSecondarySlot(2);
    public ImageSlotViewModel? SecondarySlot4 => GetSecondarySlot(3);
    public ImageSlotViewModel? SecondarySlot5 => GetSecondarySlot(4);
    public ImageSlotViewModel? SecondarySlot6 => GetSecondarySlot(5);
    public ImageSlotViewModel? SecondarySlot7 => GetSecondarySlot(6);
    public ImageSlotViewModel? SecondarySlot8 => GetSecondarySlot(7);
    public ImageSlotViewModel? SecondarySlot9 => GetSecondarySlot(8);

    public int PendingTargetCount => _sessions.Values.Count(x => x.HasPendingChanges);

    public bool HasUnsavedChanges => PendingTargetCount > 0;

    /// <summary>True once any save succeeded this session; never cleared by saving.</summary>
    public bool ChangesSavedThisSession { get; private set; }

    /// <summary>True if the user closed the window and discarded unsaved changes.</summary>
    public bool ClosedWithDiscardedChanges { get; private set; }

    /// <summary>True if the supplied group could not be loaded.</summary>
    public bool GroupLoadFailed { get; private set; }

    public async Task InitializeForGroupAsync(string groupId)
    {
        IsGroupPassThrough = true;
        GroupSearchText = groupId;
        await LoadGroupAsync();
        GroupLoadFailed = ProductTree.Count == 0;
    }

    public bool CanSaveAll => !IsBusy && PendingTargetCount > 0;

    public bool IsInteractionLocked =>
        IsBusy ||
        IsLoadingGroup ||
        IsAddingImages ||
        IsAssigningImage ||
        IsSaving;

    public BatchLoaderViewModel(
        IProductRepository productRepository,
        IAssetReferenceRepository assetReferenceRepository,
        IBadImageRepository badImageRepository,
        IImageSlotViewModelFactory imageSlotViewModelFactory,
        IImageProcessingService imageProcessingService,
        IImageNormalizationService imageNormalizationService,
        ISaveWorkflowService saveWorkflowService,
        IOptions<PathOptions> pathOptions,
        ILogger<BatchLoaderViewModel> logger)
    {
        _productRepository = productRepository;
        _assetReferenceRepository = assetReferenceRepository;
        _badImageRepository = badImageRepository;
        _imageSlotViewModelFactory = imageSlotViewModelFactory;
        _imageProcessingService = imageProcessingService;
        _imageNormalizationService = imageNormalizationService;
        _saveWorkflowService = saveWorkflowService;
        _pathOptions = pathOptions.Value;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadGroupAsync()
    {
        if (IsInteractionLocked)
        {
            return;
        }

        try
        {
            IsLoadingGroup = true;
            IsBusy = true;
            StatusMessage = "Loading group...";

            ProductTree.Clear();
            _sessions.Clear();
            _nodesByKey.Clear();

            SelectedTargetMode = null;
            SelectedProduct = null;
            SelectedPrimarySlot = null;
            SelectedSecondarySlots.Clear();
            SelectedSlot = null;
            RaiseSecondarySlotPropertiesChanged();

            if (string.IsNullOrWhiteSpace(GroupSearchText))
            {
                StatusMessage = "Enter a group ID.";
                return;
            }

            var groupId = GroupSearchText.Trim();
            var products = await _productRepository.GetByParentGroupIdAsync(groupId, CancellationToken.None);

            if (products.Count == 0)
            {
                StatusMessage = $"No products found for group '{groupId}'.";
                return;
            }

            // Try the group's own row first (may be missing or have no Base_Code).
            var groupProduct = await _productRepository.GetByIdAsync(groupId, CancellationToken.None);

            var groupBaseCode = groupProduct?.BaseCode?.Trim();

            // Fall back to the children's BaseCode (the most common non-empty one).
            if (string.IsNullOrWhiteSpace(groupBaseCode))
            {
                var childBaseCodes = products
                    .Select(p => p.BaseCode?.Trim())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .GroupBy(c => c!, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Count())
                    .ToList();

                groupBaseCode = childBaseCodes.FirstOrDefault()?.Key ?? string.Empty;

                if (childBaseCodes.Count > 1)
                {
                    _logger.LogWarning(
                        "Group {GroupId} children have {Count} different BaseCodes ({Codes}); using '{Chosen}'.",
                        groupId,
                        childBaseCodes.Count,
                        string.Join(", ", childBaseCodes.Select(g => g.Key)),
                        groupBaseCode);
                }
            }

            if (string.IsNullOrWhiteSpace(groupBaseCode))
            {
                _logger.LogWarning("Group {GroupId} has no BaseCode on the group or its children.", groupId);
            }

            var root = new BatchProductNodeViewModel
            {
                Title = groupId,
                IsGroup = true,
                Product = new ProductContext
                {
                    ProductId = groupId,
                    ProductName = groupProduct?.ProductName ?? groupId,
                    ParentGroupId = string.Empty,
                    BaseCode = groupBaseCode ?? string.Empty,
                    SupplierProductCode = groupProduct?.SupplierProductCode ?? string.Empty
                }
            };

            RegisterNode(root);

            foreach (var product in products)
            {
                var child = new BatchProductNodeViewModel
                {
                    Title = $"{product.ProductId}",
                    IsGroup = false,
                    Product = product
                };

                root.Children.Add(child);
                RegisterNode(child);
            }

            ProductTree.Add(root);
            SelectedTreeNode = root;

            RaisePendingStateChanged();

            StatusMessage = $"Loaded group target {groupId}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load group {GroupSearchText}", GroupSearchText);
            StatusMessage = $"Load group failed: {ex.Message}";
        }
        finally
        {
            IsLoadingGroup = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddImagesAsync()
    {
        if (IsInteractionLocked)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Select images",
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tif;*.tiff;*.gif",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await AddImageFilesAsync(dialog.FileNames);
    }

    [RelayCommand]
    private void ClearImagePool()
    {
        if (IsInteractionLocked)
        {
            return;
        }

        if (ImagePool.Count == 0)
        {
            StatusMessage = "Image pool is already empty.";
            return;
        }

        ImagePool.Clear();
        StatusMessage = "Cleared all images from the image pool.";
    }

    [RelayCommand]
    private void RemovePoolImage(BatchImagePoolItemViewModel? item)
    {
        if (IsInteractionLocked)
        {
            return;
        }

        if (item is null)
        {
            return;
        }

        ImagePool.Remove(item);
        StatusMessage = $"Removed {item.FileName} from image pool.";
    }

    [RelayCommand]
    private void SelectSlot(ImageSlotViewModel? slot)
    {
        if (IsInteractionLocked)
        {
            return;
        }

        if (slot is null)
        {
            return;
        }

        SelectedSlot = slot;
        StatusMessage = $"{slot.DisplayLabel} selected for assignment.";
    }

    [RelayCommand]
    private async Task AssignPoolImageToSelectedSlotAsync(BatchImagePoolItemViewModel? item)
    {
        if (IsInteractionLocked)
        {
            return;
        }

        if (item is null)
        {
            return;
        }

        if (SelectedSlot is null)
        {
            StatusMessage = "Select an image slot first, then choose an image from the pool.";
            return;
        }

        await AssignPoolImageToSlotAsync(item, SelectedSlot);
    }

    [RelayCommand(CanExecute = nameof(CanSaveAll))]
    private async Task SaveAllAsync()
    {
        if (IsInteractionLocked)
        {
            return;
        }

        try
        {
            IsSaving = true;
            IsBusy = true;

            CaptureCurrentSelectedNodeState();

            var sessionsToSave = _sessions.Values
                .Where(x => x.HasPendingChanges)
                .OrderBy(x => x.TargetMode == TargetMode.Group ? 0 : 1)
                .ThenBy(x => x.Product.ProductId)
                .ToList();

            if (sessionsToSave.Count == 0)
            {
                StatusMessage = "There are no pending batch changes to save.";
                return;
            }

            var successCount = 0;
            var failureCount = 0;
            var failures = new List<string>();

            foreach (var session in sessionsToSave)
            {
                StatusMessage = $"Saving {session.TargetMode.ToString().ToLowerInvariant()} target {session.Product.ProductId}...";

                var request = new SaveRequest
                {
                    ProductContext = session.Product,
                    TargetMode = session.TargetMode,
                    Slots = session.Slots.Select(CloneSlotState).ToList(),
                    Username = Environment.UserName
                };

                // TODO:
                // Need to re-write this to upload all images at once. 
                // Currently, this code runs PER Product/Product Group.
                // This creates an issue where multiple .zip files are created with the EXACT same name
                // meaning the last item is the only item that pushes images.
                var result = await _saveWorkflowService.ExecuteAsync(request, CancellationToken.None);

                if (result.Success)
                {
                    successCount++;
                    ChangesSavedThisSession = true;

                    var refreshedSession = await ReloadTargetSessionAsync(session);

                    _sessions[session.Key] = refreshedSession;

                    if (_nodesByKey.TryGetValue(session.Key, out var node))
                    {
                        node.HasPendingChanges = false;
                    }

                    if (SelectedTreeNode is not null && BuildSessionKey(SelectedTreeNode) == session.Key)
                    {
                        LoadSlotsFromSession(refreshedSession);
                        SelectedTargetMode = refreshedSession.TargetMode.ToString();
                        SelectedProduct = refreshedSession.Product;
                        SelectedSlot = null;
                    }
                }
                else
                {
                    failureCount++;
                    failures.Add($"{session.TargetMode}:{session.Product.ProductId} - {result.Message}");
                }
            }

            RaisePendingStateChanged();

            if (failureCount == 0)
            {
                StatusMessage = $"Saved and refreshed {successCount} batch target(s) successfully.";
            }
            else
            {
                StatusMessage = $"Saved/refreshed {successCount} target(s), {failureCount} failed. First failure: {failures.First()}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch Save All failed.");
            StatusMessage = $"Batch save failed: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
            IsBusy = false;
        }
    }

    public bool CanClose()
    {
        CaptureCurrentSelectedNodeState();

        if (!HasUnsavedChanges)
        {
            return true;
        }

        var result = MessageBox.Show(
            "You have unsaved batch changes. If you close this window, those changes will be lost.\n\nDo you want to close anyway?",
            "Unsaved Batch Changes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        ClosedWithDiscardedChanges = result == MessageBoxResult.Yes;
        return ClosedWithDiscardedChanges;
    }

    public async Task AddImageFilesAsync(IEnumerable<string> filePaths)
    {
        if (IsAddingImages || IsInteractionLocked && !IsAddingImages)
        {
            _logger.LogInformation("Skipping duplicate AddImageFilesAsync call because an add operation is already in progress.");
            return;
        }

        try
        {
            IsAddingImages = true;
            IsBusy = true;

            StatusMessage = "Inspecting images...";

            var distinctFiles = filePaths
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(Path.GetFileName, NaturalStringComparer.Instance!)
                .ToList();

            if (distinctFiles.Count == 0)
            {
                StatusMessage = "No new images were added.";
                return;
            }

            var inspectionResults = await _imageNormalizationService.InspectAsync(distinctFiles, CancellationToken.None);
            var filesNeedingNormalization = inspectionResults
                .Where(x => x.IsSuccess && x.NeedsNormalization)
                .ToList();

            IReadOnlyList<string> filesToAdd;

            if (filesNeedingNormalization.Count > 0)
            {
                IsBusy = false;
                var promptResult = MessageBox.Show(
                    $"Dolly found {filesNeedingNormalization.Count} image(s) that would benefit from normalization.\n\n" +
                    "Normalization will:\n" +
                    "- convert supported formats to JPEG\n" +
                    "- normalize DPI metadata to 72\n" +
                    "- keep your original files untouched\n\n" +
                    "Would you like to normalize those images now?",
                    "Normalize Images",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);
                IsBusy = true;

                if (promptResult == MessageBoxResult.Cancel)
                {
                    StatusMessage = "Add images cancelled.";
                    return;
                }

                if (promptResult == MessageBoxResult.Yes)
                {
                    StatusMessage = "Normalizing images...";
                    var normalizationResults = await _imageNormalizationService.NormalizeIfNeededAsync(distinctFiles, CancellationToken.None);

                    var failed = normalizationResults.Where(x => !x.IsSuccess).ToList();
                    if (failed.Count > 0)
                    {
                        StatusMessage = $"Some images could not be normalized. First failure: {failed[0].Message}";
                    }

                    filesToAdd = normalizationResults
                        .Where(x => x.IsSuccess)
                        .Select(x => x.OutputFilePath)
                        .OrderBy(Path.GetFileName, NaturalStringComparer.Instance)
                        .ToList();

                    var normalizedCount = normalizationResults.Count(x => x.WasNormalized);
                    if (normalizedCount > 0)
                    {
                        StatusMessage = $"Added images to pool. {normalizedCount} image(s) were normalized first.";
                    }
                }
                else
                {
                    filesToAdd = distinctFiles;
                }
            }
            else
            {
                filesToAdd = distinctFiles;
            }

            StatusMessage = "Adding images to the pool...";
            AddImageFilesToPool(filesToAdd);
        }
        finally
        {
            IsAddingImages = false;
            IsBusy = false;
        }
    }

    public void AddImageFiles(IEnumerable<string> filePaths)
    {
        if (IsAddingImages)
        {
            _logger.LogInformation("Skipping duplicate AddImageFiles call because an add operation is already in progress.");
            return;
        }

        _ = AddImageFilesAsync(filePaths);
    }

    private void AddImageFilesToPool(IEnumerable<string> filePaths)
    {
        var files = filePaths
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(Path.GetFileName, NaturalStringComparer.Instance)
            .ToList();

        var existingPaths = ImagePool
            .Select(x => x.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newItems = files
            .Where(path => !existingPaths.Contains(path))
            .Select(BatchImagePoolItemViewModel.FromFile)
            .OrderBy(x => x.FileName, NaturalStringComparer.Instance)
            .ToList();

        foreach (var item in newItems)
        {
            ImagePool.Add(item);
        }

        if (newItems.Count > 0)
        {
            StatusMessage = $"Added {newItems.Count} image(s) to the image pool.";
        }
        else if (string.IsNullOrWhiteSpace(StatusMessage) || StatusMessage == "Ready.")
        {
            StatusMessage = "No new images were added.";
        }
    }

    public async Task AssignPoolImageToSlotAsync(BatchImagePoolItemViewModel item, ImageSlotViewModel slot)
    {
        if (IsAssigningImage || IsInteractionLocked && !IsAssigningImage)
        {
            return;
        }

        try
        {
            IsAssigningImage = true;
            IsBusy = true;
            StatusMessage = $"Assigning {item.FileName} to {slot.DisplayLabel}...";

            var validation = await _imageProcessingService.ValidateImageAsync(item.FilePath, CancellationToken.None);
            if (!validation.IsValid)
            {
                slot.HasValidationError = true;
                slot.ValidationMessage = validation.Message;
                StatusMessage = $"Cannot assign {item.FileName}: {validation.Message}";
                return;
            }

            slot.HasValidationError = false;
            slot.ValidationMessage = null;

            if (validation.HasWarning)
            {
                slot.HasWarning = true;
                slot.WarningMessage = validation.WarningMessage;
            }

            slot.PendingSourceFilePath = item.FilePath;
            slot.PendingAction = PendingActionType.Replace;

            SelectedSlot = slot;
            MarkCurrentNodeAsDirty();
            StatusMessage = validation.HasWarning
                ? $"{item.FileName} assigned to {slot.DisplayLabel} with warning."
                : $"{item.FileName} assigned to {slot.DisplayLabel}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign pool image {FileName} to slot {SlotLabel}", item.FileName, slot.DisplayLabel);
            StatusMessage = $"Assignment failed: {ex.Message}";
        }
        finally
        {
            IsAssigningImage = false;
            IsBusy = false;
        }
    }

    private async Task<BatchTargetEditSession> ReloadTargetSessionAsync(BatchTargetEditSession session)
    {
        var targetId = session.Product.ProductId;

        var assetReferences = await _assetReferenceRepository.GetAssetReferencesAsync(targetId, CancellationToken.None);

        var badRefs = await _badImageRepository.GetBadReferenceTypesAsync(
            targetId,
            session.Product.ProductName,
            session.TargetMode == TargetMode.Product,
            CancellationToken.None);

        var refreshedSlots = ImageSlotDefinitions.All
            .Select(def =>
            {
                var currentAssetName = ResolveCurrentAssetNameForSlot(def, assetReferences);

                return new ImageSlotState
                {
                    Definition = def,
                    CurrentAssetName = currentAssetName,
                    CurrentImagePath = BuildPreviewImagePath(session.Product, session.TargetMode, def, currentAssetName),
                    PendingSourceFilePath = null,
                    PendingAction = PendingActionType.None,
                    HasWarning = badRefs.Contains(def.ReferenceType),
                    WarningMessage = badRefs.Contains(def.ReferenceType)
                        ? $"Bad image warning for {def.ReferenceType}"
                        : null
                };
            })
            .ToList();

        return new BatchTargetEditSession
        {
            Key = session.Key,
            Product = session.Product,
            TargetMode = session.TargetMode,
            Slots = refreshedSlots,
            HasPendingChanges = false
        };
    }

    private async Task LoadSelectedNodeSlotsAsync(BatchProductNodeViewModel node, bool preferSessionState = true)
    {
        if (node.Product is null)
        {
            return;
        }

        try
        {
            IsBusy = true;

            var targetMode = node.IsGroup ? TargetMode.Group : TargetMode.Product;
            var targetId = node.Product.ProductId;
            var sessionKey = BuildSessionKey(node);

            SelectedTargetMode = targetMode.ToString();
            SelectedProduct = node.Product;
            SelectedPrimarySlot = null;
            SelectedSecondarySlots.Clear();
            SelectedSlot = null;

            StatusMessage = $"Loading {targetMode.ToString().ToLowerInvariant()} images for {targetId}...";

            if (preferSessionState && _sessions.TryGetValue(sessionKey, out var existingSession))
            {
                LoadSlotsFromSession(existingSession);
                StatusMessage = $"Loaded cached {targetMode.ToString().ToLowerInvariant()} target {targetId}.";
                return;
            }

            var assetReferences = await _assetReferenceRepository.GetAssetReferencesAsync(targetId, CancellationToken.None);

            var badRefs = await _badImageRepository.GetBadReferenceTypesAsync(
                targetId,
                node.Product.ProductName,
                targetMode == TargetMode.Product,
                CancellationToken.None);

            var slots = ImageSlotDefinitions.All
                .Select(def =>
                {
                    var currentAssetName = ResolveCurrentAssetNameForSlot(def, assetReferences);
                    var vm = _imageSlotViewModelFactory.Create(def);
                    vm.CurrentAssetName = currentAssetName;
                    vm.CurrentImagePath = BuildPreviewImagePath(node.Product, targetMode, def, currentAssetName);
                    vm.HasWarning = badRefs.Contains(def.ReferenceType);
                    vm.WarningMessage = badRefs.Contains(def.ReferenceType)
                        ? $"Bad image warning for {def.ReferenceType}"
                        : null;
                    HookSlot(vm);
                    return vm;
                })
                .ToList();

            SelectedPrimarySlot = slots.FirstOrDefault(x => x.Definition.Kind == ImageSlotKind.Primary);

            foreach (var slot in slots.Where(x => x.Definition.Kind == ImageSlotKind.Secondary))
            {
                SelectedSecondarySlots.Add(slot);
            }

            RaiseSecondarySlotPropertiesChanged();

            _sessions[sessionKey] = new BatchTargetEditSession
            {
                Key = sessionKey,
                Product = node.Product,
                TargetMode = targetMode,
                Slots = BuildSlotStatesFromCurrentUi(),
                HasPendingChanges = false
            };

            node.HasPendingChanges = false;

            RaisePendingStateChanged();

            StatusMessage = $"Loaded {targetMode.ToString().ToLowerInvariant()} target {targetId}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load slots for selected node {NodeTitle}", node.Title);
            StatusMessage = $"Load target failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadSlotsFromSession(BatchTargetEditSession session)
    {
        SelectedPrimarySlot = null;
        SelectedSecondarySlots.Clear();

        var slots = session.Slots
            .Select(state =>
            {
                var vm = _imageSlotViewModelFactory.Create(state.Definition);
                vm.CurrentAssetName = state.CurrentAssetName;
                vm.CurrentImagePath = state.CurrentImagePath;
                vm.PendingSourceFilePath = state.PendingSourceFilePath;
                vm.PendingAction = state.PendingAction;
                vm.HasWarning = state.HasWarning;
                vm.WarningMessage = state.WarningMessage;
                HookSlot(vm);
                return vm;
            })
            .ToList();

        SelectedPrimarySlot = slots.FirstOrDefault(x => x.Definition.Kind == ImageSlotKind.Primary);

        foreach (var slot in slots.Where(x => x.Definition.Kind == ImageSlotKind.Secondary))
        {
            SelectedSecondarySlots.Add(slot);
        }

        RaiseSecondarySlotPropertiesChanged();
    }

    private ImageSlotViewModel? GetSecondarySlot(int index)
    {
        return index >= 0 && index < SelectedSecondarySlots.Count
            ? SelectedSecondarySlots[index]
            : null;
    }

    private void RaiseSecondarySlotPropertiesChanged()
    {
        OnPropertyChanged(nameof(SecondarySlot1));
        OnPropertyChanged(nameof(SecondarySlot2));
        OnPropertyChanged(nameof(SecondarySlot3));
        OnPropertyChanged(nameof(SecondarySlot4));
        OnPropertyChanged(nameof(SecondarySlot5));
        OnPropertyChanged(nameof(SecondarySlot6));
        OnPropertyChanged(nameof(SecondarySlot7));
        OnPropertyChanged(nameof(SecondarySlot8));
        OnPropertyChanged(nameof(SecondarySlot9));
    }

    private void CaptureCurrentSelectedNodeState()
    {
        if (SelectedTreeNode?.Product is null)
        {
            return;
        }

        CaptureCurrentNodeState(SelectedTreeNode);
    }

    private void CaptureCurrentNodeState(BatchProductNodeViewModel node)
    {
        if (node.Product is null)
        {
            return;
        }

        var key = BuildSessionKey(node);

        if (!_sessions.TryGetValue(key, out var session))
        {
            return;
        }

        session.Slots = BuildSlotStatesFromCurrentUi();
        session.HasPendingChanges = session.Slots.Any(x => x.PendingAction != PendingActionType.None);

        node.HasPendingChanges = session.HasPendingChanges;

        RaisePendingStateChanged();
    }

    private List<ImageSlotState> BuildSlotStatesFromCurrentUi()
    {
        var states = new List<ImageSlotState>();

        if (SelectedPrimarySlot is not null)
        {
            states.Add(SelectedPrimarySlot.ToState());
        }

        states.AddRange(SelectedSecondarySlots.Select(x => x.ToState()));
        return states;
    }

    private void MarkCurrentNodeAsDirty()
    {
        if (SelectedTreeNode?.Product is null)
        {
            return;
        }

        var key = BuildSessionKey(SelectedTreeNode);

        if (_sessions.TryGetValue(key, out var session))
        {
            session.Slots = BuildSlotStatesFromCurrentUi();
            session.HasPendingChanges = session.Slots.Any(x => x.PendingAction != PendingActionType.None);
        }

        SelectedTreeNode.HasPendingChanges = true;

        RaisePendingStateChanged();
    }

    private void RaisePendingStateChanged()
    {
        OnPropertyChanged(nameof(PendingTargetCount));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CanSaveAll));
        OnPropertyChanged(nameof(IsInteractionLocked));
        SaveAllCommand.NotifyCanExecuteChanged();
    }

    private void RefreshInteractionState()
    {
        OnPropertyChanged(nameof(IsInteractionLocked));
    }

    private void RegisterNode(BatchProductNodeViewModel node)
    {
        if (node.Product is null)
        {
            return;
        }

        _nodesByKey[BuildSessionKey(node)] = node;
    }

    private static string BuildSessionKey(BatchProductNodeViewModel node)
    {
        var mode = node.IsGroup ? TargetMode.Group : TargetMode.Product;
        return $"{mode}:{node.Product?.ProductId}";
    }

    private void HookSlot(ImageSlotViewModel slot)
    {
        slot.PropertyChanged -= SlotOnPropertyChanged;
        slot.PropertyChanged += SlotOnPropertyChanged;
    }

    private void SlotOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImageSlotViewModel.PendingAction)
            or nameof(ImageSlotViewModel.PendingSourceFilePath)
            or nameof(ImageSlotViewModel.HasValidationError))
        {
            MarkCurrentNodeAsDirty();
        }
    }

    private static ImageSlotState CloneSlotState(ImageSlotState state)
    {
        return new ImageSlotState
        {
            Definition = state.Definition,
            CurrentAssetName = state.CurrentAssetName,
            CurrentImagePath = state.CurrentImagePath,
            PendingSourceFilePath = state.PendingSourceFilePath,
            PendingAction = state.PendingAction,
            HasWarning = state.HasWarning,
            WarningMessage = state.WarningMessage
        };
    }

    private static string? ResolveCurrentAssetNameForSlot(ImageSlotDefinition definition, IDictionary<string, string?> assetReferences)
    {
        if (definition.Kind == ImageSlotKind.Primary)
        {
            if (assetReferences.TryGetValue("Primary_Cut_Out", out var cutOutAssetName) &&
                !string.IsNullOrWhiteSpace(cutOutAssetName))
            {
                return cutOutAssetName;
            }

            if (assetReferences.TryGetValue("Primary_Image", out var primaryAssetName) &&
                !string.IsNullOrWhiteSpace(primaryAssetName))
            {
                return primaryAssetName;
            }

            return null;
        }

        assetReferences.TryGetValue(definition.ReferenceType, out var secondaryAssetName);
        return secondaryAssetName;
    }

    #region Preview Image Loading

    /// <summary>
    /// Resolves the preview image for a slot:
    /// 1. Local PreviewImageRoot (products only, and only if there is an asset name and the file exists).
    /// 2. Amazon S3 fallback URL (ALWAYS attempted, even if there is no asset name in the database).
    ///    - Products use their ProductId.
    ///    - Groups use their BaseCode (group images on AWS are not saved under the PGRP_ id).
    /// </summary>
    private string? BuildPreviewImagePath(
        ProductContext product,
        TargetMode targetMode,
        ImageSlotDefinition definition,
        string? assetName)
    {
        if (targetMode != TargetMode.Group && !string.IsNullOrWhiteSpace(assetName))
        {
            var localPath = BuildLocalPreviewImagePath(assetName);
            if (!string.IsNullOrWhiteSpace(localPath))
                return localPath;
        }

        return BuildFallbackPreviewUrl(product, targetMode, definition);
    }

    private string? BuildLocalPreviewImagePath(string assetName)
    {
        try
        {
            var root = Environment.ExpandEnvironmentVariables(_pathOptions.PreviewImageRoot);
            if (string.IsNullOrWhiteSpace(root)) return null;
            var fullPath = Path.Combine(root, $"{assetName}.jpg");
            return File.Exists(fullPath) ? fullPath : null;
        }
        catch (Exception) { return null; }
    }

    private string? BuildFallbackPreviewUrl(
        ProductContext product,
        TargetMode targetMode,
        ImageSlotDefinition definition)
    {
        var code = ResolveFallbackPreviewKeyStem(product, targetMode);
        if (string.IsNullOrWhiteSpace(code))
            return null;

        if (definition.Kind == ImageSlotKind.Primary)
        {
            return BuildAmazonPreviewUrl(code, isPrimary: true);
        }

        var slotNumber = ResolveSecondarySlotNumber(definition);
        return slotNumber is null
            ? null
            : BuildAmazonPreviewUrl(code, isPrimary: false, slotNumber);
    }

    /// <summary>
    /// Builds the Amazon S3 preview URL from a product / base code.
    /// AWS file names are all lower case, so the code is lower-cased.
    /// Primary:   {FallbackPreviewPrimaryBaseUrl}/{code}.jpg     e.g. http://slingsby.s3.amazonaws.com/p/l/440145.jpg
    /// Secondary: {FallbackPreviewAltBaseUrl}/{code}_{n}.jpg     e.g. http://slingsby.s3.amazonaws.com/p/alt/l/440145_1.jpg
    /// </summary>
    private string? BuildAmazonPreviewUrl(string? code, bool isPrimary, int? secondarySlotNumber = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        code = code.Trim().ToLowerInvariant();

        if (isPrimary)
        {
            var primaryBase = _pathOptions.FallbackPreviewPrimaryBaseUrl;
            return string.IsNullOrWhiteSpace(primaryBase)
                ? null
                : $"{primaryBase.TrimEnd('/')}/{code}.jpg";
        }

        if (secondarySlotNumber is not > 0)
            return null;

        var altBase = _pathOptions.FallbackPreviewAltBaseUrl;
        return string.IsNullOrWhiteSpace(altBase)
            ? null
            : $"{altBase.TrimEnd('/')}/{code}_{secondarySlotNumber}.jpg";
    }

    /// <summary>
    /// Secondary slot number (1-based) = position of this slot among the secondary definitions.
    /// </summary>
    private static int? ResolveSecondarySlotNumber(ImageSlotDefinition definition)
    {
        var secondaries = ImageSlotDefinitions.All
            .Where(x => x.Kind == ImageSlotKind.Secondary)
            .ToList();

        var index = secondaries.FindIndex(x => x.ReferenceType == definition.ReferenceType);
        return index >= 0 ? index + 1 : null;
    }

    /// <summary>
    /// Groups -> BaseCode. Products -> ProductId.
    /// </summary>
    private static string? ResolveFallbackPreviewKeyStem(ProductContext product, TargetMode targetMode)
    {
        if (targetMode == TargetMode.Group)
            return string.IsNullOrWhiteSpace(product.BaseCode)
                ? null
                : product.BaseCode;

        return string.IsNullOrWhiteSpace(product.ProductId)
            ? null
            : product.ProductId;
    }

    #endregion

    private sealed class BatchTargetEditSession
    {
        public string Key { get; set; } = string.Empty;
        public ProductContext Product { get; set; } = new();
        public TargetMode TargetMode { get; set; }
        public List<ImageSlotState> Slots { get; set; } = new();
        public bool HasPendingChanges { get; set; }
    }
}