using dw_api_web.Data;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Data.SqlClient;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;

// Clases para controlar la serialización JSON
public class EstadoEnvioTrendResponse
{
    [JsonPropertyName("EstadoEnvio")]
    public string EstadoEnvio { get; set; }

    [JsonPropertyName("TotalEnvios")]
    public int TotalEnvios { get; set; }

    [JsonPropertyName("Trend")]
    public string Trend { get; set; }
}

public class ClienteTrendResponse
{
    [JsonPropertyName("ClienteKey")]
    public int ClienteKey { get; set; }

    [JsonPropertyName("ClienteNombre")]
    public string ClienteNombre { get; set; }

    [JsonPropertyName("TotalPedidos")]
    public int TotalPedidos { get; set; }

    [JsonPropertyName("Trend")]
    public string Trend { get; set; }
}

namespace dw_api_web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly DatabaseConnectionManager _dbConnectionManager;

        public DashboardController(DatabaseConnectionManager dbConnectionManager)
        {
            _dbConnectionManager = dbConnectionManager;
        }

        // Método para ejecutar consultas SQL
        private async Task<DataTable> ExecuteQueryAsync(string query)
        {
            DataTable dataTable = new DataTable();

            using (var connection = _dbConnectionManager.GetDataWarehouseConnection())
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(query, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        dataTable.Load(reader);
                    }
                }
            }

            return dataTable;
        }

        [HttpGet("counters")]
        public async Task<IActionResult> CountPedidosByEstado(string tab = null)
        {
            var currentYear = DateTime.Now.Year;
            var currentMonth = DateTime.Now.Month;

            string query = @"
                SELECT dim_Pedido.Estado, COUNT(dim_Pedido.[Pedido Key]) AS cantidad
                FROM dim_Pedido
                WHERE dim_Pedido.Estado IN ('Entregado', 'En ruta', 'Pendiente', 'Cancelado')";

            if (tab == "yearly")
            {
                query += $@"
                    AND YEAR(dim_Pedido.[Fecha Pedido]) = {currentYear}
                    GROUP BY YEAR(dim_Pedido.[Fecha Pedido]), dim_Pedido.Estado";
            }
            else if (tab == "monthly")
            {
                query += $@"
                    AND MONTH(dim_Pedido.[Fecha Pedido]) = {currentMonth}
                    AND YEAR(dim_Pedido.[Fecha Pedido]) = {currentYear}
                    GROUP BY MONTH(dim_Pedido.[Fecha Pedido]), dim_Pedido.Estado";
            }
            else
            {
                query += " GROUP BY dim_Pedido.Estado";
            }

            var dataTable = await ExecuteQueryAsync(query);

            if (dataTable.Rows.Count == 0)
            {
                return NotFound("No se encontraron pedidos.");
            }

            // Convert DataTable to Dictionary
            var countDict = new Dictionary<string, int>();
            foreach (DataRow row in dataTable.Rows)
            {
                countDict[row["Estado"].ToString()] = Convert.ToInt32(row["cantidad"]);
            }

            return Ok(countDict);
        }

        [HttpGet("trends/states")]
        public async Task<IActionResult> StateTrends()
        {
            string recentMonthsQuery = @"
                SELECT DISTINCT TOP 2 YEAR(Fact_Envio.[Fecha Envio]) AS Year, MONTH(Fact_Envio.[Fecha Envio]) AS Month
                FROM Fact_Envio
                ORDER BY Year DESC, Month DESC;
            ";

            var recentMonths = await ExecuteQueryAsync(recentMonthsQuery);

            if (recentMonths.Rows.Count < 2)
            {
                return Ok(new { message = "Insufficient data" });
            }

            var lastMonth = (int)recentMonths.Rows[1]["Month"];
            var currentMonth = (int)recentMonths.Rows[0]["Month"];
            var currentYear = (int)recentMonths.Rows[0]["Year"];

            string queryForMonthData(int year, int month) => $@"
                SELECT dim_Ubicacion.[Estado Envio], COUNT(Fact_Envio.[Envio Key]) AS TotalEnvios
                FROM dim_Ubicacion
                JOIN Fact_Envio ON Fact_Envio.[Ubicacion Key] = dim_Ubicacion.[Ubicacion Key]
                WHERE YEAR(Fact_Envio.[Fecha Envio]) = {year} AND MONTH(Fact_Envio.[Fecha Envio]) = {month}
                GROUP BY dim_Ubicacion.[Estado Envio];
            ";

            var lastData = await ExecuteQueryAsync(queryForMonthData(currentYear, lastMonth));
            var currentData = await ExecuteQueryAsync(queryForMonthData(currentYear, currentMonth));

            var lastDataDict = new Dictionary<string, int>();
            foreach (DataRow row in lastData.Rows)
            {
                lastDataDict[row["Estado Envio"].ToString()] = Convert.ToInt32(row["TotalEnvios"]);
            }

            var response = new List<EstadoEnvioTrendResponse>();
            foreach (DataRow row in currentData.Rows)
            {
                var estado = row["Estado Envio"].ToString();
                var total = Convert.ToInt32(row["TotalEnvios"]);
                var previousTotal = lastDataDict.ContainsKey(estado) ? lastDataDict[estado] : 0;
                var trend = total > previousTotal ? "up" : total < previousTotal ? "down" : "no change";

                response.Add(new EstadoEnvioTrendResponse
                {
                    EstadoEnvio = estado,
                    TotalEnvios = total,
                    Trend = trend
                });
            }

            return Ok(response);
        }

        [HttpGet("trends/clients")]
        public async Task<IActionResult> ClientTrends()
        {
            // Obtener los dos meses más recientes
            string recentMonthsQuery = @"
                SELECT DISTINCT TOP 2 YEAR(Fact_Envio.[Fecha Envio]) AS Year, MONTH(Fact_Envio.[Fecha Envio]) AS Month
                FROM Fact_Envio
                ORDER BY Year DESC, Month DESC;
            ";

            var recentMonths = await ExecuteQueryAsync(recentMonthsQuery);

            if (recentMonths.Rows.Count < 2)
            {
                return Ok(new { message = "Insufficient data" });
            }

            var lastMonth = (int)recentMonths.Rows[1]["Month"];
            var currentMonth = (int)recentMonths.Rows[0]["Month"];
            var currentYear = (int)recentMonths.Rows[0]["Year"];

            // Método para obtener los datos por mes
            async Task<DataTable> GetMonthData(int year, int month)
            {
                string query = $@"
                    SELECT dim_Cliente.[Cliente Key], dim_Cliente.Nombre, COUNT(Fact_Envio.[Envio Key]) AS TotalPedidos
                    FROM dim_Cliente
                    JOIN Fact_Envio ON Fact_Envio.[Cliente Key] = dim_Cliente.[Cliente Key]
                    WHERE YEAR(Fact_Envio.[Fecha Envio]) = {year} AND MONTH(Fact_Envio.[Fecha Envio]) = {month}
                    GROUP BY dim_Cliente.[Cliente Key], dim_Cliente.Nombre;
                ";
                return await ExecuteQueryAsync(query);
            }

            // Obtener los datos de ambos meses
            var lastData = await GetMonthData(currentYear, lastMonth);
            var currentData = await GetMonthData(currentYear, currentMonth);

            // Crear diccionarios para los resultados de cada mes
            var lastDataDict = new Dictionary<int, int>();  // [Cliente Key] -> TotalPedidos
            foreach (DataRow row in lastData.Rows)
            {
                lastDataDict[(int)row["Cliente Key"]] = Convert.ToInt32(row["TotalPedidos"]);
            }

            // Crear la respuesta con las tendencias
            var response = new List<ClienteTrendResponse>();
            foreach (DataRow row in currentData.Rows)
            {
                var ClienteKey = (int)row["Cliente Key"];
                var nombre = row["Nombre"].ToString();
                var totalPedidos = Convert.ToInt32(row["TotalPedidos"]);
                var previousTotal = lastDataDict.ContainsKey(ClienteKey) ? lastDataDict[ClienteKey] : 0;

                // Calcular la tendencia
                var trend = totalPedidos > previousTotal ? "up" : totalPedidos < previousTotal ? "down" : "no change";

                response.Add(new ClienteTrendResponse
                {
                    ClienteKey = ClienteKey,
                    ClienteNombre = nombre,
                    TotalPedidos = totalPedidos,
                    Trend = trend
                });
            }

            return Ok(response);
        }
    }
}
