using Microsoft.AspNetCore.Mvc;
using MonitorSalaCirurgica.DAO;
using MonitorSalaCirurgica.Models;
using MonitorSalaCirurgica.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MonitorSalaCirurgica.Controllers
{
    /// <summary>Tela inicial: uma visão rápida de cada sala e da plataforma FIWARE</summary>
    public class HomeController : BaseController
    {
        public IActionResult Index()
        {
            var resumos = new List<ResumoSalaViewModel>();
            try
            {
                foreach (var sala in new SalaDAO().ListagemDoContrato())
                    resumos.Add(AnaliseSala.Analisa(sala, AnaliseSala.ObtemOuCriaConfiguracao(sala), false));
            }
            catch (Exception erro)
            {
                ViewBag.ErroFiware = MensagemDoErro(erro);
            }
            return View(resumos);
        }

        /// <summary>Chamado via AJAX pelo site.js: situação dos três componentes do FIWARE</summary>
        public IActionResult StatusServicos()
        {
            var componentes = HelperFiware.Config.Configurado ? new StatusFiwareDAO().Verifica() : new List<StatusComponenteViewModel>();
            return Json(new
            {
                host = HelperFiware.Config.HostLimpo,
                configurado = HelperFiware.Config.Configurado,
                componentes,
                supervisao = new { ultimoCiclo = EstadoSupervisao.UltimoCiclo, erro = EstadoSupervisao.ErroGeral }
            });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
