using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Core.Options;
using Dolly.ImageLoader.Infrastructure.Data;
using Dolly.ImageLoader.Infrastructure.Repositories;
using Dolly.ImageLoader.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dolly.ImageLoader.Infrastructure.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection("Database"));
        services.Configure<PathOptions>(configuration.GetSection("Paths"));
        services.Configure<ImageOptions>(configuration.GetSection("Image"));
        services.Configure<PackagingOptions>(configuration.GetSection("Packaging"));

        services.AddSingleton<SqlConnectionFactory>();

        services.AddSingleton<IProductRepository, ProductRepository>();
        services.AddSingleton<IAssetReferenceRepository, AssetReferenceRepository>();
        services.AddSingleton<IBadImageRepository, BadImageRepository>();
        services.AddSingleton<IImageImportMetadataRepository, ImageImportMetadataRepository>();

        services.AddSingleton<IAssetNamingService, AssetNamingService>();
        services.AddSingleton<IImageFileService, LocalImageFileService>();
        services.AddSingleton<IImageProcessingService, ImageSharpImageProcessingService>();
        services.AddSingleton<IAssetLinkService, AssetLinkService>();
        services.AddSingleton<IMetadataService, MetadataService>();
        services.AddSingleton<IPackageService, ZipPackageService>();
        services.AddSingleton<IAmazonImageService, AmazonImageService>();
        services.AddSingleton<ISaveWorkflowService, SaveWorkflowService>();

        return services;
    }
}