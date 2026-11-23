using MonitorSalaCirurgica.Models;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace MonitorSalaCirurgica.DAO
{
    /// <summary>
    /// CRUD das inscrições (/v2/subscriptions) do Orion que alimentam o STH-Comet.
    /// O STH-Comet não busca os dados: ele recebe notificações do Orion. É preciso UMA inscrição
    /// POR GRANDEZA (requisição 2.6 da coleção). Como o Orion 4.x não tem mais o formato "legacy",
    /// a notificação é montada com httpCustom no formato NGSIv1 que o STH-Comet entende.
    /// </summary>
    public class InscricaoDAO : PadraoFiwareDAO<InscricaoViewModel>
    {
        protected override string UrlColecao => HelperFiware.UrlOrion + "/v2/subscriptions";
        protected override HttpMethod MetodoUpdate => new HttpMethod("PATCH");

        /// <summary>Corpo da notificação em NGSIv1: o Orion troca ${id}, ${type} e ${atributo} pelos valores</summary>
        private static JObject Notificacao(string atributo, string url)
        {
            string tipo = Contrato.GrandezaPorAtributo(atributo)?.TipoNgsi ?? "Number";
            return new JObject
            {
                ["httpCustom"] = new JObject
                {
                    ["url"] = string.IsNullOrWhiteSpace(url) ? HelperFiware.Config.UrlNotificacaoSth : url,
                    ["json"] = new JObject
                    {
                        ["contextResponses"] = new JArray(new JObject
                        {
                            ["contextElement"] = new JObject
                            {
                                ["id"] = "${id}",
                                ["type"] = "${type}",
                                ["isPattern"] = "false",
                                // o placeholder ocupa o valor inteiro: o número chega ao histórico como número
                                ["attributes"] = new JArray(new JObject
                                {
                                    ["name"] = atributo,
                                    ["type"] = tipo,
                                    ["value"] = "${" + atributo + "}"
                                })
                            },
                            ["statusCode"] = new JObject { ["code"] = "200", ["reasonPhrase"] = "OK" }
                        })
                    }
                },
                ["attrs"] = new JArray(atributo)
            };
        }

        protected override JObject CriaJson(InscricaoViewModel i)
        {
            JObject json = new JObject();
            json["description"] = string.IsNullOrWhiteSpace(i.Descricao) ? $"STH-Comet: {i.Atributo} de {i.EntidadeId}" : i.Descricao;
            json["subject"] = new JObject
            {
                ["entities"] = new JArray(new JObject { ["id"] = i.EntidadeId, ["type"] = i.EntidadeTipo ?? Contrato.TipoEntidade }),
                ["condition"] = new JObject { ["attrs"] = new JArray(i.Atributo) }
            };
            json["notification"] = Notificacao(i.Atributo, i.Url);
            if (i.Throttling > 0)
                json["throttling"] = i.Throttling;
            return json;
        }

        /// <summary>PATCH: o objeto "notification" é substituído inteiro, então é montado de novo</summary>
        protected override JToken CorpoUpdate(InscricaoViewModel i) => new JObject
        {
            ["description"] = i.Descricao ?? "",
            ["status"] = i.Status == "inactive" ? "inactive" : "active",
            ["throttling"] = i.Throttling,
            ["notification"] = Notificacao(i.Atributo, i.Url)
        };

        protected override InscricaoViewModel MontaModel(JObject s)
        {
            var entidade = s.SelectToken("subject.entities[0]");
            var notif = s["notification"];
            InscricaoViewModel i = new InscricaoViewModel();
            i.Id = (string)s["id"];
            i.Descricao = (string)s["description"];
            i.Status = (string)s["status"] ?? "active";
            i.EntidadeId = (string)(entidade?["id"] ?? entidade?["idPattern"]);
            i.EntidadeTipo = (string)entidade?["type"];
            i.Atributo = string.Join(", ", (notif?["attrs"] as JArray ?? new JArray()).Select(a => (string)a));
            i.Url = (string)(notif?.SelectToken("httpCustom.url") ?? notif?.SelectToken("http.url"));
            i.Formato = notif?["httpCustom"] != null ? "httpCustom (NGSIv1)" : (string)notif?["attrsFormat"] ?? "normalized";
            i.Throttling = (int?)s["throttling"] ?? 0;
            i.VezesEnviada = (int?)notif?["timesSent"] ?? 0;
            i.UltimaNotificacao = Data(notif?["lastNotification"]);
            i.UltimoSucesso = Data(notif?["lastSuccess"]);
            i.UltimaFalha = Data(notif?["lastFailure"]);
            i.MotivoFalha = (string)notif?["lastFailureReason"];
            return i;
        }

        private static System.DateTime? Data(JToken t) =>
            t == null || t.Type == JTokenType.Null ? (System.DateTime?)null : HelperFiware.ConverteDataFiware((string)t);

        /// <summary>Inscrições de uma entidade (inclusive as criadas pelo Postman)</summary>
        public List<InscricaoViewModel> ListagemDaEntidade(string entidadeId) =>
            Listagem().Where(i => i.EntidadeId == entidadeId).ToList();

        /// <summary>
        /// Cria as inscrições que faltam para as três grandezas da sala. As que já existem não são
        /// duplicadas: inscrição repetida grava cada medida duas vezes e distorce as médias.
        /// Devolve quantas foram criadas.
        /// </summary>
        public int CriaPadraoDaSala(SalaViewModel sala)
        {
            var existentes = ListagemDaEntidade(sala.NomeEntidade);
            int criadas = 0;
            foreach (var g in Contrato.GrandezasExibicao)
            {
                if (existentes.Any(e => e.Atributo == g.Atributo))
                    continue;
                Insert(new InscricaoViewModel
                {
                    EntidadeId = sala.NomeEntidade,
                    EntidadeTipo = Contrato.TipoEntidade,
                    Atributo = g.Atributo
                });
                criadas++;
            }
            return criadas;
        }

        public int RemoveDaEntidade(string entidadeId)
        {
            var lista = ListagemDaEntidade(entidadeId);
            foreach (var i in lista)
                Delete(i.Id);
            return lista.Count;
        }
    }
}
