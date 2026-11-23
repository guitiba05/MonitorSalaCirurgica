using MonitorSalaCirurgica.DAO;
using MonitorSalaCirurgica.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Linq;

namespace MonitorSalaCirurgica.Services.Alexa
{
    /// <summary>
    /// Lógica da skill "monitor cirúrgico": recebe o JSON da Alexa e devolve o JSON da resposta.
    /// Segue o padrão do projeto: NÃO fala com o ESP32. Lê o Orion e o STH-Comet pela AnaliseSala
    /// e, para silenciar ou testar, usa o Alarme (PATCH setAlert no Orion).
    ///
    /// Intents (modelo em alexa/skill-package/interactionModels/custom/pt-BR.json):
    ///   EstadoSalaIntent "como está a sala" · GrandezaIntent "qual a temperatura" · AlertaIntent "tem algum alerta"
    ///   LimitesIntent "quais são os limites" · SilenciarIntent "silenciar o alarme por 5 minutos" (pede confirmação)
    ///   ReativarIntent "reativar o alarme" · TestarAlertaIntent "testar o alerta de luz"
    /// Todos aceitam o número da sala ({sala}): "sala dois" → sala02.
    /// </summary>
    public class AlexaSkill
    {
        private const string Titulo = "Monitor de Sala Cirúrgica";
        private const string Ajuda =
            "Você pode perguntar: como está a sala, qual a temperatura, tem algum alerta, ou quais são os limites. " +
            "Também posso silenciar o alarme por alguns minutos, reativar o alarme ou testar um alerta. O que deseja?";

        public JObject Processa(JObject envelope)
        {
            var pedido = envelope["request"];
            string tipo = (string)pedido?["type"];
            try
            {
                if (tipo == "LaunchRequest") return Resposta("Monitor da sala cirúrgica. " + Ajuda, false, Ajuda);
                if (tipo == "IntentRequest") return TrataIntent((JObject)pedido["intent"]);
                if (tipo == "SessionEndedRequest") return new JObject { ["version"] = "1.0", ["response"] = new JObject() };
                return Resposta("Desculpe, não entendi o pedido.");
            }
            catch (FiwareIndisponivelException)
            {
                return Resposta("Não consegui falar com o FIWARE agora. Confira se a instância na AWS está ligada e se o IP está atualizado na aplicação.");
            }
            catch (ErroFiwareException erro)
            {
                return Resposta($"O {erro.Componente} respondeu com erro. Veja os detalhes na aplicação.");
            }
            catch (Exception erro)
            {
                Console.WriteLine("[Alexa] erro: " + erro.Message);
                return Resposta("Tive um problema ao consultar o monitor. Tente de novo em instantes.");
            }
        }

        private JObject TrataIntent(JObject intent)
        {
            string nome = (string)intent["name"];
            switch (nome)
            {
                case "AMAZON.HelpIntent": return Resposta(Ajuda, false, Ajuda);
                case "AMAZON.StopIntent":
                case "AMAZON.CancelIntent":
                case "AMAZON.NavigateHomeIntent": return Resposta("Até logo.");
                case "AMAZON.FallbackIntent": return Resposta("Não entendi. " + Ajuda, false, Ajuda);
            }

            // os outros intents precisam da sala
            string deviceId = ResolveSala(intent);
            var sala = new SalaDAO().Consulta(deviceId);
            if (sala == null)
                return Resposta($"Não encontrei a {deviceId.Replace("sala", "sala ")} no IoT Agent. Cadastre a sala na aplicação primeiro.");
            var cfg = AnaliseSala.ObtemOuCriaConfiguracao(sala);
            Grandeza grandeza = ResolveGrandeza(intent);

            switch (nome)
            {
                case "EstadoSalaIntent":
                    return Resposta(Falas.Resumo(AnaliseSala.Analisa(sala, cfg, false)));

                case "GrandezaIntent":
                {
                    var r = AnaliseSala.Analisa(sala, cfg, false);
                    return Resposta(grandeza == null ? Falas.Resumo(r) : Falas.Grandeza(r, grandeza));
                }

                case "AlertaIntent":
                {
                    var r = AnaliseSala.Analisa(sala, cfg, false);
                    return Resposta(r.Estado == null ? Falas.Resumo(r) : Falas.Alarme(r) + PadraoNaPlaca(r));
                }

                case "LimitesIntent":
                    return Resposta(Falas.Limites(AnaliseSala.Analisa(sala, cfg, false), grandeza));

                case "SilenciarIntent":
                {
                    var op = ConfigSupervisao.Atual;
                    int minutos = (int)Math.Max(1, Math.Min(Numero(intent, "minutos") ?? op.SilencioPadraoMinutos, op.SilencioMaximoMinutos));
                    string confirmacao = (string)intent["confirmationStatus"] ?? "NONE";
                    if (confirmacao == "DENIED")
                        return Resposta("Tudo bem, o alarme continua ligado.");
                    if (confirmacao != "CONFIRMED")
                        return ConfirmaIntent(intent, $"Silenciar o alarme da {cfg.Nome} por {minutos} {(minutos == 1 ? "minuto" : "minutos")}? " +
                                                      "O LED e o buzzer ficam desligados mesmo se algum valor sair do limite.");
                    minutos = Alarme.Silencia(sala, minutos, OrigemAlerta.Alexa);
                    return Resposta($"Pronto. Alarme da {cfg.Nome} silenciado por {minutos} {(minutos == 1 ? "minuto" : "minutos")}. " +
                                    "A supervisão volta sozinha depois disso, ou diga: reativar o alarme.");
                }

                case "ReativarIntent":
                    Alarme.Reativa(sala.Id);
                    return Resposta($"Supervisão automática reativada na {cfg.Nome}.");

                case "TestarAlertaIntent":
                {
                    if (grandeza == null)
                        return Resposta("Qual alerta você quer testar: luz, temperatura ou umidade?", false,
                                        "Diga, por exemplo: testar o alerta de temperatura.");
                    Alarme.Testa(sala, grandeza.Alerta, OrigemAlerta.Alexa);
                    var p = Contrato.Padrao(grandeza.Alerta);
                    return Resposta($"Testando o alerta de {grandeza.Chave} na {cfg.Nome} por {ConfigSupervisao.Atual.TesteSegundos} segundos: " +
                                    $"LED {p.Led} e buzzer em {p.BuzzerHz} hertz.");
                }

                default:
                    return Resposta("Ainda não sei fazer isso. " + Ajuda, false, Ajuda);
            }
        }

