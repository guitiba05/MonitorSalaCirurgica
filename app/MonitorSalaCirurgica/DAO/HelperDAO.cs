using System;
using System.Data;
using System.Data.SqlClient;

namespace MonitorSalaCirurgica.DAO
{
    /// <summary>Execução das stored procedures (centraliza a execução das stored procedures)</summary>
    static class HelperDAO
    {
        public static object NullAsDbNull(object valor)
        {
            if (valor == null)
                return DBNull.Value;
            else
                return valor;
        }

        /// <param name="consultaUltimoIdentity">true = devolve o Id gerado pelo IDENTITY</param>
        public static int ExecutaProc(string nomeProc, SqlParameter[] parametros, bool consultaUltimoIdentity = false)
        {
            using (SqlConnection conexao = ConexaoBD.GetConexao())
            {
                using (SqlCommand comando = new SqlCommand(nomeProc, conexao))
                {
                    comando.CommandType = CommandType.StoredProcedure;
                    if (parametros != null)
                        comando.Parameters.AddRange(parametros);
                    comando.ExecuteNonQuery();
                    if (consultaUltimoIdentity)
                    {
                        comando.Parameters.Clear();
                        comando.CommandType = CommandType.Text;
                        comando.CommandText = "select isnull(@@IDENTITY,0)";
                        return Convert.ToInt32(comando.ExecuteScalar());
                    }
                    else
                        return 0;
                }
            }
        }

        public static DataTable ExecutaProcSelect(string nomeProc, SqlParameter[] parametros)
        {
            using (SqlConnection conexao = ConexaoBD.GetConexao())
            {
                using (SqlDataAdapter adapter = new SqlDataAdapter(nomeProc, conexao))
                {
                    if (parametros != null)
                        adapter.SelectCommand.Parameters.AddRange(parametros);
                    adapter.SelectCommand.CommandType = CommandType.StoredProcedure;
                    DataTable tabela = new DataTable();
                    adapter.Fill(tabela);
                    return tabela;
                }
            }
        }
    }
}
