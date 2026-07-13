using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Dolly.ImageLoader.Batch.ViewModels;

public partial class BatchImagePoolItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string filePath = string.Empty;

    [ObservableProperty]
    private string fileName = string.Empty;

    [ObservableProperty]
    private BitmapImage? thumbnail;

    public string TooltipText => FilePath;

    public static BatchImagePoolItemViewModel FromFile(string filePath)
    {
        return new BatchImagePoolItemViewModel
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            Thumbnail = LoadThumbnail(filePath)
        };
    }

    private static BitmapImage? LoadThumbnail(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 180;
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
