using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MonitorSalaCirurgica.DAO;
using MonitorSalaCirurgica.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MonitorSalaCirurgica.Controllers
{
    /// <summary>CRUD da tabela Limite (SQL Server): faixa de cada grandeza por perfil</summary>
    public class LimiteController : PadraoController<LimiteViewModel>
    {
        public LimiteController()
        {
            DAO = new LimiteDAO();
            GeraProximoId = true;
        }

        public override IActionResult Index()
        {
            try
            {
                // quais salas usam cada perfil (para avisar antes de mexer)
                ViewBag.Uso = new ConfiguracaoSalaDAO().Listagem().GroupBy(c => c.PerfilAtivo)
                                .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(c => c.Nome)));
            }
            catch (Exception)
            {
                ViewBag.Uso = new Dictionary<string, string>();
            }
            return base.Index();
        }

        protected override void ValidaDados(LimiteViewModel model, string operacao)
        {
            base.ValidaDados(model, operacao);

            if (string.IsNullOrWhiteSpace(model.Perfil))
                ModelState.AddModelError("Perfil", "Informe o perfil.");
            else if (model.Perfil.Length > 50)
                ModelState.AddModelError("Perfil", "O perfil tem no máximo 50 caracteres.");
            if (Contrato.GrandezaPorChave(model.Grandeza) == null)
                ModelState.AddModelError("Grandeza", "Escolha a grandeza.");
            if (model.Minimo == null)
                ModelState.AddModelError("Minimo", "Informe o mínimo.");
            if (model.Maximo == null)
                ModelState.AddModelError("Maximo", "Informe o máximo.");
            if (model.Histerese == null || model.Histerese < 0)
                ModelState.AddModelError("Histerese", "Informe a histerese (0 para nenhuma).");

            if (model.Minimo.HasValue && model.Maximo.HasValue)
            {
                if (model.Minimo >= model.Maximo)
                    ModelState.AddModelError("Maximo", "O máximo precisa ser maior que o mínimo.");
                else if (model.Histerese.HasValue && model.Histerese * 2 >= model.Maximo - model.Minimo)
                    ModelState.AddModelError("Histerese", "A histerese precisa ser menor que metade da faixa.");
                if ((model.Grandeza == "umidade" || model.Grandeza == "luminosidade") && (model.Minimo < 0 || model.Maximo > 100))
                    ModelState.AddModelError("Minimo", "Umidade e luminosidade vão de 0 a 100 %.");
                if (model.Grandeza == "temperatura" && (model.Minimo < -40 || model.Maximo > 80))
                    ModelState.AddModelError("Minimo", "O DHT11 mede de 0 a 50 °C: use uma faixa entre -40 e 80.");
            }

            // um perfil tem só uma linha por grandeza (UNIQUE no banco)
            if (!string.IsNullOrWhiteSpace(model.Perfil) &&
                ((LimiteDAO)DAO).ListagemPorPerfil(model.Perfil.Trim()).Any(l => l.Grandeza == model.Grandeza && l.Id != model.Id))
                ModelState.AddModelError("Grandeza", $"O perfil \"{model.Perfil}\" já tem limite de {model.Grandeza}. Altere a linha existente.");
        }

        protected override void PreencheDadosParaView(string Operacao, LimiteViewModel model)
        {
            base.PreencheDadosParaView(Operacao, model);
            if (Operacao == "I" && model.Histerese == null)
                model.Histerese = 0;
            ViewBag.Perfis = ((LimiteDAO)DAO).ListagemPerfis();
            List<SelectListItem> grandezas = new List<SelectListItem> { new SelectListItem("Selecione...", "") };
            foreach (var g in Contrato.GrandezasExibicao)
                grandezas.Add(new SelectListItem($"{g.Nome} ({g.Unidade})", g.Chave));
            ViewBag.Grandezas = grandezas;
        }
    }
}
