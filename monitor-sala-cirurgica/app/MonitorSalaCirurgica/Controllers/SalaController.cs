using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MonitorSalaCirurgica.DAO;
using MonitorSalaCirurgica.Models;
using MonitorSalaCirurgica.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MonitorSalaCirurgica.Controllers
{
    /// <summary>
    /// CRUD das salas direto no FIWARE (IoT Agent), junto com a configuração da sala no banco:
    ///   listar GET /iot/devices · cadastrar POST /iot/services (se faltar) + POST /iot/devices
    ///   alterar PUT /iot/devices/{id} · excluir DELETE /iot/devices/{id}
    ///   testar PATCH setAlert na entidade do Orion (botões luz, temp, umid e off)
    /// </summary>
    public class SalaController : PadraoFiwareController<SalaViewModel>
    {
        public SalaController()
        {
            DAO = new SalaDAO();
        }

        public override IActionResult Index()
        {
            try
            {
                var salas = DAO.Listagem();
                List<ConfiguracaoSalaViewModel> configs = new List<ConfiguracaoSalaViewModel>();
                try { configs = new ConfiguracaoSalaDAO().Listagem(); }
                catch (Exception erro) { ViewBag.ErroBanco = MensagemDoErro(erro); }

                var entidades = new EntidadeSalaDAO();
                foreach (var s in salas)
                {
                    var cfg = configs.FirstOrDefault(c => c.DeviceId == s.Id);
                    s.ExisteNoBanco = cfg != null;
                    if (cfg != null)
                    {
                        s.Nome = cfg.Nome; s.PerfilAtivo = cfg.PerfilAtivo;
                        s.JanelaSegundos = cfg.JanelaSegundos; s.SupervisaoAtiva = cfg.SupervisaoAtiva;
                    }
                    try { s.Estado = entidades.ConsultaEstado(s.NomeEntidade); }
                    catch (Exception) { s.Estado = null; }
                }
                // configurações no banco cujo dispositivo saiu do IoT Agent (ex.: excluído pelo Postman)
                salas.AddRange(configs.Where(c => salas.All(s => s.Id != c.DeviceId)).Select(c => new SalaViewModel
                {
                    Id = c.DeviceId, Nome = c.Nome, PerfilAtivo = c.PerfilAtivo, JanelaSegundos = c.JanelaSegundos,
                    SupervisaoAtiva = c.SupervisaoAtiva, ExisteNoIotAgent = false
                }));
                return View(NomeViewIndex, salas.OrderBy(s => s.Id).ToList());
            }
            catch (Exception erro)
            {
                return View("Error", new ErrorViewModel(MensagemDoErro(erro)));
            }
        }

        protected override void PreencheDadosParaView(string Operacao, SalaViewModel model)
        {
            if (Operacao == "I" && string.IsNullOrEmpty(model.Id))
            {
                // sugere o próximo device_id livre: sala01, sala02...
                var usados = DAO.Listagem().Select(s => s.Id).ToList();
                int n = 1;
                while (usados.Contains($"sala{n:00}")) n++;
                model.Id = $"sala{n:00}";
                model.NomeEntidade = $"{Contrato.PrefixoEntidade}{n:000}";
                model.Nome = $"Sala {n:00}";
                model.PerfilAtivo = ConfigSupervisao.Atual.PerfilPadrao;
                model.JanelaSegundos = ConfigSupervisao.Atual.JanelaPadraoSegundos;
            }
            PreparaComboPerfis();
            ViewBag.ServidorMqtt = HelperFiware.Config.HostLimpo;
            ViewBag.PortaMqtt = HelperFiware.Config.PortaMqtt;
        }

        private void PreparaComboPerfis()
        {
            List<SelectListItem> lista = new List<SelectListItem>();
            foreach (var perfil in new LimiteDAO().ListagemPerfis())
                lista.Add(new SelectListItem(perfil, perfil));
            ViewBag.Perfis = lista;
        }

        public override IActionResult Edit(string id)
        {
            try
            {
                ViewBag.Operacao = "A";
                var model = DAO.Consulta(id);
                if (model == null)
                    return RedirectToAction(NomeViewIndex);
                var cfg = new ConfiguracaoSalaDAO().ConsultaPorDevice(id);
                model.Nome = cfg?.Nome ?? model.Nome ?? id;
                model.PerfilAtivo = cfg?.PerfilAtivo ?? ConfigSupervisao.Atual.PerfilPadrao;
                model.JanelaSegundos = cfg?.JanelaSegundos ?? ConfigSupervisao.Atual.JanelaPadraoSegundos;
                model.SupervisaoAtiva = cfg?.SupervisaoAtiva ?? true;
                PreencheDadosParaView("A", model);
                return View(NomeViewForm, model);
            }
            catch (Exception erro)
            {
                return View("Error", new ErrorViewModel(MensagemDoErro(erro)));
            }
        }

        protected override void ValidaDados(SalaViewModel model, string operacao)
        {
            base.ValidaDados(model, operacao);
            if (operacao == "I")
            {
                if (string.IsNullOrWhiteSpace(model.NomeEntidade) ||
                    !Regex.IsMatch(model.NomeEntidade, "^urn:ngsi-ld:[A-Za-z0-9]+:[A-Za-z0-9_-]+$"))
                    ModelState.AddModelError("NomeEntidade", "Formato esperado: urn:ngsi-ld:SalaCirurgica:001");
                if (string.IsNullOrWhiteSpace(model.Apikey) || !Regex.IsMatch(model.Apikey, "^[A-Za-z0-9_-]{1,50}$"))
                    ModelState.AddModelError("Apikey", "Use apenas letras, números, - e _.");
            }
            if (string.IsNullOrWhiteSpace(model.Nome))
                ModelState.AddModelError("Nome", "Informe o nome da sala.");
            else if (model.Nome.Length > 80)
                ModelState.AddModelError("Nome", "O nome tem no máximo 80 caracteres.");
            if (model.JanelaSegundos < 10 || model.JanelaSegundos > 3600)
                ModelState.AddModelError("JanelaSegundos", "A janela vai de 10 a 3600 segundos.");
            if (!new LimiteDAO().ListagemPerfis().Contains(model.PerfilAtivo))
                ModelState.AddModelError("PerfilAtivo", "Perfil inexistente. Cadastre-o na tela Limites.");
        }

        protected override void AntesDeSalvar(SalaViewModel model, string operacao)
        {
            // requisição 2.1: grupo de serviço do apikey (se ainda não existir)
            if (operacao == "I")
                new SalaDAO().GaranteServiceGroup(model.Apikey);
        }

        protected override void DepoisDeSalvar(SalaViewModel model, string operacao)
        {
            new ConfiguracaoSalaDAO().Salva(new ConfiguracaoSalaViewModel
            {
                DeviceId = model.Id,
                Nome = model.Nome.Trim(),
                PerfilAtivo = model.PerfilAtivo,
                JanelaSegundos = model.JanelaSegundos,
                SupervisaoAtiva = model.SupervisaoAtiva
            });
            string msg = operacao == "I" ? $"{model.Nome} ({model.Id}) cadastrada no IoT Agent." : $"{model.Nome} atualizada.";
            if (operacao == "I" && model.CriarInscricoes)
            {
                int criadas = new InscricaoDAO().CriaPadraoDaSala(model);
                msg += $" {criadas} inscrição(ões) do STH-Comet criada(s).";
            }
            MensagemSucesso(msg);
            EstadoSupervisao.Acorda();   // aplica perfil e janela já no próximo ciclo
        }

        /// <summary>
        /// Exclui do IoT Agent e do banco. Com ?excluirEntidade=true&amp;entidade=..., também apaga a
        /// entidade do Orion e as inscrições (pasta 6 da coleção, "Cleanup").
        /// </summary>
        public override IActionResult Delete(string id)
        {
            try
            {
                bool excluirEntidade = Request.Query["excluirEntidade"] == "true";
                string entidade = Request.Query["entidade"];
                var partes = new List<string>();
                DAO.Delete(id);
                partes.Add("dispositivo do IoT Agent");
                if (excluirEntidade && !string.IsNullOrEmpty(entidade))
                {
                    int n = new InscricaoDAO().RemoveDaEntidade(entidade);
                    new EntidadeSalaDAO().Delete(entidade);
                    partes.Add($"entidade do Orion e {n} inscrição(ões)");
                }
                new ConfiguracaoSalaDAO().DeletePorDevice(id);
                partes.Add("configuração do banco");
                EstadoSupervisao.Remove(id);
                MensagemSucesso($"Sala {id} excluída: {string.Join(", ", partes)}.");
                return RedirectToAction(NomeViewIndex);
            }
            catch (Exception erro)
            {
                return View("Error", new ErrorViewModel(MensagemDoErro(erro)));
            }
        }

        /// <summary>Botões de teste: liga o padrão do alerta por alguns segundos (PATCH setAlert)</summary>
        public IActionResult Testar(string id, string alerta)
        {
            try
            {
                if (!Contrato.AlertaValido(alerta))
                    throw new Exception("Alerta inválido.");
                var sala = DAO.Consulta(id);
                if (sala == null)
                    throw new Exception($"A sala {id} não existe no IoT Agent.");
                Alarme.Testa(sala, alerta, OrigemAlerta.Teste);
                var p = Contrato.Padrao(alerta);
                MensagemSucesso(alerta == "off"
                    ? $"Comando setAlert|off enviado para {id}."
                    : $"Comando setAlert|{alerta} enviado para {id}: LED {p.Led}, buzzer {p.BuzzerHz} Hz. A supervisão automática volta em {ConfigSupervisao.Atual.TesteSegundos} s.");
            }
            catch (Exception erro)
            {
                MensagemErro(MensagemDoErro(erro));
            }
            return RedirectToAction(NomeViewIndex);
        }
    }
}
