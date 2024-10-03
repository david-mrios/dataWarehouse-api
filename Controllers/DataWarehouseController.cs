using dw_api_web.Data;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using System.Data;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace dw_api_web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataWarehouseController : ControllerBase
    {
        private readonly DatabaseConnectionManager _dbConnectionManager;

        public DataWarehouseController(DatabaseConnectionManager dbConnectionManager)
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

        // Método para obtener datos
        private async Task<DataTable> GetDataAsync()
        {
            string query = @"
            SELECT 
                dim_Cliente.Nombre + ' ' + dim_Cliente.Apellido AS FullName,
                dim_Cliente.[Puntos Fidelidad] AS PuntosFidelidad,
                Fact_Envio.[Empresa Envio] AS EmpresaEnvio,
                dim_AreaEnvio.Area AS Area,
                Fact_Envio.[Metodo Envio] AS MetodoEnvio,
                dim_AreaEnvio.[Costo Envio] AS CostoEnvio,
                dim_Producto.Nombre AS Producto,
                dim_Pedido.[Precio Unitario] AS PrecioUnitario,
                dim_Pedido.Cantidad AS Cantidad,
                dim_Pedido.[Metodo Pago] AS MetodoPago,
                dim_Ofertas.Nombre AS Oferta,
                dim_Ofertas.Descuento AS Descuento,
    	        (dim_Pedido.Cantidad * dim_Pedido.[Precio Unitario] + dim_AreaEnvio.[Costo Envio]) * (1- dim_Ofertas.Descuento / 100) as CostoFinal

            FROM 
                Fact_Envio
            JOIN 
                dim_Pedido ON Fact_Envio.[Pedido Key] = dim_Pedido.[Pedido Key]
            JOIN 
                dim_Cliente ON Fact_Envio.[Cliente Key] = dim_Cliente.[Cliente Key]
            JOIN 
                dim_Ofertas ON Fact_Envio.[Oferta Key] = dim_Ofertas.[Oferta Key]
            JOIN 
                dim_AreaEnvio ON Fact_Envio.[Area Envio Key] = dim_AreaEnvio.[Area Envio Key]
            JOIN 
                dim_Producto ON Fact_Envio.[Producto Key] = dim_Producto.[Producto Key]
            JOIN 
                dim_Ubicacion ON Fact_Envio.[Ubicacion Key] = dim_Ubicacion.[Ubicacion Key];
        ";

            return await ExecuteQueryAsync(query);
        }

        // Endpoints de la API
        [HttpGet("df")]
        public async Task<ActionResult<IEnumerable<Dictionary<string, object>>>> ViewDf()
        {
            var data = await GetDataAsync();
            var result = new List<Dictionary<string, object>>();

            foreach (DataRow row in data.Rows)
            {
                var rowDict = new Dictionary<string, object>();
                foreach (DataColumn column in data.Columns)
                {
                    rowDict[column.ColumnName] = row[column];
                }
                result.Add(rowDict);
            }

            return Ok(result);
        }

        [HttpGet("histograma-costo-envio")]
        public async Task<ActionResult<List<decimal>>> GetHistogramaCostoEnvio()
        {
            var data = await GetDataAsync();
            var costoEnvio = new List<decimal>();

            foreach (DataRow row in data.Rows)
            {
                if (row["CostoEnvio"] != DBNull.Value)
                {
                    costoEnvio.Add(Convert.ToDecimal(row["CostoEnvio"]));
                }
            }

            return Ok(costoEnvio);
        }

        [HttpGet("box-plot")]
        public async Task<IActionResult> GetBoxPlot()
        {
            var data = await GetDataAsync();
            var columns = new[] { "CostoFinal", "Descuento", "Cantidad", "CostoEnvio", "PrecioUnitario" };
            var dataDict = new Dictionary<string, object>();

            foreach (var column in columns)
            {
                if (data.Columns.Contains(column))
                {
                    var values = new List<decimal>();
                    var indexedValues = new List<(int index, decimal value)>();

                    for (int i = 0; i < data.Rows.Count; i++)
                    {
                        var row = data.Rows[i];
                        if (row[column] != DBNull.Value)
                        {
                            var value = Convert.ToDecimal(row[column]);
                            values.Add(value);
                            indexedValues.Add((i, value));  // Guardamos el índice y el valor.
                        }
                    }

                    if (values.Count > 0)
                    {
                        var minVal = values.Min();
                        var q1 = Quantile(values, (double)0.25m);
                        var median = Quantile(values, (double)0.5m);
                        var q3 = Quantile(values, (double)0.75m);
                        var maxVal = values.Max();
                        var meanVal = values.Average();
                        var iqr = q3 - q1;

                        var lowerBound = q1 - 1.5m * iqr;
                        var upperBound = q3 + 1.5m * iqr;

                        // Encontramos los outliers con sus índices
                        var outliers = indexedValues
                            .Where(iv => iv.value < lowerBound || iv.value > upperBound)
                            .Select(iv => new[] { iv.index, iv.value }) // Creamos el array con índice y valor.
                            .ToList();

                        dataDict[column] = new
                        {
                            boxplot = new[] { minVal, q1, median, q3, maxVal },
                            mean = meanVal,
                            outliers = outliers // Guardamos índice y valor de cada outlier
                        };
                    }
                }
            }

            return Ok(dataDict);
        }


        [HttpGet("scatter-costo-final")]
        public async Task<ActionResult<List<List<decimal>>>> GetScatterCostoFinal()
        {
            var data = await GetDataAsync();
            //var costoFinal = new List<(int Index, decimal CostoFinal)>();
            var costoFinal = new List<List<decimal>>();


            for (int i = 0; i < data.Rows.Count; i++)
            {
                if (data.Rows[i]["CostoFinal"] != DBNull.Value)
                {
                    decimal costoValue = Convert.ToDecimal(data.Rows[i]["CostoFinal"]);
                    Console.WriteLine("Descuento: " + costoValue);
                    costoFinal.Add(new List<decimal> { i, costoValue });

                }
            }

            return Ok(costoFinal);
        }


        [HttpGet("head")]
        public async Task<ActionResult<IEnumerable<Dictionary<string, object>>>> GetHead()
        {
            var data = await GetDataAsync();
            var head = data.AsEnumerable().Take(11).CopyToDataTable();
            var result = new List<Dictionary<string, object>>();

            foreach (DataRow row in head.Rows)
            {
                var rowDict = new Dictionary<string, object>();
                foreach (DataColumn column in head.Columns)
                {
                    rowDict[column.ColumnName] = row[column];
                }
                result.Add(rowDict);
            }

            return Ok(result);
        }

        [HttpGet("tail")]
        public async Task<ActionResult<IEnumerable<Dictionary<string, object>>>> GetTail()
        {
            var data = await GetDataAsync();
            var tail = data.AsEnumerable().Skip(Math.Max(0, data.Rows.Count - 11)).CopyToDataTable();
            var result = new List<Dictionary<string, object>>();

            foreach (DataRow row in tail.Rows)
            {
                var rowDict = new Dictionary<string, object>();
                foreach (DataColumn column in tail.Columns)
                {
                    rowDict[column.ColumnName] = row[column];
                }
                result.Add(rowDict);
            }

            return Ok(result);
        }

         // Método para calcular cuantiles
        private decimal Quantile(List<decimal> values, double quantile)
        {
            values.Sort();
            int n = values.Count;
            double k = (n - 1) * quantile;
            int f = (int)Math.Floor(k);
            double c = k - f;

            if (f + 1 < n)
            {
                return values[f] + (decimal)c * (values[f + 1] - values[f]);
            }
            else
            {
                return values[f];
            }
        }
    }
}
