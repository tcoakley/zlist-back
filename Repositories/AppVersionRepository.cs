using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using zListBack.Models;

namespace zListBack.Repositories
{
    public class AppVersionRepository
    {
        private readonly IDbConnection _connection;
        private readonly ILogger<AppVersionRepository> _logger;

        public AppVersionRepository(IDbConnection connection, ILogger<AppVersionRepository> logger)
        {
            _connection = connection;
            _logger = logger;
        }

        public async Task<Result<List<AppVersion>>> GetAllVersions()
        {
            try
            {
                const string sql = @"
                    SELECT Id, Version, ReleasedAt, Notes
                    FROM AppVersions
                    ORDER BY ReleasedAt DESC, Id DESC;";

                var versions = await _connection.QueryAsync<AppVersion>(sql);
                return Result<List<AppVersion>>.Ok(versions.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllVersions failed.");
                return Result<List<AppVersion>>.Fail(ex.Message);
            }
        }
    }
}
