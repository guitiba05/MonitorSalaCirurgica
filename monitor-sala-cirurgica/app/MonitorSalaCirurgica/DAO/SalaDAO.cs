using MonitorSalaCirurgica.Models;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace MonitorSalaCirurgica.DAO
{
    /// <summary>
    /// CRUD das salas direto no IoT Agent (/iot/devices), com os atributos e o comando do contrato:
    ///   t → temperature · h → humidity · l → luminosity · a → alert · comando setAlert.
    /// Faz o que as requisições 2.1 e 2.3 da coleção do FIWARE Made Simple fazem pelo Postman.
    /// </summary>
    public class SalaDAO : PadraoFiwareDAO<SalaViewModel>
    {
        protected override string UrlColecao => HelperFiware.UrlIotAgent + "/iot/devices";

        /// <summary>Atributos do contrato: letra do UltraLight → atributo da entidade</summary>
        public static JArray AtributosDoContrato()
        {
            JArray atributos = new JArray();
            foreach (var g in Contrato.GrandezasExibicao)
                atributos.Add(new JObject { ["object_id"] = g.ObjectId, ["name"] = g.Atributo, ["type"] = g.TipoNgsi });
            atributos.Add(new JObject { ["object_id"] = "a", ["name"] = Contrato.AtributoAlerta, ["type"] = "Text" });
            return atributos;
        }

        private static JArray ComandosDoContrato()
        {
            return new JArray(new JObject { ["name"] = Contrato.Comando, ["type"] = "command" });
        }

        private static JArray AtributosEstaticos(SalaViewModel sala)
        {
            // o nome aparece na entidade do Orion junto com as medidas
            return new JArray(new JObject { ["name"] = "nome", ["type"] = "Text", ["value"] = sala.Nome ?? sala.Id });
        }

        protected override JObject CriaJson(SalaViewModel sala)
        {
            JObject json = new JObject();
            json["device_id"] = sala.Id;
            json["entity_name"] = sala.NomeEntidade;
            json["entity_type"] = Contrato.TipoEntidade;
            json["apikey"] = sala.Apikey;
            json["protocol"] = "PDI-IoTA-UltraLight";
            json["transport"] = "MQTT";
            json["attributes"] = AtributosDoContrato();
            json["commands"] = ComandosDoContrato();
            json["static_attributes"] = AtributosEstaticos(sala);
            return json;
        }

        /// <summary>POST /iot/devices recebe uma lista: {"devices": [ ... ]}</summary>
        protected override JToken CorpoInsert(SalaViewModel sala) => new JObject { ["devices"] = new JArray(CriaJson(sala)) };

        /// <summary>
        /// PUT /iot/devices/{id}: reaplica atributos e comando do contrato e o nome.
        /// (O IoT Agent não deixa mudar device_id, entidade nem apikey na alteração.)
        /// </summary>
        protected override JToken CorpoUpdate(SalaViewModel sala) => new JObject
        {
            ["attributes"] = AtributosDoContrato(),
            ["commands"] = ComandosDoContrato(),
            ["static_attributes"] = AtributosEstaticos(sala)
        };

        /// <summary>GET /iot/devices devolve {"count": n, "devices": [ ... ]}</summary>
        protected override IEnumerable<JObject> ExtraiLista(JToken json) =>
            (json["devices"] as JArray ?? new JArray()).Children<JObject>();

        protected override SalaViewModel MontaModel(JObject registro)
        {
            SalaViewModel s = new SalaViewModel();
            s.Id = (string)registro["device_id"];
            s.NomeEntidade = (string)registro["entity_name"];
            s.Apikey = (string)registro["apikey"];
            s.Transporte = (string)registro["transport"];
            s.QuantidadeAtributos = (registro["attributes"] as JArray)?.Count ?? 0;
            s.TemComando = (registro["commands"] as JArray ?? new JArray()).Any(c => (string)c["name"] == Contrato.Comando);
            var nome = (registro["static_attributes"] as JArray ?? new JArray()).FirstOrDefault(a => (string)a["name"] == "nome");
            s.Nome = nome == null ? null : HelperFiware.TokenParaTexto(nome["value"]);
            return s;
        }

        /// <summary>
        /// Grupo de serviço do apikey (requisição 2.1): diz ao IoT Agent qual tipo de entidade criar.
        /// Se já existir, é reaproveitado.
        /// </summary>
        public void GaranteServiceGroup(string apikey)
        {
            var json = HelperFiware.ExecutaRequisicaoJson(HttpMethod.Get, HelperFiware.UrlIotAgent + "/iot/services?limit=1000");
            var grupos = json?["services"] as JArray ?? new JArray();
            if (grupos.Any(g => (string)g["apikey"] == apikey))
                return;

            var corpo = new JObject
            {
                ["services"] = new JArray(new JObject
                {
                    ["apikey"] = apikey,
                    ["resource"] = HelperFiware.Config.Recurso,
                    ["entity_type"] = Contrato.TipoEntidade,
                    ["autoprovision"] = false
                })
            };
            try
            {
                HelperFiware.Executa(HttpMethod.Post, HelperFiware.UrlIotAgent + "/iot/services", corpo);
            }
            catch (ErroFiwareException erro) when (erro.Status == HttpStatusCode.Conflict)
            {
                // criado por outra pessoa ao mesmo tempo: tudo certo
            }
        }

        /// <summary>Salas que seguem o contrato (têm o comando setAlert): as que a supervisão acompanha</summary>
        public List<SalaViewModel> ListagemDoContrato() => Listagem().Where(s => s.TemComando).ToList();
    }
}
