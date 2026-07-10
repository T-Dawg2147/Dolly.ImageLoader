using Dolly.ImageLoader.Core.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Dolly.ImageLoader.Infrastructure.Data;

public sealed class SqlConnectionFactory
{
    private readonly string _connString;

    public SqlConnectionFactory(IOptions<DatabaseOptions> options)
    {
        _connString = options.Value.ConnectionString;
    }

    public SqlConnection Create()
        => new(_connString);
}