using MonitorSalaCirurgica.Models;
using System;
using System.Data;
using System.Data.SqlClient;

namespace MonitorSalaCirurgica.DAO
{
    public class ConfiguracaoSalaDAO : PadraoDAO<ConfiguracaoSalaViewModel>
    {
        protected override SqlParameter[] CriaParametros(ConfiguracaoSalaViewModel c)
        {
            SqlParameter[] p = new SqlParameter[6];
            p[0] = new SqlParameter("id", c.Id);
            p[1] = new SqlParameter("deviceId", c.DeviceId);
            p[2] = new SqlParameter("nome", c.Nome);
            p[3] = new SqlParameter("perfilAtivo", c.PerfilAtivo);
            p[4] = new SqlParameter("janelaSegundos", c.JanelaSegundos);
            p[5] = new SqlParameter("supervisaoAtiva", c.SupervisaoAtiva);
            return p;
        }

        protected override ConfiguracaoSalaViewModel MontaModel(DataRow registro)
        {
            ConfiguracaoSalaViewModel c = new ConfiguracaoSalaViewModel();
            c.Id = Convert.ToInt32(registro["id"]);
            c.DeviceId = registro["deviceId"].ToString();
            c.Nome = registro["nome"].ToString();
            c.PerfilAtivo = registro["perfilAtivo"].ToString();
            c.JanelaSegundos = Convert.ToInt32(registro["janelaSegundos"]);
            c.SupervisaoAtiva = Convert.ToBoolean(registro["supervisaoAtiva"]);
            return c;
        }

        protected override void SetTabela()
        {
            Tabela = "ConfiguracaoSala";
            ChaveIdentity = true;
        }

        public ConfiguracaoSalaViewModel ConsultaPorDevice(string deviceId)
        {
            var p = new SqlParameter[] { new SqlParameter("deviceId", deviceId) };
            var tabela = HelperDAO.ExecutaProcSelect("spConsultaConfiguracaoPorDevice", p);
            return tabela.Rows.Count == 0 ? null : MontaModel(tabela.Rows[0]);
        }

        public void DeletePorDevice(string deviceId)
        {
            HelperDAO.ExecutaProc("spDeleteConfiguracaoPorDevice", new SqlParameter[] { new SqlParameter("deviceId", deviceId) });
        }

        /// <summary>Insere se a sala ainda não tem configuração; senão, atualiza</summary>
        public void Salva(ConfiguracaoSalaViewModel model)
        {
            var atual = ConsultaPorDevice(model.DeviceId);
            if (atual == null)
                model.Id = Insert(model);
            else
            {
                model.Id = atual.Id;
                Update(model);
            }
        }
    }
}
