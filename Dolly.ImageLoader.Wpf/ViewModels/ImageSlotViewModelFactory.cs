using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Core.Models;

namespace Dolly.ImageLoader.Wpf.ViewModels;

public sealed class ImageSlotViewModelFactory : IImageSlotViewModelFactory
{
    private readonly IImageProcessingService _imageProcessingService;

    public ImageSlotViewModelFactory(IImageProcessingService imageProcessingService)
    {
        _imageProcessingService = imageProcessingService;
    }

    public ImageSlotViewModel Create(ImageSlotDefinition definition)
    {
        return new ImageSlotViewModel(definition, _imageProcessingService);
    }
}