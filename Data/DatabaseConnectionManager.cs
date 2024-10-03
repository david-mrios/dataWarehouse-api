using System.Data.SqlClient;

namespace dw_api_web.Data
{
    public class DatabaseConnectionManager
    {
        private readonly string _dataWarehouseConnectionString;
        private readonly string _cmiConnectionString;

        public DatabaseConnectionManager(IConfiguration configuration)
        {
            // Lee las cadenas de conexión desde el archivo appsettings.json
            _dataWarehouseConnectionString = configuration.GetConnectionString("DataWarehouseConnection");
            _cmiConnectionString = configuration.GetConnectionString("CMIConnection");
        }

        // Método para obtener la conexión a la base de datos TecnoNic_DW
        public SqlConnection GetDataWarehouseConnection()
        {
            return new SqlConnection(_dataWarehouseConnectionString);
        }

        // Método para obtener la conexión a la base de datos CMISentinelPrime
        public SqlConnection GetCMIConnection()
        {
            return new SqlConnection(_cmiConnectionString);
        }
    }
}
