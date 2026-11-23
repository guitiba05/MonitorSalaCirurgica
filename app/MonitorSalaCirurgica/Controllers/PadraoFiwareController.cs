using Microsoft.AspNetCore.Mvc;
using MonitorSalaCirurgica.DAO;
using MonitorSalaCirurgica.Models;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace MonitorSalaCirurgica.Controllers
{
    /// <summary>
    /// O mesmo PadraoController, para os CRUDs gravados direto no FIWARE via API
    /// (item de 5,0 do enunciado). A chave é texto (device_id, id da inscrição).
    /// Erros do FIWARE (409, 404...) aparecem no próprio formulário.
    /// </summary>
    public class PadraoFiwareController<T> : BaseController where T : PadraoFiwareViewModel
    {
        protected PadraoFiwareDAO<T> DAO { get; set; }
        protected string NomeViewIndex { get; set; } = "index";
        protected string NomeViewForm { get; set; } = "form";

        /// <summary>false quando o FIWARE gera o id (ex.: inscrições do Orion)</summary>
        protected bool UsuarioInformaId { get; set; } = true;

        public virtual IActionResult Index()
        {
            try
            {
                return View(NomeViewIndex, DAO.Listagem());
            }
            catch (Exception erro)
            {
                return View("Error", new ErrorViewModel(MensagemDoErro(erro)));
            }
        }

        public virtual IActionResult Create()
        {
            try
            {
                ViewBag.Operacao = "I";
                T model = Activator.CreateInstance<T>();
                PreencheDadosParaView("I", model);
                return View(NomeViewForm, model);
            }
            catch (Exception erro)
            {
                return View("Error", new ErrorViewModel(MensagemDoErro(erro)));
            }
        }

        protected virtual void PreencheDadosParaView(string Operacao, T model)
        {
        }

        public virtual IActionResult Save(T model, string Operacao)
        {
            try
            {
                ValidaDados(model, Operacao);
                if (ModelState.IsValid)
                {
                    try
                    {
                        AntesDeSalvar(model, Operacao);
                        if (Operacao == "I")
                            model.Id = DAO.Insert(model);
                        else
                            DAO.Update(model);
                        DepoisDeSalvar(model, Operacao);
                        if (TempData["Mensagem"] == null)
                            MensagemSucesso("Registro salvo no FIWARE com sucesso!");
                        return RedirectToAction(NomeViewIndex);
                    }
                    catch (ErroFiwareException erro)
                    {
                        // ex.: 409 = device_id já cadastrado
                        ModelState.AddModelError(erro.Codigo == "DUPLICATE_DEVICE_ID" ? "Id" : "", erro.Message);
                    }
                }
                ViewBag.Operacao = Operacao;
                PreencheDadosParaView(Operacao, model);
                return View(NomeViewForm, model);
            }
            catch (Exception erro)
            {
                return View("Error", new ErrorViewModel(MensagemDoErro(erro)));
            }
        }

        /// <summary>Passos extras antes de gravar no FIWARE (ex.: garantir o grupo de serviço)</summary>
        protected virtual void AntesDeSalvar(T model, string operacao)
        {
        }

        /// <summary>Passos extras depois do FIWARE (ex.: gravar a configuração da sala no banco)</summary>
        protected virtual void DepoisDeSalvar(T model, string operacao)
        {
        }

        protected virtual void ValidaDados(T model, string operacao)
        {
            var camposComErro = ModelState.Where(e => e.Value.Errors.Count > 0).Select(e => e.Key).ToList();
            ModelState.Clear();
            foreach (var campo in camposComErro)
                ModelState.AddModelError(campo, "Valor inválido.");

            if (!UsuarioInformaId && operacao == "I")
                return;
            // no FIWARE o código vira parte da URL: só letras, números, - e _
            if (string.IsNullOrWhiteSpace(model.Id))
                ModelState.AddModelError("Id", "Informe o código.");
            else if (!Regex.IsMatch(model.Id, "^[A-Za-z0-9_-]{1,50}$"))
                ModelState.AddModelError("Id", "Use apenas letras, números, - e _ (sem espaços ou acentos).");
            else if (operacao == "I" && DAO.Consulta(model.Id) != null)
                ModelState.AddModelError("Id", "Código já está em uso!");
            else if (operacao == "A" && DAO.Consulta(model.Id) == null)
                ModelState.AddModelError("Id", "Este registro não existe!");
        }

        public virtual IActionResult Edit(string id)
        {
            try
            {
                ViewBag.Operacao = "A";
                var model = DAO.Consulta(id);
                if (model == null)
                    return RedirectToAction(NomeViewIndex);
                PreencheDadosParaView("A", model);
                return View(NomeViewForm, model);
            }
            catch (Exception erro)
            {
                return View("Error", new ErrorViewModel(MensagemDoErro(erro)));
            }
        }

        public virtual IActionResult Delete(string id)
        {
            try
            {
                DAO.Delete(id);
                MensagemSucesso("Registro excluído do FIWARE com sucesso!");
                return RedirectToAction(NomeViewIndex);
            }
            catch (Exception erro)
            {
                return View("Error", new ErrorViewModel(MensagemDoErro(erro)));
            }
        }
    }
}
