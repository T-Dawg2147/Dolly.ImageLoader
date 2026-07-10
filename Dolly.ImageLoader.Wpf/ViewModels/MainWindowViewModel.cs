using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
using Dolly.ImageLoader.Wpf.Shared.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dolly.ImageLoader.Wpf.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IProductRepository _productRepository;
    private readonly IAssetReferenceRepository _assetReferenceRepository;
    private readonly IBadImageRepository _badImageRepository;
    private readonly ISaveWorkflowService _saveWorkflowService;
    private readonly IImageSlotViewModelFactory _imageSlotViewModelFactory;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly PathOptions _pathOptions;

    private bool _suppressModePrompt;
    private bool _suppressSearchTextChanged;
    private int _searchRequestVersion;

    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        if (_suppressSearchTextChanged)
        {
            return;
        }

        _ = UpdateSearchResultsAsync(value);
    }

    [ObservableProperty]
    private ProductContext _selectedProduct = new();

    [ObservableProperty]
    private bool _isBusy;

    partial void OnIsBusyChanged(bool value)
    {
        SaveChangesCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanSaveChanges));
    }

    [ObservableProperty]
    private string _statusMessage = "Ready.";

    [ObservableProperty]
    private ImageSlotViewModel? _primarySlot;

    partial void OnPrimarySlotChanged(ImageSlotViewModel? oldValue, ImageSlotViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= SlotOnPropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += SlotOnPropertyChanged;
        }

        RecalculatePendingChanges();
    }

    [ObservableProperty]
    private bool _isSearchResultsOpen;

    [ObservableProperty]
    private ProductSearchResultViewModel? _selectedSearchResult;

    partial void OnSelectedSearchResultChanged(ProductSearchResultViewModel? value)
    {
        HighlightedSearchResultIndex = value is null ? -1 : SearchResults.IndexOf(value);
    }

    [ObservableProperty]
    private int _highlightedSearchResultIndex = -1;

    public ObservableCollection<ProductSearchResultViewModel> SearchResults { get; } = new();
    public ObservableCollection<ImageSlotViewModel> SecondarySlots { get; } = new();

    public bool IsProductMode
    {
        get => CurrentTargetMode == TargetMode.Product;
        set
        {
            if (!value || CurrentTargetMode == TargetMode.Product)
            {
                return;
            }

            if (_suppressModePrompt)
            {
                SetProperty(ref currentTargetMode, TargetMode.Product);
                OnPropertyChanged(nameof(IsGroupMode));
                _ = LoadSlotsAsync();
                return;
            }

            if (!CanDiscardUnsavedChanges())
            {
                _suppressModePrompt = true;
                OnPropertyChanged(nameof(IsProductMode));
                OnPropertyChanged(nameof(IsGroupMode));
                _suppressModePrompt = false;
                return;
            }

            SetProperty(ref currentTargetMode, TargetMode.Product);
            OnPropertyChanged(nameof(IsGroupMode));
            _ = LoadSlotsAsync();
        }
    }

    public bool IsGroupMode
    {
        get => CurrentTargetMode == TargetMode.Group;
        set
        {
            if (!value || CurrentTargetMode == TargetMode.Group)
            {
                return;
            }

            if (_suppressModePrompt)
            {
                SetProperty(ref currentTargetMode, TargetMode.Group);
                OnPropertyChanged(nameof(IsProductMode));
                _ = LoadSlotsAsync();
                return;
            }

            if (!CanDiscardUnsavedChanges())
            {
                _suppressModePrompt = true;
                OnPropertyChanged(nameof(IsProductMode));
                OnPropertyChanged(nameof(IsGroupMode));
                _suppressModePrompt = false;
                return;
            }

            SetProperty(ref currentTargetMode, TargetMode.Group);
            OnPropertyChanged(nameof(IsProductMode));
            _ = LoadSlotsAsync();
        }
    }

    private TargetMode currentTargetMode = TargetMode.Product;
    public TargetMode CurrentTargetMode
    {
        get => currentTargetMode;
        set
        {
            if (SetProperty(ref currentTargetMode, value))
            {
                OnPropertyChanged(nameof(IsProductMode));
                OnPropertyChanged(nameof(IsGroupMode));
            }
        }
    }

    public int PendingChangesCount =>
        GetAllSlots().Count(x => x.HasPendingChange);

    public bool HasPendingChanges => PendingChangesCount > 0;

    public bool CanSaveChanges =>
        !IsBusy &&
        !string.IsNullOrWhiteSpace(SelectedProduct.ProductId) &&
        HasPendingChanges;

    public MainWindowViewModel(
        IProductRepository productRepository,
        IAssetReferenceRepository assetReferenceRepository,
        IBadImageRepository badImageRepository,
        ISaveWorkflowService saveWorkflowService,
        IImageSlotViewModelFactory imageSlotViewModelFactory,
        IOptions<PathOptions> pathOptions,
        ILogger<MainWindowViewModel> logger)
    {
        _productRepository = productRepository;
        _assetReferenceRepository = assetReferenceRepository;
        _badImageRepository = badImageRepository;
        _saveWorkflowService = saveWorkflowService;
        _imageSlotViewModelFactory = imageSlotViewModelFactory;
        _pathOptions = pathOptions.Value;
        _logger = logger;

        SecondarySlots.CollectionChanged += SecondarySlotsOnCollectionChanged;

        BuildEmptySlots();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (IsSearchResultsOpen && SelectedSearchResult is not null)
        {
            await SelectSearchResultAsync(SelectedSearchResult);
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            StatusMessage = "Enter a product search value.";
            return;
        }

        await UpdateSearchResultsAsync(SearchText, openDropdown: true);

        if (SearchResults.Count == 1)
        {
            await SelectSearchResultAsync(SearchResults[0]);
        }
    }

    [RelayCommand]
    private async Task SelectSearchResultAsync(ProductSearchResultViewModel? result)
    {
        if (result is null)
        {
            return;
        }

        try
        {
            if (!CanDiscardUnsavedChanges())
            {
                StatusMessage = "Selection cancelled.";
                return;
            }

            IsSearchResultsOpen = false;
            SearchResults.Clear();
            SelectedSearchResult = null;
            HighlightedSearchResultIndex = -1;

            await LoadSelectedProductAsync(result.Product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting search result {ProductId}", result.ProductId);
            StatusMessage = $"Selection failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CloseSearchResults()
    {
        IsSearchResultsOpen = false;
        SelectedSearchResult = null;
        HighlightedSearchResultIndex = -1;
    }

    [RelayCommand]
    private void MoveSearchSelectionDown()
    {
        if (!IsSearchResultsOpen || SearchResults.Count == 0)
        {
            return;
        }

        if (HighlightedSearchResultIndex < SearchResults.Count - 1)
        {
            HighlightedSearchResultIndex++;
        }
        else
        {
            HighlightedSearchResultIndex = 0;
        }

        SelectedSearchResult = SearchResults[HighlightedSearchResultIndex];
    }

    [RelayCommand]
    private void MoveSearchSelectionUp()
    {
        if (!IsSearchResultsOpen || SearchResults.Count == 0)
        {
            return;
        }

        if (HighlightedSearchResultIndex > 0)
        {
            HighlightedSearchResultIndex--;
        }
        else
        {
            HighlightedSearchResultIndex = SearchResults.Count - 1;
        }

        SelectedSearchResult = SearchResults[HighlightedSearchResultIndex];
    }

    [RelayCommand]
    private async Task CommitSearchSelectionAsync()
    {
        if (IsSearchResultsOpen && SelectedSearchResult is not null)
        {
            await SelectSearchResultAsync(SelectedSearchResult);
            return;
        }

        await SearchAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!CanDiscardUnsavedChanges())
        {
            StatusMessage = "Refresh cancelled.";
            return;
        }

        await LoadSlotsAsync();
    }

    [RelayCommand(CanExecute = nameof(CanSaveChanges))]
    private async Task SaveChangesAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedProduct.ProductId))
            {
                StatusMessage = "Load a product before saving.";
                return;
            }

            IsBusy = true;
            StatusMessage = $"Saving {PendingChangesCount} change(s)...";

            var slots = new List<ImageSlotState>();

            if (PrimarySlot is not null)
            {
                slots.Add(PrimarySlot.ToState());
            }

            slots.AddRange(SecondarySlots.Select(x => x.ToState()));

            var request = new SaveRequest
            {
                ProductContext = SelectedProduct,
                TargetMode = CurrentTargetMode,
                Slots = slots,
                Username = Environment.UserName
            };

            var result = await _saveWorkflowService.ExecuteAsync(request, CancellationToken.None);
            StatusMessage = result.Message;

            if (result.Success)
            {
                await LoadSlotsAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving changes.");
            StatusMessage = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            RecalculatePendingChanges();
        }
    }

    private async Task UpdateSearchResultsAsync(string? rawSearchText, bool openDropdown = true)
    {
        var requestVersion = ++_searchRequestVersion;
        var trimmedSearch = rawSearchText?.Trim() ?? string.Empty;

        if (trimmedSearch.Length < 2)
        {
            SearchResults.Clear();
            SelectedSearchResult = null;
            HighlightedSearchResultIndex = -1;
            IsSearchResultsOpen = false;
            return;
        }

        try
        {
            var results = await _productRepository.SearchProductsAsync(trimmedSearch, CancellationToken.None);

            if (requestVersion != _searchRequestVersion)
            {
                return;
            }

            SearchResults.Clear();

            foreach (var result in results)
            {
                SearchResults.Add(new ProductSearchResultViewModel
                {
                    Product = result
                });
            }

            if (SearchResults.Count > 0)
            {
                HighlightedSearchResultIndex = 0;
                SelectedSearchResult = SearchResults[0];
                IsSearchResultsOpen = openDropdown;
                StatusMessage = $"{SearchResults.Count} product(s) found.";
            }
            else
            {
                SelectedSearchResult = null;
                HighlightedSearchResultIndex = -1;
                IsSearchResultsOpen = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating search results for {SearchText}", trimmedSearch);
            StatusMessage = $"Search failed: {ex.Message}";
            IsSearchResultsOpen = false;
        }
    }

    private async Task LoadSelectedProductAsync(ProductContext product)
    {
        SelectedProduct = product;

        _suppressSearchTextChanged = true;
        SearchText = product.ProductId;
        _suppressSearchTextChanged = false;

        OnPropertyChanged(nameof(CanSaveChanges));

        StatusMessage = $"Loaded product {product.ProductId}.";
        await LoadSlotsAsync();
    }

    private async Task LoadSlotsAsync()
    {
        try
        {
            BuildEmptySlots();

            if (string.IsNullOrWhiteSpace(SelectedProduct.ProductId))
            {
                StatusMessage = "No product selected.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Loading image slots...";

            var targetId = CurrentTargetMode == TargetMode.Product
                ? SelectedProduct.ProductId
                : SelectedProduct.ParentGroupId;

            if (string.IsNullOrWhiteSpace(targetId))
            {
                StatusMessage = "Target ID is empty.";
                return;
            }

            var assetReferences = await _assetReferenceRepository.GetAssetReferencesAsync(targetId, CancellationToken.None);

            var badRefs = await _badImageRepository.GetBadReferenceTypesAsync(
                targetId,
                SelectedProduct.ProductName,
                CurrentTargetMode == TargetMode.Product,
                CancellationToken.None);

            var allSlots = ImageSlotDefinitions.All
                .Select(def =>
                {
                    var currentAssetName = ResolveCurrentAssetNameForSlot(def, assetReferences);

                    var vm = _imageSlotViewModelFactory.Create(def);
                    vm.CurrentAssetName = currentAssetName;
                    vm.CurrentImagePath = BuildPreviewImageSource(SelectedProduct, CurrentTargetMode, def, currentAssetName);
                    vm.HasWarning = badRefs.Contains(def.ReferenceType);
                    vm.WarningMessage = badRefs.Contains(def.ReferenceType)
                        ? $"Bad image warning for {def.ReferenceType}"
                        : null;

                    return vm;
                })
                .ToList();

            PrimarySlot = allSlots.FirstOrDefault(x => x.Definition.Kind == ImageSlotKind.Primary);

            SecondarySlots.Clear();
            foreach (var secondary in allSlots.Where(x => x.Definition.Kind == ImageSlotKind.Secondary))
            {
                SecondarySlots.Add(secondary);
            }

            RecalculatePendingChanges();
            StatusMessage = $"Loaded images for {targetId} ({CurrentTargetMode}).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading image slots.");
            StatusMessage = $"Load failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BuildEmptySlots()
    {
        if (PrimarySlot is not null)
        {
            PrimarySlot.PropertyChanged -= SlotOnPropertyChanged;
        }

        foreach (var slot in SecondarySlots)
        {
            slot.PropertyChanged -= SlotOnPropertyChanged;
        }

        var allSlots = ImageSlotDefinitions.All
            .Select(def => _imageSlotViewModelFactory.Create(def))
            .ToList();

        PrimarySlot = allSlots.FirstOrDefault(x => x.Definition.Kind == ImageSlotKind.Primary);

        SecondarySlots.Clear();
        foreach (var secondary in allSlots.Where(x => x.Definition.Kind == ImageSlotKind.Secondary))
        {
            SecondarySlots.Add(secondary);
        }

        RecalculatePendingChanges();
    }

    private void SecondarySlotsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ImageSlotViewModel slot in e.OldItems)
            {
                slot.PropertyChanged -= SlotOnPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (ImageSlotViewModel slot in e.NewItems)
            {
                slot.PropertyChanged += SlotOnPropertyChanged;
            }
        }

        RecalculatePendingChanges();
    }

    private void SlotOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImageSlotViewModel.PendingAction)
            or nameof(ImageSlotViewModel.PendingSourceFilePath)
            or nameof(ImageSlotViewModel.HasValidationError))
        {
            RecalculatePendingChanges();
        }
    }

    private void RecalculatePendingChanges()
    {
        OnPropertyChanged(nameof(PendingChangesCount));
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(CanSaveChanges));
        SaveChangesCommand.NotifyCanExecuteChanged();
    }

    private bool CanDiscardUnsavedChanges()
    {
        if (!HasPendingChanges)
        {
            return true;
        }

        var result = MessageBox.Show(
            "You have unsaved changes. If you continue, those changes will be lost.\n\nDo you want to continue?",
            "Unsaved Changes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    private IEnumerable<ImageSlotViewModel> GetAllSlots()
    {
        if (PrimarySlot is not null)
        {
            yield return PrimarySlot;
        }

        foreach (var slot in SecondarySlots)
        {
            yield return slot;
        }
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

    private string? BuildPreviewImageSource(
        ProductContext product,
        TargetMode targetMode,
        ImageSlotDefinition definition,
        string? assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
        {
            return null;
        }

        var localPath = BuildLocalPreviewImagePath(assetName);
        if (!string.IsNullOrWhiteSpace(localPath))
        {
            return localPath;
        }

        return BuildFallbackPreviewUrl(product, targetMode, definition);
    }

    private string? BuildLocalPreviewImagePath(string assetName)
    {
        var root = Environment.ExpandEnvironmentVariables(_pathOptions.PreviewImageRoot);
        var fullPath = Path.Combine(root, $"{assetName}.jpg");

        return File.Exists(fullPath) ? fullPath : null;
    }

    private string? BuildFallbackPreviewUrl(
        ProductContext product,
        TargetMode targetMode,
        ImageSlotDefinition definition)
    {
        var keyStem = ResolveFallbackPreviewKeyStem(product, targetMode);
        if (string.IsNullOrWhiteSpace(keyStem))
        {
            return null;
        }

        var baseUrl = definition.Kind == ImageSlotKind.Primary
            ? _pathOptions.FallbackPreviewPrimaryBaseUrl
            : _pathOptions.FallbackPreviewAltBaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        var fileName = BuildFallbackPreviewFileName(definition, keyStem);

        return $"{baseUrl.TrimEnd('/')}/{fileName}";
    }

    private static string? ResolveFallbackPreviewKeyStem(ProductContext product, TargetMode targetMode)
    {
        if (targetMode == TargetMode.Group)
        {
            return string.IsNullOrWhiteSpace(product.BaseCode)
                ? null
                : product.BaseCode;
        }

        return string.IsNullOrWhiteSpace(product.ProductId)
            ? null
            : product.ProductId;
    }

    private static string BuildFallbackPreviewFileName(ImageSlotDefinition definition, string keyStem)
    {
        if (definition.Kind == ImageSlotKind.Primary)
        {
            return $"{keyStem}.jpg";
        }

        return $"{keyStem}_{definition.ReferenceType}.jpg";
    }
}