using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MonitorSalaCirurgica.DAO;
using MonitorSalaCirurgica.Models;
using System;
using System.Collections.Generic;

namespace MonitorSalaCirurgica.Controllers
{
    /// <summary>
    /// CRUD das inscrições do Orion (/v2/subscriptions) que levam as medidas ao STH-Comet,
    /// uma por grandeza, no formato da requisição 2.6 da coleção. Sem elas o histórico fica vazio.
    /// </summary>
    public class InscricaoController : PadraoFiwareController<InscricaoViewModel>
    {
        public InscricaoController()
        {
            DAO = new InscricaoDAO();
            UsuarioInformaId = false;     // o Orion gera o id da inscrição
        }

        protected override void PreencheDadosParaView(string Operacao, InscricaoViewModel model)
        {
            if (Operacao == "I")
            {
                PreparaComboSalas();
                if (string.IsNullOrEmpty(model.Url)) model.Url = HelperFiware.Config.UrlNotificacaoSth;
                if (string.IsNullOrEmpty(model.Atributo)) model.Atributo = "todas";
            }
            List<SelectListItem> grandezas = new List<SelectListItem>();
            if (Operacao == "I")
                grandezas.Add(new SelectListItem("As três (uma inscrição para cada)", "todas"));
            foreach (var g in Contrato.GrandezasExibicao)
                grandezas.Add(new SelectListItem($"{g.Nome} ({g.Atributo})", g.Atributo));
            ViewBag.Grandezas = grandezas;
        }

        protected override void ValidaDados(InscricaoViewModel model, string operacao)
        {
            base.ValidaDados(model, operacao);
            if (operacao == "I" && string.IsNullOrEmpty(model.SalaId))
                ModelState.AddModelError("SalaId", "Escolha a sala.");
            if (model.Atributo != "todas" && Contrato.GrandezaPorAtributo(model.Atributo) == null)
                ModelState.AddModelError("Atributo", "Grandeza inválida.");
            if (string.IsNullOrWhiteSpace(model.Url) || !Uri.IsWellFormedUriString(model.Url, UriKind.Absolute))
                ModelState.AddModelError("Url", "Informe uma URL válida (ex.: http://sth-comet:8666/notify).");
            if (model.Throttling < 0 || model.Throttling > 3600)
                ModelState.AddModelError("Throttling", "O intervalo mínimo vai de 0 a 3600 s.");
            if (model.Descricao != null && model.Descricao.Length > 200)
                ModelState.AddModelError("Descricao", "A descrição tem no máximo 200 caracteres.");
        }

        public override IActionResult Save(InscricaoViewModel model, string Operacao)
        {
            // "As três": cria as inscrições que faltam para a sala, sem duplicar
            if (Operacao == "I" && model.Atributo == "todas")
            {
                try
                {
                    ValidaDados(model, Operacao);
                    var sala = string.IsNullOrEmpty(model.SalaId) ? null : new SalaDAO().Consulta(model.SalaId);
                    if (ModelState.IsValid && sala != null)
                    {
                        int criadas = new InscricaoDAO().CriaPadraoDaSala(sala);
                        MensagemSucesso(criadas == 0 ? "A sala já tinha as três inscrições." : $"{criadas} inscrição(ões) criada(s) para {sala.NomeEntidade}.");
                        return RedirectToAction(NomeViewIndex);
                    }
                    if (sala == null) ModelState.AddModelError("SalaId", "Sala não encontrada no IoT Agent.");
                    ViewBag.Operacao = Operacao;
                    PreencheDadosParaView(Operacao, model);
                    return View(NomeViewForm, model);
                }
                catch (Exception erro)
                {
                    return View("Error", new ErrorViewModel(MensagemDoErro(erro)));
                }
            }
            return base.Save(model, Operacao);
        }

        protected override void AntesDeSalvar(InscricaoViewModel model, string operacao)
        {
            if (operacao == "I")
            {
                var sala = new SalaDAO().Consulta(model.SalaId);
                if (sala == null)
                    throw new Exception("Sala não encontrada no IoT Agent.");
                model.EntidadeId = sala.NomeEntidade;
                model.EntidadeTipo = Contrato.TipoEntidade;
            }
        }

        /// <summary>Atalho da listagem: cria as três inscrições de uma sala</summary>
        public IActionResult CriarPadrao(string salaId)
        {
            return Save(new InscricaoViewModel { SalaId = salaId, Atributo = "todas", Url = HelperFiware.Config.UrlNotificacaoSth }, "I");
        }

        public override IActionResult Index()
        {
            try { PreparaComboSalas(); }
            catch (Exception) { ViewBag.Salas = new List<SelectListItem>(); }
            return base.Index();
        }
    }
}
