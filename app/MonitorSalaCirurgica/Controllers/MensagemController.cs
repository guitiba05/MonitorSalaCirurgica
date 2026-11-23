using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MonitorSalaCirurgica.DAO;
using MonitorSalaCirurgica.Models;
using System;
using System.Collections.Generic;

namespace MonitorSalaCirurgica.Controllers
{
    /// <summary>
    /// Tela de consulta das mensagens IoT (item de 2,0 do enunciado): consulta via API ao
    /// STH-Comet, chamada por AJAX, com filtros e paginação.
    /// </summary>
    public class MensagemController : BaseController
    {
        public IActionResult ExibeConsultaAvancada()
        {
            try
            {
                PreparaComboSalas();
                List<SelectListItem> grandezas = new List<SelectListItem>();
                foreach (var g in Contrato.GrandezasExibicao)
                    grandezas.Add(new SelectListItem(g.Nome, g.Chave));
                ViewBag.Grandezas = grandezas;
                return View("ConsultaAvancada");
            }
            catch (Exception erro)
            {
                return View("Error", new ErrorViewModel(MensagemDoErro(erro)));
            }
        }

        /// <summary>Chamado via AJAX: devolve a partial view com o grid ou um JSON de erro</summary>
        public IActionResult ObtemDadosConsultaAvancada(string sala, string grandeza, DateTime? dataInicial,
                                                        DateTime? dataFinal, int pagina = 1, int tamanho = 25, string ordem = "desc")
        {
            try
            {
                if (string.IsNullOrEmpty(sala))
                    throw new Exception("Selecione a sala.");
                var g = Contrato.GrandezaPorChave(grandeza);
                if (g == null)
                    throw new Exception("Selecione a grandeza.");
                if (dataInicial.HasValue && dataFinal.HasValue && dataInicial > dataFinal)
                    throw new Exception("A data inicial deve ser menor que a data final.");

                var modelSala = new SalaDAO().Consulta(sala);
                if (modelSala == null)
                    throw new Exception("Sala não encontrada no IoT Agent.");

                var resultado = new MensagemIoTDAO().ConsultaPagina(modelSala, g, dataInicial, dataFinal,
                                                                    pagina, tamanho, ordem != "asc");
                return PartialView("pvGridMensagens", resultado);
            }
            catch (Exception erro)
            {
                return Json(new { erro = true, msg = MensagemDoErro(erro) });
            }
        }
    }
}
