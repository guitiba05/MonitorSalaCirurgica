using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using MonitorSalaCirurgica.DAO;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace MonitorSalaCirurgica.Controllers
{
    /// <summary>
    /// O que todos os controllers da aplicação têm em comum: exigir login (como o PadraoController
    /// do CadAluno), mensagens para a próxima tela e o combo de salas.
    /// </summary>
    public class BaseController : Controller
    {
        protected bool ExigeAutenticacao { get; set; } = true;

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (ExigeAutenticacao && !HelperControllers.VerificaUserLogado(HttpContext.Session))
                context.Result = RedirectToAction("Index", "Login");
            else
            {
                ViewBag.Logado = true;
                base.OnActionExecuting(context);
            }
        }

        protected void MensagemSucesso(string msg) => TempData["Mensagem"] = msg;
        protected void MensagemErro(string msg) => TempData["Erro"] = msg;
        protected void MensagemAviso(string msg) => TempData["Aviso"] = msg;

        /// <summary>Mensagem amigável para erro de banco (o mais comum: script.sql não executado)</summary>
        protected static string MensagemDoErro(Exception erro)
        {
            if (erro is SqlException)
                return "Banco de dados indisponível ou desatualizado: execute database/script.sql e confira " +
                       "ConnectionStrings:BancoSQL no appsettings.json. Detalhe: " + erro.Message;
            return erro.Message;
        }

        /// <summary>Combo de salas do IoT Agent (device_id - nome), como o combo de cidades do CadAluno</summary>
        protected void PreparaComboSalas(bool incluiTodas = false)
        {
            List<SelectListItem> lista = new List<SelectListItem>();
            if (incluiTodas)
                lista.Add(new SelectListItem("Todas", ""));
            Dictionary<string, string> nomes;
            try { nomes = new ConfiguracaoSalaDAO().Listagem().ToDictionary(c => c.DeviceId, c => c.Nome); }
            catch (Exception) { nomes = new Dictionary<string, string>(); }
            foreach (var sala in new SalaDAO().ListagemDoContrato())
            {
                string nome;
                string texto = nomes.TryGetValue(sala.Id, out nome) ? $"{nome} ({sala.Id})" : sala.Id;
                lista.Add(new SelectListItem(texto, sala.Id));
            }
            ViewBag.Salas = lista;
        }
    }
}
