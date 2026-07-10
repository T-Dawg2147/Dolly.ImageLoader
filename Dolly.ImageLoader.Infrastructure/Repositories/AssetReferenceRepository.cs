using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Dolly.ImageLoader.Infrastructure.Repositories;

public sealed class AssetReferenceRepository : IAssetReferenceRepository
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly ILogger<AssetReferenceRepository> _logger;

    public AssetReferenceRepository(SqlConnectionFactory connectionFactory, ILogger<AssetReferenceRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IDictionary<string, string?>> GetAssetReferencesAsync(string targetId, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT
                               [Primary_Image],
                               [Primary_Cut_Out],
                               [Secondary_Image_01],
                               [Secondary_Image_02],
                               [Secondary_Image_03],
                               [Secondary_Image_04],
                               [Secondary_Image_05],
                               [Secondary_Image_06],
                               [Secondary_Image_07],
                               [Secondary_Image_08],
                               [Secondary_Image_09]
                           FROM [Catalogue].[AssetsByReference]
                           WHERE [Id] = @targetId;
                           """;

        var result = CreateEmptyReferenceDictionary();

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@targetId", targetId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            _logger.LogInformation("No asset reference row found for target {TargetId}", targetId);
            return result;
        }

        result["Primary_Image"] = DbValueToNullableString(reader["Primary_Image"]);
        result["Primary_Cut_Out"] = DbValueToNullableString(reader["Primary_Cut_Out"]);
        result["Secondary_Image"] = DbValueToNullableString(reader["Secondary_Image_01"]);
        result["Secondary_Image_02"] = DbValueToNullableString(reader["Secondary_Image_02"]);
        result["Secondary_Image_03"] = DbValueToNullableString(reader["Secondary_Image_03"]);
        result["Secondary_Image_04"] = DbValueToNullableString(reader["Secondary_Image_04"]);
        result["Secondary_Image_05"] = DbValueToNullableString(reader["Secondary_Image_05"]);
        result["Secondary_Image_06"] = DbValueToNullableString(reader["Secondary_Image_06"]);
        result["Secondary_Image_07"] = DbValueToNullableString(reader["Secondary_Image_07"]);
        result["Secondary_Image_08"] = DbValueToNullableString(reader["Secondary_Image_08"]);
        result["Secondary_Image_09"] = DbValueToNullableString(reader["Secondary_Image_09"]);

        _logger.LogInformation("Loaded asset references for target {TargetId}", targetId);

        return result;
    }

    private static Dictionary<string, string?> CreateEmptyReferenceDictionary()
    {
        return new Dictionary<string, string?>
        {
            ["Primary_Image"] = null,
            ["Primary_Cut_Out"] = null,
            ["Secondary_Image"] = null,
            ["Secondary_Image_02"] = null,
            ["Secondary_Image_03"] = null,
            ["Secondary_Image_04"] = null,
            ["Secondary_Image_05"] = null,
            ["Secondary_Image_06"] = null,
            ["Secondary_Image_07"] = null,
            ["Secondary_Image_08"] = null,
            ["Secondary_Image_09"] = null
        };
    }

    private static string? DbValueToNullableString(object value)
    {
        return value == DBNull.Value ? null : value.ToString();
    }
}