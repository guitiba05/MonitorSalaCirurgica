using Microsoft.AspNetCore.Mvc;
using MonitorSalaCirurgica.DAO;
using MonitorSalaCirurgica.Infra;
using MonitorSalaCirurgica.Services;
using System;
using System.Text.RegularExpressions;

namespace MonitorSalaCirurgica.Controllers
{
    /// <summary>
    /// Tela Conexão: health check dos três componentes (requisições 0.1 a 0.3 da coleção)
    /// e troca do IP da EC2, que muda a cada sessão do Learner Lab.
    /// </summary>
    public class ConexaoController : BaseController
    {
        public IActionResult Index()
        {
            return View(HelperFiware.Config);
        }

        [HttpPost]
        public IActionResult SalvarHost(string host)
        {
            host = Regex.Replace((host ?? "").Trim(), "^https?://", "").TrimEnd('/');
            // IPv4 (54.210.1.2) ou nome DNS (ec2-54-210-1-2.compute-1.amazonaws.com)
            if (!Regex.IsMatch(host, @"^(\d{1,3}(\.\d{1,3}){3}|[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?)*)$"))
            {
                MensagemErro("Endereço inválido. Use só o IP público da EC2, por exemplo 54.210.10.20.");
                return RedirectToAction("Index");
            }
            try
            {
                ConfiguracaoArquivo.GravaHostFiware(host);
                var config = HelperFiware.Config;
                config.Host = host;
                HelperFiware.Configura(config);    // vale já, sem reiniciar a aplicação
                EstadoSupervisao.Acorda();
                MensagemSucesso($"IP do FIWARE alterado para {host} e gravado no appsettings.json.");
            }
            catch (Exception erro)
            {
                MensagemErro("Não foi possível gravar o appsettings.json: " + erro.Message);
            }
            return RedirectToAction("Index");
        }
    }
}
