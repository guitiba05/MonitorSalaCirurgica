using Microsoft.AspNetCore.Mvc;
using MonitorSalaCirurgica.DAO;
using MonitorSalaCirurgica.Models;
using MonitorSalaCirurgica.Services;
using System;
using System.Linq;

namespace MonitorSalaCirurgica.Controllers
{
    /// <summary>
    /// Dashboard (critérios H3 e H4 · "Dois gráficos" do enunciado). A tela carrega vazia e o
    /// site.js busca por AJAX:
    ///   ObtemDadosDashboard a cada 5 s: valores atuais, médias, limites e alerta
    ///   ObtemHistorico a cada 15 s: séries do STH-Comet para o gráfico de linhas e o histograma
    /// </summary>
    public class DashboardController : BaseController
    {
        public IActionResult Index(string salaId)
        {
            try
            {
                PreparaComboSalas();
                var salas = (System.Collections.Generic.List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>)ViewBag.Salas;
                ViewBag.SalaId = salaId ?? salas.FirstOrDefault()?.Value;
            }
            catch (Exception erro)
            {
                ViewBag.ErroFiware = MensagemDoErro(erro);
                ViewBag.Salas = new System.Collections.Generic.List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
            }
            ViewBag.SilencioPadrao = ConfigSupervisao.Atual.SilencioPadraoMinutos;
            return View();
        }

        /// <summary>Estado atual da sala (JSON)</summary>
        public IActionResult ObtemDadosDashboard(string salaId)
        {
            try
            {
                var r = AnaliseSala.AnalisaPorDevice(salaId);
                if (r == null)
                    return Json(new { erro = true, msg = $"A sala {salaId} não está cadastrada no IoT Agent." });
                string mostrado = r.Estado?.Alerta ?? "off";
                return Json(new
                {
                    r.DeviceId, r.Nome, r.EntityId, r.Perfil, r.JanelaSegundos, r.SupervisaoAtiva,
                    estado = r.Estado,
                    grandezas = r.Grandezas,
                    r.AlertaCalculado, r.Motivo, r.UltimoEnviado, r.Confirmado, r.AlertaDesde, r.UltimaVerificacao,
                    intervencao = r.Intervencao,
                    alertaMostrado = mostrado,
                    padrao = Contrato.Padrao(mostrado),
                    aviso = r.Erro ?? EstadoSupervisao.ErroGeral
                });
            }
            catch (Exception erro)
            {
                return Json(new { erro = true, msg = MensagemDoErro(erro) });
            }
        }

        /// <summary>
        /// Histórico das três grandezas (STH-Comet). Até 30 min: medidas brutas (de 100 em 100);
        /// acima disso: média por minuto (aggrMethod=sum), para não trazer milhares de pontos.
        /// </summary>
        public IActionResult ObtemHistorico(string salaId, int minutos = 15)
        {
            try
            {
                minutos = Math.Max(1, Math.Min(minutos, 24 * 60));
                var sala = new SalaDAO().Consulta(salaId);
                if (sala == null)
                    return Json(new { erro = true, msg = "Sala não encontrada no IoT Agent." });
                var cfg = AnaliseSala.ObtemOuCriaConfiguracao(sala);
                var limites = new LimiteDAO().ListagemPorPerfil(cfg.PerfilAtivo);
                DateTime ate = DateTime.Now, de = ate.AddMinutes(-minutos);
                bool agregado = minutos > 30;
                var dao = new MensagemIoTDAO();

                var series = Contrato.GrandezasExibicao.Select(g =>
                {
                    var pontos = agregado
                        ? dao.ConsultaMediaPorMinuto(sala.NomeEntidade, g, de, ate)
                        : dao.ConsultaPeriodo(sala.NomeEntidade, g, de, ate)
                             .Select(m => new PontoGrafico { X = HelperFiware.ConverteParaMilissegundos(m.DataHora), Y = m.Valor }).ToList();
                    var limite = limites.FirstOrDefault(l => l.Grandeza == g.Chave);
                    return new
                    {
                        grandeza = g.Chave, nome = g.Nome, unidade = g.Unidade, alerta = g.Alerta,
                        minimo = limite?.Minimo, maximo = limite?.Maximo, pontos
                    };
                }).ToList();

                return Json(new
                {
                    de = HelperFiware.ConverteParaMilissegundos(de),
                    ate = HelperFiware.ConverteParaMilissegundos(ate),
                    agregado, perfil = cfg.PerfilAtivo, series
                });
            }
            catch (Exception erro)
            {
                return Json(new { erro = true, msg = MensagemDoErro(erro) });
            }
        }

        [HttpPost]
        public IActionResult Silenciar(string salaId, int minutos)
        {
            try
            {
                var sala = new SalaDAO().Consulta(salaId);
                if (sala == null)
                    return Json(new { erro = true, msg = "Sala não encontrada." });
                minutos = Alarme.Silencia(sala, minutos, "Dashboard");
                return Json(new { msg = $"Alarme silenciado por {minutos} min." });
            }
            catch (Exception erro)
            {
                return Json(new { erro = true, msg = MensagemDoErro(erro) });
            }
        }

        [HttpPost]
        public IActionResult Reativar(string salaId)
        {
            Alarme.Reativa(salaId);
            return Json(new { msg = "Supervisão automática reativada." });
        }
    }
}
