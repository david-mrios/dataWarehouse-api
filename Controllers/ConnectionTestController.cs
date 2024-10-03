using dw_api_web.Data;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;

namespace dw_api_web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConnectionTestController : ControllerBase
    {
        private readonly DatabaseConnectionManager _dbConnectionManager;
        private readonly ILogger<ConnectionTestController> _logger;

        public ConnectionTestController(DatabaseConnectionManager dbConnectionManager, ILogger<ConnectionTestController> logger)
        {
            _dbConnectionManager = dbConnectionManager;
            _logger = logger;
        }

        [HttpGet("test-eda-connection")]
        public IActionResult TestEDAConnection()
        {
            try
            {
                using (SqlConnection connection = _dbConnectionManager.GetDataWarehouseConnection())
                {
                    connection.Open();
                    return Ok("Conexión exitosa a la base de datos TecnoNic_DW (EDA)");
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Error al conectar a la base de datos TecnoNic_DW");
                return StatusCode(500, "Error al conectar a la base de datos TecnoNic_DW: " + ex.Message);
            }
        }

        [HttpGet("test-dashboard-connection")]
        public IActionResult TestDashboardConnection()
        {
            try
            {
                using (SqlConnection connection = _dbConnectionManager.GetCMIConnection())
                {
                    connection.Open();
                    return Ok("Conexión exitosa a la base de datos CMISentinelPrime (Dashboard)");
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Error al conectar a la base de datos CMISentinelPrime");
                return StatusCode(500, "Error al conectar a la base de datos CMISentinelPrime: " + ex.Message);
            }
        }
    }
}
