using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Core.Models;
using Dolly.ImageLoader.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Dolly.ImageLoader.Infrastructure.Repositories;

public sealed class ImageImportMetadataRepository : IImageImportMetadataRepository
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly ILogger<ImageImportMetadataRepository> _logger;

    public ImageImportMetadataRepository(
        SqlConnectionFactory connectionFactory,
        ILogger<ImageImportMetadataRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<ImageImportMetadata?> GetByTargetIdAsync(string targetId, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT
                               [ImageName],
                               [LegacyPath],
                               [FolderPath],
                               [Hierarchy_Classification],
                               [Section_Identifier]
                           FROM [Catalogue].[fn_ImagePath](@targetId);
                           """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@targetId", targetId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            _logger.LogWarning("fn_ImagePath returned no rows for target {TargetId}", targetId);
            return null;
        }

        var result = new ImageImportMetadata
        {
            ImageName = reader["ImageName"].ToString() ?? string.Empty,
            LegacyPath = reader["LegacyPath"] == DBNull.Value ? null : reader["LegacyPath"].ToString(),
            FolderPath = reader["FolderPath"] == DBNull.Value ? null : reader["FolderPath"].ToString(),
            HierarchyClassification = reader["Hierarchy_Classification"].ToString() ?? string.Empty,
            SectionIdentifier = reader["Section_Identifier"].ToString() ?? string.Empty
        };

        _logger.LogInformation(
            "Loaded image import metadata for {TargetId}. ImageName={ImageName}, SectionIdentifier={SectionIdentifier}",
            targetId,
            result.ImageName,
            result.SectionIdentifier);

        return result;
    }
}