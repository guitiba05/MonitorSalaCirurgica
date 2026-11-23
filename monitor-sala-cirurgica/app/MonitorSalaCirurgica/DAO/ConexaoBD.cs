using System.Data.SqlClient;

namespace MonitorSalaCirurgica.DAO
{
    public static class ConexaoBD
    {
        /// <summary>Connection string lida do appsettings.json (ConnectionStrings:BancoSQL) no Startup</summary>
        public static string StringConexao { get; set; }

        /// <summary>
        /// Método Estático que retorna um conexao aberta com o BD
        /// </summary>
        /// <returns>Conexão aberta</returns>
        public static SqlConnection GetConexao()
        {
            if (string.IsNullOrWhiteSpace(StringConexao))
                throw new System.Exception("Connection string 'BancoSQL' não configurada no appsettings.json.");
            SqlConnection conexao = new SqlConnection(StringConexao);
            conexao.Open();
            return conexao;
        }
    }
}
