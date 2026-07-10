using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Dolly.ImageLoader.Infrastructure.Repositories;

public sealed class BadImageRepository : IBadImageRepository
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly ILogger<BadImageRepository> _logger;

    public BadImageRepository(SqlConnectionFactory connectionFactory, ILogger<BadImageRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IReadOnlySet<string>> GetBadReferenceTypesAsync(
        string productOrGroupId,
        string productName,
        bool isProductMode,
        CancellationToken cancellationToken)
    {
        var allowedReferenceTypes = isProductMode
            ? new[]
            {
                "Primary_Image",
                "Primary_Cut_Out",
                "Secondary_Image_01",
                "Secondary_Image_02",
                "Secondary_Image_03",
                "Secondary_Image_04",
                "Secondary_Image_05",
                "Secondary_Image_06",
                "Secondary_Image_07",
                "Secondary_Image_08",
                "Secondary_Image_09"
            }
            : new[]
            {
                "Primary_Image",
                "Primary_Cut_Out"
            };

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var sql = isProductMode
            ? BuildProductModeSql(allowedReferenceTypes.Length)
            : BuildGroupModeSql(allowedReferenceTypes.Length);

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);

        if (isProductMode)
        {
            command.Parameters.AddWithValue("@lookupValue", productName);
        }
        else
        {
            command.Parameters.AddWithValue("@lookupValue", productOrGroupId);
        }

        for (var i = 0; i < allowedReferenceTypes.Length; i++)
        {
            command.Parameters.AddWithValue($"@ref{i}", allowedReferenceTypes[i]);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var sqlReferenceType = reader["Reference_Type"]?.ToString();
            var mappedReferenceType = MapSqlReferenceTypeToUiReferenceType(sqlReferenceType);

            if (!string.IsNullOrWhiteSpace(mappedReferenceType))
            {
                result.Add(mappedReferenceType);
            }
        }

        _logger.LogInformation(
            "Loaded {Count} bad image references for lookup value {LookupValue} in {Mode} mode",
            result.Count,
            isProductMode ? productName : productOrGroupId,
            isProductMode ? "Product" : "Group");

        return result;
    }

    private static string BuildProductModeSql(int refCount)
    {
        var inClause = string.Join(", ", Enumerable.Range(0, refCount).Select(i => $"@ref{i}"));

        return $"""
                SELECT [Reference_Type]
                FROM [Catalogue].[BadImageNames]
                WHERE [Name] = @lookupValue
                  AND [Reference_Type] IN ({inClause});
                """;
    }

    private static string BuildGroupModeSql(int refCount)
    {
        var inClause = string.Join(", ", Enumerable.Range(0, refCount).Select(i => $"@ref{i}"));

        return $"""
                SELECT [Reference_Type]
                FROM [Catalogue].[BadImageNames]
                WHERE [Id] = @lookupValue
                  AND [Reference_Type] IN ({inClause});
                """;
    }

    private static string? MapSqlReferenceTypeToUiReferenceType(string? sqlReferenceType)
    {
        return sqlReferenceType switch
        {
            "Primary_Image" => "Primary_Image",
            "Primary_Cut_Out" => "Primary_Image",
            "Secondary_Image_01" => "Secondary_Image",
            "Secondary_Image_02" => "Secondary_Image_02",
            "Secondary_Image_03" => "Secondary_Image_03",
            "Secondary_Image_04" => "Secondary_Image_04",
            "Secondary_Image_05" => "Secondary_Image_05",
            "Secondary_Image_06" => "Secondary_Image_06",
            "Secondary_Image_07" => "Secondary_Image_07",
            "Secondary_Image_08" => "Secondary_Image_08",
            "Secondary_Image_09" => "Secondary_Image_09",
            _ => null
        };
    }
}