using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Core.Models;
using Dolly.ImageLoader.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Dolly.ImageLoader.Infrastructure.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly ILogger<ProductRepository> _logger;

    public ProductRepository(
        SqlConnectionFactory connectionFactory,
        ILogger<ProductRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<ProductContext?> GetByIdAsync(string productId, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT TOP 1
                               P.[Id],
                               P.[Name],
                               P.[Parent_ID],
                               P.[Base_Code],
                               P.[Supplier_Product_Code]
                           FROM [Catalogue].[Products] AS P
                           WHERE P.[Id] = @id;
                           """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", productId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            _logger.LogInformation("No product found for id {ProductId}", productId);
            return null;
        }

        var result = MapProduct(reader);
        _logger.LogInformation("Loaded product {ProductId}", productId);
        return result;
    }

    public async Task<IReadOnlyList<ProductContext>> SearchProductsAsync(string searchText, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT TOP 25
                               P.[Id],
                               P.[Name],
                               P.[Parent_ID],
                               P.[Base_Code],
                               P.[Supplier_Product_Code]
                           FROM [Catalogue].[Products] AS P
                           WHERE P.[Id] LIKE @search
                              OR P.[Name] LIKE @search
                              OR P.[ParentId] LIKE @search
                              OR P.[Base_Code] LIKE @search
                              OR P.[Supplier_Product_Code] LIKE @search
                           ORDER BY
                               CASE WHEN P.[Id] = @exact OR P.[Parent_ID] = @exact THEN 0 ELSE 1 END,
                               P.[Id];
                           """;

        var results = new List<ProductContext>();

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@search", $"%{searchText}%");
        command.Parameters.AddWithValue("@exact", searchText);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapProduct(reader));
        }

        _logger.LogInformation("SearchProductsAsync returned {Count} results for {SearchText}", results.Count, searchText);
        return results;
    }

    public async Task<IReadOnlyList<ProductContext>> GetByParentGroupIdAsync(string parentGroupId, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT
                               P.[Id],
                               P.[Name],
                               P.[Parent_ID],
                               P.[Base_Code],
                               P.[Supplier_Product_Code]
                           FROM [Catalogue].[Products] AS P
                           WHERE P.[Parent_ID] = @parentGroupId
                           ORDER BY P.[Id];
                           """;

        var results = new List<ProductContext>();

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@parentGroupId", parentGroupId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapProduct(reader));
        }

        _logger.LogInformation(
            "GetByParentGroupIdAsync returned {Count} products for parent group {ParentGroupId}",
            results.Count,
            parentGroupId);

        return results;
    }

    private static ProductContext MapProduct(SqlDataReader reader)
    {
        return new ProductContext
        {
            ProductId = reader["Id"].ToString() ?? string.Empty,
            ProductName = reader["Name"].ToString() ?? string.Empty,
            ParentGroupId = reader["Parent_ID"] == DBNull.Value ? string.Empty : reader["Parent_ID"].ToString() ?? string.Empty,
            BaseCode = reader["Base_Code"] == DBNull.Value ? string.Empty : reader["Base_Code"].ToString() ?? string.Empty,
            SupplierProductCode = reader["Supplier_Product_Code"] == DBNull.Value ? string.Empty : reader["Supplier_Product_Code"].ToString() ?? string.Empty
        };
    }
}