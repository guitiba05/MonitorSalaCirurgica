using MonitorSalaCirurgica.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace MonitorSalaCirurgica.DAO
{
    public class LimiteDAO : PadraoDAO<LimiteViewModel>
    {
        protected override SqlParameter[] CriaParametros(LimiteViewModel limite)
        {
            SqlParameter[] p = new SqlParameter[6];
            p[0] = new SqlParameter("id", limite.Id);
            p[1] = new SqlParameter("perfil", limite.Perfil.Trim());
            p[2] = new SqlParameter("grandeza", limite.Grandeza);
            p[3] = new SqlParameter("minimo", limite.Minimo ?? 0);
            p[4] = new SqlParameter("maximo", limite.Maximo ?? 0);
            p[5] = new SqlParameter("histerese", limite.Histerese ?? 0);
            return p;
        }

        protected override LimiteViewModel MontaModel(DataRow registro)
        {
            LimiteViewModel l = new LimiteViewModel();
            l.Id = Convert.ToInt32(registro["id"]);
            l.Perfil = registro["perfil"].ToString();
            l.Grandeza = registro["grandeza"].ToString();
            l.Minimo = Convert.ToDouble(registro["minimo"]);
            l.Maximo = Convert.ToDouble(registro["maximo"]);
            l.Histerese = Convert.ToDouble(registro["histerese"]);
            return l;
        }

        protected override void SetTabela()
        {
            Tabela = "Limite";
            NomeSpListagem = "spListagemLimites";
        }

        /// <summary>Limites de um perfil (uma linha por grandeza)</summary>
        public List<LimiteViewModel> ListagemPorPerfil(string perfil)
        {
            var p = new SqlParameter[] { new SqlParameter("perfil", perfil) };
            return MontaLista(HelperDAO.ExecutaProcSelect("spListagemLimitesPorPerfil", p));
        }

        /// <summary>Nomes dos perfis cadastrados</summary>
        public List<string> ListagemPerfis()
        {
            return HelperDAO.ExecutaProcSelect("spListagemPerfis", null).Rows.Cast<DataRow>()
                            .Select(r => r["perfil"].ToString()).ToList();
        }
    }
}
