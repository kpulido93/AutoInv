using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

using Npgsql;

namespace Webhook.Controllers
{
    [ApiController]
    [Route("")]
    public class ClientsController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IMemoryCache _cache;

        public ClientsController(IConfiguration config, IMemoryCache cache)
        {
            _config = config;
            _cache = cache;
        }

        [HttpGet("id-clients")]
        public async Task<IActionResult> GetClients()
        {
            const string cacheKey = "clients_id_list_v1";

            if (_cache.TryGetValue(cacheKey, out List<object> cached) && cached != null)
                return Ok(cached);

            var clients = new List<object>();
            string connString = _config.GetConnectionString("Postgres");

            try
            {
                await using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                string sql = "SELECT org_id, org_name FROM accountdefinition " +
                             "WHERE org_name NOT LIKE 'ZZ\\_%' ESCAPE '\\' " +
                             "ORDER BY org_name";

                await using var cmd = new NpgsqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    clients.Add(new
                    {
                        id = reader["org_id"].ToString(),
                        name = reader["org_name"].ToString()
                    });
                }

                _cache.Set(cacheKey, clients, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1)
                });

                return Ok(clients);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
