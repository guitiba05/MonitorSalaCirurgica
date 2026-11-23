using MonitorSalaCirurgica.Models;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace MonitorSalaCirurgica.Services
{
    /// <summary>
    /// Anúncio por voz no Echo quando a supervisão dispara ou encerra um alerta (opcional).
    /// Uma skill só fala quando alguém pergunta; para a Alexa falar sozinha, usamos um serviço de
    /// anúncio que recebe a frase pela URL, como o Voice Monkey (Alexa:UrlAnuncio com {texto}).
    /// </summary>
    public static class AnuncioAlexa
    {
        private static readonly HttpClient cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        public static bool Ligado => !string.IsNullOrWhiteSpace(ConfigAlexa.Atual.UrlAnuncio);

        /// <summary>Faz um GET na URL configurada; devolve a mensagem do resultado (nunca lança exceção)</summary>
        public static string Anuncia(string texto)
        {
            string modelo = ConfigAlexa.Atual.UrlAnuncio;
            if (string.IsNullOrWhiteSpace(modelo)) return "Anúncios desligados (Alexa:UrlAnuncio vazio).";
            if (!modelo.Contains("{texto}")) return "A URL de anúncio precisa ter {texto} no lugar da frase.";
            try
            {
                using (var resposta = cliente.GetAsync(modelo.Replace("{texto}", Uri.EscapeDataString(texto))).Result)
                {
                    if (!resposta.IsSuccessStatusCode)
                        return $"O serviço de anúncio respondeu HTTP {(int)resposta.StatusCode}.";
                }
                Console.WriteLine("[Alexa] anúncio enviado: " + texto);
                return "Anúncio enviado.";
            }
            catch (Exception erro)
            {
                return "Falha ao chamar o serviço de anúncio: " + (erro.InnerException ?? erro).Message;
            }
        }

        /// <summary>O supervisor não espera o anúncio terminar</summary>
        public static void AnunciaEmSegundoPlano(string texto)
        {
            if (Ligado) Task.Run(() => Anuncia(texto));
        }
    }
}
