using MonitorSalaCirurgica.Models;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace MonitorSalaCirurgica.DAO
{
    /// <summary>
    /// DAO genérico para recursos gravados direto no FIWARE via API (mesma ideia do PadraoDAO do
    /// CadAluno): a URL da coleção faz o papel da tabela, CriaJson o papel do CriaParametros e
    /// MontaModel o mesmo papel do PadraoDAO. Os métodos são virtuais para cada API ajustar o formato.
    /// </summary>
    public abstract class PadraoFiwareDAO<T> where T : PadraoFiwareViewModel
    {
        /// <summary>Endereço da coleção (ex.: http://IP:4041/iot/devices)</summary>
        protected abstract string UrlColecao { get; }
        protected abstract JObject CriaJson(T model);
        protected abstract T MontaModel(JObject registro);

        protected virtual string UrlItem(string id) => UrlColecao + "/" + HelperFiware.Codifica(id);
        protected virtual string ParametrosListagem => "?limit=1000";
        protected virtual HttpMethod MetodoUpdate => HttpMethod.Put;
        protected virtual JToken CorpoInsert(T model) => CriaJson(model);
        protected virtual JToken CorpoUpdate(T model) => CriaJson(model);
        protected virtual IEnumerable<JObject> ExtraiLista(JToken json) => json.Children<JObject>();

        /// <summary>Insere e devolve o id (o informado ou o gerado pelo FIWARE no cabeçalho Location)</summary>
        public virtual string Insert(T model)
        {
            var resposta = HelperFiware.Executa(HttpMethod.Post, UrlColecao, CorpoInsert(model));
            string local = resposta.Cabecalho("Location");
            return string.IsNullOrEmpty(local) ? model.Id : local.Split('/').Last();
        }

        public virtual void Update(T model)
        {
            HelperFiware.Executa(MetodoUpdate, UrlItem(model.Id), CorpoUpdate(model));
        }

        public virtual void Delete(string id)
        {
            HelperFiware.Executa(HttpMethod.Delete, UrlItem(id), retornaNullSeNaoEncontrado: true);
        }

        public virtual T Consulta(string id)
        {
            var json = HelperFiware.ExecutaRequisicaoJson(HttpMethod.Get, UrlItem(id), retornaNullSeNaoEncontrado: true);
            if (json == null)
                return null;
            else
                return MontaModel((JObject)json);
        }

        public virtual List<T> Listagem()
        {
            var json = HelperFiware.ExecutaRequisicaoJson(HttpMethod.Get, UrlColecao + ParametrosListagem);
            List<T> lista = new List<T>();
            if (json != null)
                foreach (JObject registro in ExtraiLista(json))
                    lista.Add(MontaModel(registro));
            return lista.OrderBy(m => m.Id).ToList();
        }
    }
}
