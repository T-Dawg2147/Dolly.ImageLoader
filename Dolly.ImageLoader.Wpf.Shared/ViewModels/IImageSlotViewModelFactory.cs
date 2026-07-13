using Dolly.ImageLoader.Core.Models;

namespace Dolly.ImageLoader.Wpf.Shared.ViewModels;

public interface IImageSlotViewModelFactory
{
    ImageSlotViewModel Create(ImageSlotDefinition definition);
}
