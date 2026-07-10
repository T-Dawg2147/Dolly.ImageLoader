using Dolly.ImageLoader.Core.Models;

namespace Dolly.ImageLoader.Wpf.ViewModels;

public interface IImageSlotViewModelFactory
{
    ImageSlotViewModel Create(ImageSlotDefinition definition);
}