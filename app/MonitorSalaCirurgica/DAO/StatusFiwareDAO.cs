using MonitorSalaCirurgica.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MonitorSalaCirurgica.DAO
{
    /// <summary>Versão e tempo de resposta do IoT Agent, do Orion e do STH-Comet</summary>
    public class StatusFiwareDAO
    {
        public List<StatusComponenteViewModel> Verifica()
        {
            return new List<StatusComponenteViewModel>
            {
                VerificaComponente("IoT Agent", HelperFiware.UrlIotAgent + "/iot/about"),
                VerificaComponente("Orion", HelperFiware.UrlOrion + "/version"),
                VerificaComponente("STH-Comet", HelperFiware.UrlSth + "/version"),
            };
        }

        private StatusComponenteViewModel VerificaComponente(string nome, string url)
        {
            var status = new StatusComponenteViewModel { Nome = nome, Url = url };
            var relogio = Stopwatch.StartNew();
            try
            {
                status.Versao = HelperFiware.ConsultaVersao(url);
                status.Ok = true;
            }
            catch (Exception erro)
            {
                status.Erro = erro.Message;
            }
            status.Milissegundos = relogio.ElapsedMilliseconds;
            return status;
        }
    }
}
