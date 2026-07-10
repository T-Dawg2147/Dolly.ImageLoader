using System.IO;
using System.Text;
using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Core.Models;
using Dolly.ImageLoader.Core.Options;
using Microsoft.Extensions.Options;

namespace Dolly.ImageLoader.Infrastructure.Services;

public sealed class MetadataService : IMetadataService
{
    private readonly PackagingOptions _packagingOptions;

    public MetadataService(IOptions<PackagingOptions> packagingOptions)
    {
        _packagingOptions = packagingOptions.Value;
    }

    public async Task<string> ExportMetadataCsvAsync(
        IReadOnlyList<MetadataRecord> records,
        string workingFolderPath,
        CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(workingFolderPath, _packagingOptions.MetadataFileName);

        var builder = new StringBuilder();
        builder.AppendLine("FileName,Product,Reference,Hierarchy_Classification");

        foreach (var record in records)
        {
            builder.AppendLine($"{record.FileName},{record.Product},{record.Reference},{record.HierarchyClassification}");
        }

        await File.WriteAllTextAsync(filePath, builder.ToString(), cancellationToken);

        return filePath;
    }
}