# Dolly Image Loader

A WPF-based image upload tool for managing STEP and Amazon product images. The solution contains two independent runnable applications backed by shared infrastructure.

---

## Solution Structure

```
Dolly.ImageLoader.sln
│
├── Dolly.ImageLoader.Core              # Shared models, interfaces, options, enums, constants
├── Dolly.ImageLoader.Infrastructure    # Shared services, repositories, data access (SQL, ImageSharp)
├── Dolly.ImageLoader.Wpf.Shared       # Shared WPF UI components (ImageSlotViewModel, ImageSlotCardView, converters)
│
├── Dolly.ImageLoader.Wpf              # 🖥️ Single Uploader App  (entry point: App.xaml)
└── Dolly.ImageLoader.Batch            # 📦 Batch Uploader App   (entry point: App.xaml)
```

### Project descriptions

| Project | Type | Purpose |
|---|---|---|
| `Dolly.ImageLoader.Core` | Class library (`net8.0`) | Domain models, interfaces, options, enums, slot constants |
| `Dolly.ImageLoader.Infrastructure` | Class library (`net8.0-windows`) | SQL repositories, image processing services, save workflow, DI registration |
| `Dolly.ImageLoader.Wpf.Shared` | WPF class library (`net8.0-windows`) | `ImageSlotViewModel`, `ImageSlotCardView`, value converters, `NaturalStringComparer` |
| `Dolly.ImageLoader.Wpf` | WPF executable (`net8.0-windows`) | **Single uploader** — search/select one product, edit its image slots, save |
| `Dolly.ImageLoader.Batch` | WPF executable (`net8.0-windows`) | **Batch uploader** — load a product group, build an image pool, assign images across multiple products in one session |

---

## Applications

### Single Uploader (`Dolly.ImageLoader.Wpf`)

The standard image management tool. Allows a user to:
- Search for a product by ID or name
- View and edit its primary and secondary image slots
- Save changes back to the database and hotfolder

**Entry point:** `Dolly.ImageLoader.Wpf/App.xaml`

### Batch Uploader (`Dolly.ImageLoader.Batch`)

A standalone application for working on multiple products in one session. Allows a user to:
- Load an entire product group by group ID
- Build an image pool by drag-dropping or browsing for files
- Assign pool images to slots across multiple products
- Save all pending changes in a single operation

**Entry point:** `Dolly.ImageLoader.Batch/App.xaml`

---

## How to Build

### Prerequisites
- .NET 8 SDK
- Windows (WPF requires the Windows target)

### Build all projects

```bash
dotnet build Dolly.ImageLoader.sln
```

### Build a specific app

```bash
# Single uploader
dotnet build Dolly.ImageLoader.Wpf/Dolly.ImageLoader.Wpf.csproj

# Batch uploader
dotnet build Dolly.ImageLoader.Batch/Dolly.ImageLoader.Batch.csproj
```

### Run a specific app

```bash
# Single uploader
dotnet run --project Dolly.ImageLoader.Wpf/Dolly.ImageLoader.Wpf.csproj

# Batch uploader
dotnet run --project Dolly.ImageLoader.Batch/Dolly.ImageLoader.Batch.csproj
```

Both apps read their configuration from `appsettings.json` in the same directory as the executable. Each project has its own copy.

---

## Configuration (`appsettings.json`)

Both apps share the same configuration shape:

```json
{
  "Database": {
    "ConnectionString": "<your SQL Server connection string>"
  },
  "Paths": {
    "PreviewImageRoot": "<UNC path to local preview images>",
    "HotfolderRoot": "<UNC path to the STEP hotfolder>",
    "WorkingRoot": "%LocalAppData%\\DollyImageLoader\\Sessions",
    "FallbackPreviewPrimaryBaseUrl": "<HTTP base URL for primary fallback thumbnails>",
    "FallbackPreviewAltBaseUrl": "<HTTP base URL for alt fallback thumbnails>",
    "AmazonImportRoot": "<UNC path to Amazon import folder>",
    "AmazonPrimaryLargeRoot": "...",
    "AmazonPrimaryMediumRoot": "...",
    "AmazonPrimarySmallRoot": "...",
    "AmazonSecondaryLargeRoot": "...",
    "AmazonSecondaryMediumRoot": "...",
    "AmazonSecondarySmallRoot": "..."
  },
  "Image": {
    "MaxSecondarySlots": 9,
    "AllowedExtensions": [ ".jpg", ".jpeg" ],
    "MinWidth": 300,
    "MinHeight": 300,
    "MaxWidth": 6000,
    "MaxHeight": 6000,
    "MinDpi": 72,
    "MaxFileSizeBytes": 10485760
  },
  "Packaging": {
    "EnableMetadataZip": true,
    "MetadataFileName": "Metadata.csv"
  }
}
```

---

## Shared Core (`Dolly.ImageLoader.Core` and `Dolly.ImageLoader.Wpf.Shared`)

Common logic lives in two shared projects so neither app duplicates code:

- **`Dolly.ImageLoader.Core`** — pure .NET library with no WPF dependency:
  - `Models/` — `ProductContext`, `ImageSlotState`, `SaveRequest`, `OperationResult`, etc.
  - `Interfaces/` — `IProductRepository`, `ISaveWorkflowService`, `IImageProcessingService`, etc.
  - `Options/` — `DatabaseOptions`, `PathOptions`, `ImageOptions`, `PackagingOptions`
  - `Enums/` — `TargetMode`, `PendingActionType`, `ImageSlotKind`
  - `Constants/` — `ImageSlotDefinitions`

- **`Dolly.ImageLoader.Wpf.Shared`** — WPF class library referenced by both UI apps:
  - `ViewModels/ImageSlotViewModel` — observable VM for a single image slot
  - `ViewModels/IImageSlotViewModelFactory` / `ImageSlotViewModelFactory` — DI-friendly factory
  - `Views/ImageSlotCardView` — reusable XAML card for displaying and editing a slot
  - `Converters/` — `BooleanToFontWeightConverter`, `InverseBooleanConverter`
  - `Comparers/NaturalStringComparer` — Windows shell natural sort order
