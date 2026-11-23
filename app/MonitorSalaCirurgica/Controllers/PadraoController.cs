using Microsoft.AspNetCore.Mvc;
using MonitorSalaCirurgica.DAO;
using MonitorSalaCirurgica.Models;
using System;
using System.Linq;

namespace MonitorSalaCirurgica.Controllers
{
    /// <summary>
    /// CRUD genérico sobre o SQL Server do projeto:
    /// Index, Create, Save(model, Operacao), Edit, Delete, ValidaDados e controle de login.
    /// </summary>
    public class PadraoController<T> : BaseController where T : PadraoViewModel
    {
        protected PadraoDAO<T> DAO { get; set; }
        protected bool GeraProximoId { get; set; }
        protected string NomeViewIndex { get; set; } = "index";
        protected string NomeViewForm { get; set; } = "form";

        public virtual IActionResult Index()
        {
            try
            {
                var lista = DAO.Listagem();
                return View(NomeViewIndex, lista);
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
            if (GeraProximoId && Operacao == "I")
                model.Id = DAO.ProximoId();
        }

        public virtual IActionResult Save(T model, string Operacao)
        {
            try
            {
                ValidaDados(model, Operacao);
                if (ModelState.IsValid == false)
                {
                    ViewBag.Operacao = Operacao;
                    PreencheDadosParaView(Operacao, model);
                    return View(NomeViewForm, model);
                }
                else
                {
                    if (Operacao == "I")
                        DAO.Insert(model);
                    else
                        DAO.Update(model);
                    MensagemSucesso("Registro salvo com sucesso!");
                    return RedirectToAction(NomeViewIndex);
                }
            }
            catch (Exception erro)
            {
                return View("Error", new ErrorViewModel(MensagemDoErro(erro)));
            }
        }

        protected virtual void ValidaDados(T model, string operacao)
        {
            // guarda os erros de conversão (ex.: texto em campo numérico) antes de limpar o ModelState
            var camposComErro = ModelState.Where(e => e.Value.Errors.Count > 0).Select(e => e.Key).ToList();
            ModelState.Clear();
            foreach (var campo in camposComErro)
                ModelState.AddModelError(campo, "Valor inválido.");

            if (operacao == "I" && DAO.Consulta(model.Id) != null)
                ModelState.AddModelError("Id", "Código já está em uso!");
            if (operacao == "A" && DAO.Consulta(model.Id) == null)
                ModelState.AddModelError("Id", "Este registro não existe!");
            if (model.Id <= 0)
                ModelState.AddModelError("Id", "Id inválido!");
        }

        public IActionResult Edit(int id)
        {
            try
            {
                ViewBag.Operacao = "A";
                var model = DAO.Consulta(id);
                if (model == null)
                    return RedirectToAction(NomeViewIndex);
                else
                {
                    PreencheDadosParaView("A", model);
                    return View(NomeViewForm, model);
                }
            }
            catch (Exception erro)
            {
                return View("Error", new ErrorViewModel(MensagemDoErro(erro)));
            }
        }

        public IActionResult Delete(int id)
        {
            try
            {
                DAO.Delete(id);
                MensagemSucesso("Registro excluído com sucesso!");
                return RedirectToAction(NomeViewIndex);
            }
            catch (Exception erro)
            {
                return View("Error", new ErrorViewModel(MensagemDoErro(erro)));
            }
        }
    }
}
