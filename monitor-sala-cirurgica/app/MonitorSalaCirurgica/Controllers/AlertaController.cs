using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MonitorSalaCirurgica.DAO;
using MonitorSalaCirurgica.Models;
using System;
using System.Collections.Generic;

namespace MonitorSalaCirurgica.Controllers
{
    /// <summary>Histórico de alertas: cada setAlert enviado (tabela AlertaLog), com filtros</summary>
    public class AlertaController : BaseController
    {
        public IActionResult Index(string sala, string origem, DateTime? dataInicial, DateTime? dataFinal)
        {
            try
            {
                List<SelectListItem> salas = new List<SelectListItem> { new SelectListItem("Todas", "") };
                foreach (var c in new ConfiguracaoSalaDAO().Listagem())
                    salas.Add(new SelectListItem($"{c.Nome} ({c.DeviceId})", c.DeviceId, c.DeviceId == sala));
                ViewBag.Salas = salas;

                List<SelectListItem> origens = new List<SelectListItem> { new SelectListItem("Todas", "") };
                foreach (var o in OrigemAlerta.Todas)
                    origens.Add(new SelectListItem(o, o, o == origem));
                ViewBag.Origens = origens;
                ViewBag.DataInicial = dataInicial;
                ViewBag.DataFinal = dataFinal;

                return View(new AlertaLogDAO().ConsultaAvancada(sala, origem, dataInicial, dataFinal));
            }
            catch (Exception erro)
            {
                return View("Error", new ErrorViewModel(MensagemDoErro(erro)));
            }
        }
    }
}
