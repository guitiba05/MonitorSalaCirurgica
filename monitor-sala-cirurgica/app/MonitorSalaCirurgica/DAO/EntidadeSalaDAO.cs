using MonitorSalaCirurgica.Models;
using Newtonsoft.Json.Linq;
using System.Net.Http;

namespace MonitorSalaCirurgica.DAO
{
    /// <summary>
    /// Entidade da sala no Orion (urn:ngsi-ld:SalaCirurgica:001): estado atual e comandos.
    /// Comando = PATCH na entidade; o Orion repassa ao IoT Agent, que publica no MQTT.
    /// </summary>
    public class EntidadeSalaDAO
    {
        private string UrlEntidade(string entidadeId) =>
            HelperFiware.UrlOrion + "/v2/entities/" + HelperFiware.Codifica(entidadeId);

        /// <summary>GET /v2/entities/{id}?options=keyValues; null se a entidade ainda não existe</summary>
        public EstadoSalaViewModel ConsultaEstado(string entidadeId)
        {
            var json = HelperFiware.ExecutaRequisicaoJson(HttpMethod.Get, UrlEntidade(entidadeId) + "?options=keyValues",
                                                          retornaNullSeNaoEncontrado: true);
            if (json == null)
                return null;
            EstadoSalaViewModel e = new EstadoSalaViewModel();
            e.EntityId = entidadeId;
            e.Temperatura = HelperFiware.TokenParaNumero(json["temperature"]);
            e.Umidade = HelperFiware.TokenParaNumero(json["humidity"]);
            e.Luminosidade = HelperFiware.TokenParaNumero(json["luminosity"]);
            e.Alerta = HelperFiware.TokenParaTexto(json[Contrato.AtributoAlerta]);
            e.StatusComando = HelperFiware.TokenParaTexto(json[Contrato.Comando + "_status"]);
            e.InfoComando = HelperFiware.TokenParaTexto(json[Contrato.Comando + "_info"]);
            string instante = HelperFiware.TokenParaTexto(json["TimeInstant"]);
            if (!string.IsNullOrEmpty(instante))
                e.AtualizadoEm = HelperFiware.ConverteDataFiware(instante);
            return e;
        }

        /// <summary>
        /// Envia setAlert (luz, temp, umid ou off): o IoT Agent publica "sala01@setAlert|temp"
        /// em /CIRURGIA01/sala01/cmd e o ESP32 confirma em /cmdexe.
        /// </summary>
        public void EnviaComandoAlerta(string entidadeId, string alerta)
        {
            if (!Contrato.AlertaValido(alerta))
                throw new System.Exception("Alerta inválido: " + alerta);
            var corpo = new JObject
            {
                [Contrato.Comando] = new JObject { ["type"] = "command", ["value"] = alerta }
            };
            HelperFiware.Executa(new HttpMethod("PATCH"), UrlEntidade(entidadeId) + "/attrs?type=" + Contrato.TipoEntidade, corpo);
        }

        public void Delete(string entidadeId)
        {
            HelperFiware.Executa(HttpMethod.Delete, UrlEntidade(entidadeId), retornaNullSeNaoEncontrado: true);
        }
    }
}
