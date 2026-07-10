using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Dolly.ImageLoader.Infrastructure.Services;

public sealed class AssetLinkService : IAssetLinkService
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly ILogger<AssetLinkService> _logger;

    public AssetLinkService(SqlConnectionFactory connectionFactory, ILogger<AssetLinkService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task LinkAsync(string id, string imageName, string referenceType, string username, CancellationToken cancellationToken)
    {
        await ExecuteAsync(id, imageName, referenceType, "No", username, cancellationToken);
    }

    public async Task UnlinkAsync(string id, string imageName, string referenceType, string username, CancellationToken cancellationToken)
    {
        await ExecuteAsync(id, imageName, referenceType, "Yes", username, cancellationToken);
    }

    private async Task ExecuteAsync(
        string id,
        string imageName,
        string referenceType,
        string remove,
        string username,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand("[Catalogue].[usp_AssetLink]", connection)
        {
            CommandType = System.Data.CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@ImageName", imageName);
        command.Parameters.AddWithValue("@ReferenceType", referenceType);
        command.Parameters.AddWithValue("@Remove", remove);
        command.Parameters.AddWithValue("@Username", username);

        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation(
            "Executed usp_AssetLink. Id={Id}, ImageName={ImageName}, ReferenceType={ReferenceType}, Remove={Remove}, Username={Username}",
            id, imageName, referenceType, remove, username);
    }
}