        private static string PadraoNaPlaca(ResumoSalaViewModel r)
        {
            if (r.UltimoEnviado == null || r.UltimoEnviado == "off") return "";
            var p = Contrato.Padrao(r.UltimoEnviado);
            return $" Na placa: LED azul {p.Led}, buzzer em {p.BuzzerHz} hertz" +
                   (r.Confirmado ? ", confirmado pelo ESP32." : ", aguardando a confirmação do ESP32.");
        }

        // ------------------------------------------------------------------
        //  Slots
        // ------------------------------------------------------------------
        private static double? Numero(JObject intent, string slot)
        {
            string valor = (string)intent.SelectToken($"slots.{slot}.value");
            double n;
            return double.TryParse(valor, NumberStyles.Float, CultureInfo.InvariantCulture, out n) ? n : (double?)null;
        }

        /// <summary>"sala 2" → sala02; sem número → Alexa:SalaPadrao</summary>
        private static string ResolveSala(JObject intent)
        {
            double? n = Numero(intent, "sala");
            return n.HasValue && n >= 1 && n < 1000 ? $"sala{(int)n.Value:00}" : ConfigAlexa.Atual.SalaPadrao;
        }

        /// <summary>
        /// Slot {grandeza}: com sinônimos ("calor", "luz", "umidade do ar") a Alexa devolve o id do valor
        /// em resolutions; sem resolução, compara o texto falado.
        /// </summary>
        private static Grandeza ResolveGrandeza(JObject intent)
        {
            var slot = intent.SelectToken("slots.grandeza");
            if (slot == null) return null;
            var autoridade = slot.SelectToken("resolutions.resolutionsPerAuthority[0]");
            if ((string)autoridade?.SelectToken("status.code") == "ER_SUCCESS_MATCH")
            {
                var g = Contrato.GrandezaPorChave((string)autoridade.SelectToken("values[0].value.id"));
                if (g != null) return g;
            }
            string falado = ((string)slot["value"] ?? "").ToLowerInvariant();
            if (falado.Contains("temp") || falado.Contains("calor") || falado.Contains("frio")) return Contrato.GrandezaPorChave("temperatura");
            if (falado.Contains("umid")) return Contrato.GrandezaPorChave("umidade");
            if (falado.Contains("lumin") || falado.Contains("luz") || falado.Contains("ilumina") || falado.Contains("claridade"))
                return Contrato.GrandezaPorChave("luminosidade");
            return null;
        }

        // ------------------------------------------------------------------
        //  Respostas no formato da Alexa
        // ------------------------------------------------------------------
        private static JObject Resposta(string texto, bool encerra = true, string reprompt = null)
        {
            var response = new JObject
            {
                ["outputSpeech"] = new JObject { ["type"] = "PlainText", ["text"] = texto },
                ["card"] = new JObject { ["type"] = "Simple", ["title"] = Titulo, ["content"] = texto },
                ["shouldEndSession"] = encerra
            };
            if (reprompt != null)
                response["reprompt"] = new JObject { ["outputSpeech"] = new JObject { ["type"] = "PlainText", ["text"] = reprompt } };
            return new JObject { ["version"] = "1.0", ["response"] = response };
        }

        /// <summary>Pede "sim ou não" antes de executar (Dialog.ConfirmIntent)</summary>
        private static JObject ConfirmaIntent(JObject intent, string pergunta)
        {
            var resposta = Resposta(pergunta, false, "Diga sim para confirmar ou não para cancelar.");
            var atualizado = (JObject)intent.DeepClone();
            atualizado["confirmationStatus"] = "NONE";
            resposta["response"]["directives"] = new JArray(new JObject { ["type"] = "Dialog.ConfirmIntent", ["updatedIntent"] = atualizado });
            return resposta;
        }
    }
}
