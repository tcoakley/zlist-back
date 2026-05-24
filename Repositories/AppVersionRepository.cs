using System.Data;
using Dapper;
using zListBack.Models;

namespace zListBack.Repositories
{
    public class AppVersionRepository
    {
        private readonly IDbConnection _connection;

        public AppVersionRepository(IDbConnection connection)
        {
            _connection = connection;
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
                return Result<List<AppVersion>>.Fail(ex.Message);
            }
        }
    }
}
