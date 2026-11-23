using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace MonitorSalaCirurgica.DAO
{
    /// <summary>Erro devolvido por uma API do FIWARE, com o código HTTP (ex.: 409 = já existe)</summary>
    public class ErroFiwareException : Exception
    {
        public HttpStatusCode Status { get; }
        public string Codigo { get; }         // ex.: DUPLICATE_DEVICE_ID, NotFound
        public string Componente { get; }

        public ErroFiwareException(HttpStatusCode status, string codigo, string componente, string mensagem) : base(mensagem)
        {
            Status = status;
            Codigo = codigo;
            Componente = componente;
        }
    }

    /// <summary>O FIWARE não respondeu (EC2 desligada, IP errado ou porta fechada no Security Group)</summary>
    public class FiwareIndisponivelException : Exception
    {
        public FiwareIndisponivelException(string mensagem) : base(mensagem) { }
    }

    /// <summary>Resposta de uma chamada: conteúdo e cabeçalhos (Location, fiware-total-count)</summary>
    public class RespostaFiware
    {
        public string Conteudo { get; set; }
        public HttpResponseHeaders Cabecalhos { get; set; }

        public string Cabecalho(string nome)
        {
            System.Collections.Generic.IEnumerable<string> valores;
            return Cabecalhos != null && Cabecalhos.TryGetValues(nome, out valores) ? string.Join(",", valores) : null;
        }
    }

    /// <summary>
    /// Centraliza as chamadas às APIs do FIWARE (IoT Agent :4041, Orion :1026 e STH-Comet :8666),
    /// do mesmo modo que o HelperDAO centraliza o acesso ao banco. O consumo de API segue o
    /// capítulo 37 da apostila: HttpClient com .Result e proxy opcional.
    /// A aplicação NUNCA fala direto com o ESP32.
    /// </summary>
    public static class HelperFiware
    {
        // um único HttpClient para toda a aplicação (evita esgotar as conexões)
        private static HttpClient httpClient;

        public static ConfigFiware Config { get; private set; } = new ConfigFiware();

        /// <summary>Chamado no Startup e quando o IP é trocado na tela Conexão</summary>
        public static void Configura(ConfigFiware config)
        {
            Config = config;
            var handler = new HttpClientHandler();
            if (!string.IsNullOrWhiteSpace(config.Proxy))
            {
                handler.Proxy = new WebProxy //o proxy é opcional (restrições da rede da FESA)
                {
                    Address = new Uri(config.Proxy),
                    BypassProxyOnLocal = true,
                    UseDefaultCredentials = true, //não informaremos usuário e senha
                };
            }
            httpClient = new HttpClient(handler);
            httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, config.TimeoutSegundos));
        }

        public static string UrlOrion => MontaUrlBase(Config.PortaOrion);
        public static string UrlIotAgent => MontaUrlBase(Config.PortaIotAgent);
        public static string UrlSth => MontaUrlBase(Config.PortaSth);

        private static string MontaUrlBase(int porta)
        {
            return "http://" + Config.HostLimpo + ":" + porta;
        }

        private static string NomeComponente(string url)
        {
            int porta = new Uri(url).Port;
            if (porta == Config.PortaIotAgent) return "IoT Agent";
            if (porta == Config.PortaOrion) return "Orion";
            if (porta == Config.PortaSth) return "STH-Comet";
            return "FIWARE";
        }

        /// <summary>
        /// Executa uma requisição no FIWARE com os cabeçalhos fiware-service e fiware-servicepath.
        /// </summary>
        /// <param name="retornaNullSeNaoEncontrado">Se true, devolve null quando a API responder 404</param>
        public static RespostaFiware Executa(HttpMethod metodo, string url, JToken corpo = null,
                                             bool retornaNullSeNaoEncontrado = false, bool comServico = true)
        {
            if (!Config.Configurado)
                throw new FiwareIndisponivelException("O IP do FIWARE não está configurado. Preencha Fiware:Host no appsettings.json ou na tela Conexão.");

            using (var requisicao = new HttpRequestMessage(metodo, url))
            {
                if (comServico)
                {
                    requisicao.Headers.Add("fiware-service", Config.Service);
                    requisicao.Headers.Add("fiware-servicepath", Config.ServicePath);
                }
                if (corpo != null)
                {
                    requisicao.Content = new StringContent(corpo.ToString(Formatting.None), Encoding.UTF8);
                    // o Orion quer exatamente "application/json" (sem charset)
                    requisicao.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                }

                HttpResponseMessage resposta;
                try
                {
                    resposta = httpClient.SendAsync(requisicao).Result;
                }
                catch (AggregateException erro) when (erro.InnerException is System.Threading.Tasks.TaskCanceledException)
                {
                    throw new FiwareIndisponivelException($"O {NomeComponente(url)} não respondeu em {Config.TimeoutSegundos} s " +
                        $"({MontaUrlBase(new Uri(url).Port)}). Confira se a EC2 está ligada, o IP atual e as portas no Security Group.");
                }
                catch (AggregateException erro)
                {
                    throw new FiwareIndisponivelException($"Não foi possível conectar ao {NomeComponente(url)} " +
                        $"({MontaUrlBase(new Uri(url).Port)}). Detalhe: {erro.InnerException?.Message}");
                }

                using (resposta)
                {
                    string conteudo = resposta.Content.ReadAsStringAsync().Result;
                    if (resposta.StatusCode == HttpStatusCode.NotFound && retornaNullSeNaoEncontrado)
                        return null;
                    if (!resposta.IsSuccessStatusCode)
                        throw MontaErro(resposta.StatusCode, conteudo, NomeComponente(url));
                    return new RespostaFiware { Conteudo = conteudo, Cabecalhos = resposta.Headers };
                }
            }
        }

        public static string ExecutaRequisicao(HttpMethod metodo, string url, JToken corpo = null,
                                               bool retornaNullSeNaoEncontrado = false)
        {
            return Executa(metodo, url, corpo, retornaNullSeNaoEncontrado)?.Conteudo;
        }

        /// <summary>Executa a requisição e já converte a resposta para JSON</summary>
        public static JToken ExecutaRequisicaoJson(HttpMethod metodo, string url, JToken corpo = null,
                                                   bool retornaNullSeNaoEncontrado = false, bool comServico = true)
        {
            var resposta = Executa(metodo, url, corpo, retornaNullSeNaoEncontrado, comServico);
            return LeJson(resposta?.Conteudo);
        }

        public static JToken LeJson(string conteudo)
        {
            if (string.IsNullOrWhiteSpace(conteudo))
                return null;
            // DateParseHandling.None: mantém as datas como texto para tratarmos o fuso horário
            return JsonConvert.DeserializeObject<JToken>(conteudo,
                new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
        }

        /// <summary>
        /// Lê os formatos de erro de cada componente e monta uma mensagem clara:
        ///   Orion:     {"error":"NotFound","description":"..."}
        ///   IoT Agent: {"name":"DUPLICATE_DEVICE_ID","message":"..."}
        ///   STH-Comet: {"statusCode":400,"error":"Bad Request","message":"..."}
        /// </summary>
        private static ErroFiwareException MontaErro(HttpStatusCode status, string conteudo, string componente)
        {
            string codigo = null, detalhe = conteudo;
            try
            {
                var json = JObject.Parse(conteudo);
                codigo = (string)(json["name"] ?? json["error"]);
                detalhe = (string)(json["description"] ?? json["message"]) ?? conteudo;
            }
            catch (JsonException)
            {
                // a resposta não era JSON: usamos o texto puro
            }

            string amigavel = null;
            switch (codigo)
            {
                case "DUPLICATE_DEVICE_ID": amigavel = "Já existe uma sala com esse device_id (e apikey) no IoT Agent."; break;
                case "DUPLICATE_GROUP": amigavel = "Já existe um grupo de serviço com esse apikey."; break;
                case "DEVICE_NOT_FOUND": amigavel = "Sala não encontrada no IoT Agent."; break;
            }
            if (amigavel == null && status == HttpStatusCode.UnprocessableEntity) amigavel = "Já existe no Orion.";

            string msg = $"O {componente} retornou erro {(int)status}" + (amigavel != null ? $": {amigavel}" : "") +
                         (string.IsNullOrEmpty(detalhe) ? "" : $" ({(detalhe.Length > 200 ? detalhe.Substring(0, 200) : detalhe)})");
            return new ErroFiwareException(status, codigo, componente, msg);
        }

        /// <summary>Codifica um valor para a URL, mantendo os ":" dos ids "urn:ngsi-ld:..."</summary>
        public static string Codifica(string valor)
        {
            return Uri.EscapeDataString(valor ?? "").Replace("%3A", ":");
        }

        /// <summary>Converte um valor JSON em texto sem depender da cultura (ponto decimal)</summary>
        public static string TokenParaTexto(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;
            if (token is JValue valor)
                return Convert.ToString(valor.Value, CultureInfo.InvariantCulture);
            return token.ToString(Formatting.None);
        }

        /// <summary>Número que pode vir como 23.5 ou "23.5" (IoT Agent sem autocast)</summary>
        public static double? TokenParaNumero(JToken token)
        {
            string texto = TokenParaTexto(token);
            double valor;
            if (texto != null && double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out valor))
                return valor;
            return null;
        }

        /// <summary>Converte a data UTC do FIWARE (ex.: 2026-11-19T12:34:56.789Z) para o horário local</summary>
        public static DateTime ConverteDataFiware(string data)
        {
            return DateTime.Parse(data, CultureInfo.InvariantCulture,
                                  DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal).ToLocalTime();
        }

        /// <summary>Data local → texto UTC no formato do STH-Comet (dateFrom, dateTo)</summary>
        public static string FormataDataFiware(DateTime dataLocal)
        {
            return dataLocal.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        }

        /// <summary>Data/hora em milissegundos, formato usado pelo javascript dos gráficos</summary>
        public static long ConverteParaMilissegundos(DateTime dataLocal)
        {
            return new DateTimeOffset(dataLocal).ToUnixTimeMilliseconds();
        }

        /// <summary>Versão de um componente (requisições 0.1 a 0.3 da coleção)</summary>
        public static string ConsultaVersao(string url)
        {
            var json = ExecutaRequisicaoJson(HttpMethod.Get, url, comServico: false);
            return TokenParaTexto(json?.SelectToken("orion.version") ?? json?.SelectToken("version")) ?? "online";
        }
    }
}
