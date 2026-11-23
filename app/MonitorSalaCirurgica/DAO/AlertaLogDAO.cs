using MonitorSalaCirurgica.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace MonitorSalaCirurgica.DAO
{
    public class AlertaLogDAO : PadraoDAO<AlertaLogViewModel>
    {
        protected override SqlParameter[] CriaParametros(AlertaLogViewModel a)
        {
            SqlParameter[] p = new SqlParameter[7];
            p[0] = new SqlParameter("id", a.Id);
            p[1] = new SqlParameter("dataHora", a.DataHora);
            p[2] = new SqlParameter("deviceId", a.DeviceId);
            p[3] = new SqlParameter("alerta", a.Alerta);
            p[4] = new SqlParameter("origem", a.Origem);
            p[5] = new SqlParameter("motivo", HelperDAO.NullAsDbNull(Corta(a.Motivo, 300)));
            p[6] = new SqlParameter("status", a.Status);
            return p;
        }

        protected override AlertaLogViewModel MontaModel(DataRow registro)
        {
            AlertaLogViewModel a = new AlertaLogViewModel();
            a.Id = Convert.ToInt32(registro["id"]);
            a.DataHora = Convert.ToDateTime(registro["dataHora"]);
            a.DeviceId = registro["deviceId"].ToString();
            a.Alerta = registro["alerta"].ToString();
            a.Origem = registro["origem"].ToString();
            if (registro["motivo"] != DBNull.Value)
                a.Motivo = registro["motivo"].ToString();
            a.Status = registro["status"].ToString();
            return a;
        }

        protected override void SetTabela()
        {
            Tabela = "AlertaLog";
            ChaveIdentity = true;
        }

        /// <summary>O log não é editado pela tela: só o status muda (Enviado → Confirmado...)</summary>
        public void AtualizaStatus(int id, string status)
        {
            var p = new SqlParameter[] { new SqlParameter("id", id), new SqlParameter("status", status) };
            HelperDAO.ExecutaProc("spUpdateStatus_AlertaLog", p);
        }

        /// <summary>Histórico com filtros opcionais (null = todos)</summary>
        public List<AlertaLogViewModel> ConsultaAvancada(string deviceId, string origem, DateTime? dataInicial, DateTime? dataFinal)
        {
            var p = new SqlParameter[]
            {
                new SqlParameter("deviceId", HelperDAO.NullAsDbNull(string.IsNullOrEmpty(deviceId) ? null : deviceId)),
                new SqlParameter("origem", HelperDAO.NullAsDbNull(string.IsNullOrEmpty(origem) ? null : origem)),
                new SqlParameter("dataInicial", HelperDAO.NullAsDbNull(dataInicial)),
                new SqlParameter("dataFinal", HelperDAO.NullAsDbNull(dataFinal)),
            };
            return MontaLista(HelperDAO.ExecutaProcSelect("spConsultaAvancadaAlertas", p));
        }

        private static string Corta(string s, int max) => s == null || s.Length <= max ? s : s.Substring(0, max);
    }
}
