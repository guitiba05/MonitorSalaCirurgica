using MonitorSalaCirurgica.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace MonitorSalaCirurgica.DAO
{
    /// <summary>
    /// Histórico das mensagens IoT gravado pelo STH-Comet (MongoDB).
    /// O STH-Comet aceita no máximo 100 registros por chamada (lastN e hLimit): períodos maiores
    /// são lidos de 100 em 100 com hOffset, e períodos longos usam a média por minuto.
    /// </summary>
    public class MensagemIoTDAO
    {
        public const int MaximoPorPagina = 100;

        private string UrlAtributo(string entidadeId, string atributo) =>
            $"{HelperFiware.UrlSth}/STH/v1/contextEntities/type/{Contrato.TipoEntidade}" +
            $"/id/{HelperFiware.Codifica(entidadeId)}/attributes/{HelperFiware.Codifica(atributo)}";

        private static string Periodo(DateTime? dataInicial, DateTime? dataFinal)
        {
            string p = "";
            if (dataInicial.HasValue) p += "&dateFrom=" + HelperFiware.FormataDataFiware(dataInicial.Value);
            if (dataFinal.HasValue) p += "&dateTo=" + HelperFiware.FormataDataFiware(dataFinal.Value);
            return p;
        }

        /// <summary>contextResponses[0].contextElement.attributes[0].values</summary>
        private static JArray Valores(JToken json) =>
            json?.SelectToken("contextResponses[0].contextElement.attributes[0].values") as JArray ?? new JArray();

        /// <summary>Lê uma página bruta (do mais antigo para o mais novo) e, se pedido, o total</summary>
        private List<MensagemIoTViewModel> Le(string entidadeId, Grandeza g, string parametros, out int total)
        {
            var resposta = HelperFiware.Executa(HttpMethod.Get, UrlAtributo(entidadeId, g.Atributo) + "?" + parametros,
                                                retornaNullSeNaoEncontrado: true);
            var lista = new List<MensagemIoTViewModel>();
            total = 0;
            if (resposta == null)
                return lista;
            foreach (var v in Valores(HelperFiware.LeJson(resposta.Conteudo)))
            {
                double? valor = HelperFiware.TokenParaNumero(v["attrValue"]);
                if (valor.HasValue && v["recvTime"] != null)
                    lista.Add(new MensagemIoTViewModel
                    {
                        DataHora = HelperFiware.ConverteDataFiware((string)v["recvTime"]),
                        Atributo = g.Atributo,
                        Valor = valor.Value,
                        Unidade = g.Unidade
                    });
            }
            int.TryParse(resposta.Cabecalho("fiware-total-count"), out total);
            return lista;
        }

        /// <summary>
        /// Uma página da consulta de mensagens (tela Consulta). O STH-Comet pagina do mais antigo
        /// para o mais novo; para "mais recentes primeiro", a página 1 é o fim: hOffset = total − página × tamanho.
        /// </summary>
        public PaginaMensagensViewModel ConsultaPagina(SalaViewModel sala, Grandeza g, DateTime? dataInicial,
                                                       DateTime? dataFinal, int pagina, int tamanho, bool maisRecentes)
        {
            tamanho = Math.Max(5, Math.Min(tamanho, MaximoPorPagina));
            int total;
            Le(sala.NomeEntidade, g, "hLimit=1&hOffset=0&count=true" + Periodo(dataInicial, dataFinal), out total);

            var resultado = new PaginaMensagensViewModel
            {
                Total = total,
                Tamanho = tamanho,
                Paginas = Math.Max(1, (int)Math.Ceiling(total / (double)tamanho)),
                NomeGrandeza = g.Nome,
                Unidade = g.Unidade,
                Entidade = sala.NomeEntidade
            };
            resultado.Pagina = Math.Max(1, Math.Min(pagina, resultado.Paginas));

            int offset, limite = tamanho;
            if (maisRecentes)
            {
                offset = total - resultado.Pagina * tamanho;
                if (offset < 0) { limite += offset; offset = 0; }
            }
            else
                offset = (resultado.Pagina - 1) * tamanho;

            if (total > 0 && limite > 0)
            {
                int ignorado;
                var itens = Le(sala.NomeEntidade, g, $"hLimit={limite}&hOffset={offset}" + Periodo(dataInicial, dataFinal), out ignorado);
                if (maisRecentes) itens.Reverse();
                int inicio = (resultado.Pagina - 1) * tamanho;
                for (int n = 0; n < itens.Count; n++)
                {
                    itens[n].Numero = inicio + n + 1;
                    itens[n].Sala = sala.Id;
                }
                resultado.Itens = itens;
            }
            return resultado;
        }

        /// <summary>Todas as medidas de um período, de 100 em 100 (gráficos e média da janela)</summary>
        public List<MensagemIoTViewModel> ConsultaPeriodo(string entidadeId, Grandeza g, DateTime dataInicial,
                                                          DateTime? dataFinal, int maximo = 600)
        {
            var todas = new List<MensagemIoTViewModel>();
            for (int offset = 0; offset < maximo; offset += MaximoPorPagina)
            {
                int ignorado;
                var pagina = Le(entidadeId, g, $"hLimit={MaximoPorPagina}&hOffset={offset}" + Periodo(dataInicial, dataFinal), out ignorado);
                todas.AddRange(pagina);
                if (pagina.Count < MaximoPorPagina)
                    break;
            }
            return todas;
        }

        /// <summary>
        /// Média por minuto (aggrMethod=sum: média = soma / amostras), para períodos longos.
        /// Resposta: {"_id":{"origin":"...T22:00:00Z"},"points":[{"offset":5,"samples":12,"sum":282}]}
        /// </summary>
        public List<PontoGrafico> ConsultaMediaPorMinuto(string entidadeId, Grandeza g, DateTime dataInicial, DateTime dataFinal)
        {
            string url = UrlAtributo(entidadeId, g.Atributo) + "?aggrMethod=sum&aggrPeriod=minute" + Periodo(dataInicial, dataFinal);
            var json = HelperFiware.ExecutaRequisicaoJson(HttpMethod.Get, url, retornaNullSeNaoEncontrado: true);
            var pontos = new List<PontoGrafico>();
            foreach (var bloco in Valores(json))
            {
                string origem = (string)bloco.SelectToken("_id.origin");
                if (origem == null) continue;
                DateTime inicioHora = HelperFiware.ConverteDataFiware(origem);
                foreach (var p in bloco["points"] as JArray ?? new JArray())
                {
                    int amostras = (int?)p["samples"] ?? 0;
                    double? soma = HelperFiware.TokenParaNumero(p["sum"]);
                    if (amostras == 0 || !soma.HasValue) continue;
                    DateTime instante = inicioHora.AddMinutes((int)p["offset"]);
                    if (instante >= dataInicial && instante <= dataFinal)
                        pontos.Add(new PontoGrafico
                        {
                            X = HelperFiware.ConverteParaMilissegundos(instante),
                            Y = Math.Round(soma.Value / amostras, 2)
                        });
                }
            }
            return pontos.OrderBy(p => p.X).ToList();
        }

        /// <summary>Média da grandeza na janela [agora − janela, agora]. Pontos = 0 se não há medidas.</summary>
        public double? MediaJanela(string entidadeId, Grandeza g, int janelaSegundos, out int pontos)
        {
            DateTime inicio = DateTime.Now.AddSeconds(-janelaSegundos);
            int ignorado;
            var lista = Le(entidadeId, g, $"lastN={MaximoPorPagina}" + Periodo(inicio, null), out ignorado)
                        .Where(m => m.DataHora >= inicio).ToList();
            pontos = lista.Count;
            return pontos == 0 ? (double?)null : lista.Average(m => m.Valor);
        }
    }
}
