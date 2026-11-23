using Microsoft.AspNetCore.Mvc;
using MonitorSalaCirurgica.Models;
using MonitorSalaCirurgica.Services;
using MonitorSalaCirurgica.Services.Alexa;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace MonitorSalaCirurgica.Controllers
{
    /// <summary>
    /// Tela Alexa: instruções e um simulador que monta o mesmo JSON que a Alexa enviaria e passa
    /// pela MESMA lógica da skill (sem precisar do Echo nem do ngrok).
    /// </summary>
    public class AlexaController : BaseController
    {
        public IActionResult Index()
        {
            ViewBag.Endpoint = $"{Request.Scheme}://{Request.Host}/api/alexa";
            ViewBag.AnuncioLigado = AnuncioAlexa.Ligado;
            return View(ConfigAlexa.Atual);
        }

        /// <summary>Chamado via AJAX pelo simulador (site.js)</summary>
        [HttpPost]
        public IActionResult Simular(string tipo, string intent, string sala, string grandeza, string minutos, string confirmacao)
        {
            try
            {
                var pedido = new JObject
                {
                    ["type"] = string.IsNullOrEmpty(tipo) ? "IntentRequest" : tipo,
                    ["requestId"] = "simulador." + Guid.NewGuid(),
                    ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ["locale"] = "pt-BR"
                };
                if ((string)pedido["type"] == "IntentRequest")
                {
                    var slots = new JObject();
                    if (!string.IsNullOrEmpty(sala)) slots["sala"] = new JObject { ["name"] = "sala", ["value"] = sala };
                    if (!string.IsNullOrEmpty(minutos)) slots["minutos"] = new JObject { ["name"] = "minutos", ["value"] = minutos };
                    if (!string.IsNullOrEmpty(grandeza))
                        slots["grandeza"] = new JObject
                        {
                            ["name"] = "grandeza",
                            ["value"] = grandeza,
                            // simula a resolução de sinônimos que a Alexa faz
                            ["resolutions"] = new JObject
                            {
                                ["resolutionsPerAuthority"] = new JArray(new JObject
                                {
                                    ["status"] = new JObject { ["code"] = "ER_SUCCESS_MATCH" },
                                    ["values"] = new JArray(new JObject { ["value"] = new JObject { ["name"] = grandeza, ["id"] = grandeza } })
                                })
                            }
                        };
                    pedido["intent"] = new JObject
                    {
                        ["name"] = intent,
                        ["confirmationStatus"] = string.IsNullOrEmpty(confirmacao) ? "NONE" : confirmacao,
                        ["slots"] = slots
                    };
                }
                var resposta = new AlexaSkill().Processa(new JObject { ["version"] = "1.0", ["request"] = pedido });
                var r = resposta["response"];
                bool confirmar = r["directives"] is JArray diretivas && diretivas.Count > 0 &&
                                 (string)diretivas[0]["type"] == "Dialog.ConfirmIntent";
                return Json(new
                {
                    fala = (string)r.SelectToken("outputSpeech.text"),
                    encerra = (bool?)r["shouldEndSession"] ?? true,
                    confirmar,
                    json = resposta.ToString(Formatting.Indented)
                });
            }
            catch (Exception erro)
            {
                return Json(new { erro = true, msg = MensagemDoErro(erro) });
            }
        }

        [HttpPost]
        public IActionResult TestarAnuncio()
        {
            return Json(new { msg = AnuncioAlexa.Anuncia("Teste de anúncio do Monitor de Sala Cirúrgica.") });
        }
    }
}
