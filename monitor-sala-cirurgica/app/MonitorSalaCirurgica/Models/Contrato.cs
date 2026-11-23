using System;
using System.Collections.Generic;
using System.Linq;

namespace MonitorSalaCirurgica.Models
{
    /// <summary>
    /// Valores FIXOS combinados com o Analista 2 (seção "Contrato" do Guia dos desenvolvedores).
    /// Firmware, cadastro no FIWARE e aplicação dependem deles ao mesmo tempo.
    /// </summary>
    public static class Contrato
    {
        public const string ApikeyPadrao = "CIRURGIA01";
        public const string DeviceIdPadrao = "sala01";
        public const string TipoEntidade = "SalaCirurgica";
        public const string PrefixoEntidade = "urn:ngsi-ld:SalaCirurgica:";
        public const string Comando = "setAlert";
        public const string AtributoAlerta = "alert";      // "a" no UltraLight

        /// <summary>
        /// As três grandezas na ORDEM DE PRIORIDADE do alarme (a primeira vale mais).
        /// Com mais de uma fora do limite, a aplicação manda só o alerta de maior prioridade.
        /// </summary>
        public static readonly List<Grandeza> Grandezas = new List<Grandeza>
        {
            //           chave           atributo      UL   tipo NGSI  unid. alerta  nome            fala         prioridade
            new Grandeza("luminosidade", "luminosity",  "l", "Integer", "%",  "luz",  "Luminosidade", "por cento", 1),
            new Grandeza("temperatura",  "temperature", "t", "Number",  "°C", "temp", "Temperatura",  "graus",     2),
            new Grandeza("umidade",      "humidity",    "h", "Number",  "%",  "umid", "Umidade",      "por cento", 3),
        };

        /// <summary>Ordem natural para as telas: temperatura, umidade, luminosidade</summary>
        public static readonly List<Grandeza> GrandezasExibicao =
            Grandezas.OrderBy(g => g.ObjectId == "t" ? 0 : g.ObjectId == "h" ? 1 : 2).ToList();

        /// <summary>Padrões de alerta: iguais no firmware (config.h), no README e nas telas</summary>
        public static readonly List<PadraoAlerta> Padroes = new List<PadraoAlerta>
        {
            new PadraoAlerta("luz",  1, "150 ms aceso, 150 ms apagado", 150,  2000, "luminosidade"),
            new PadraoAlerta("temp", 2, "400 ms aceso, 400 ms apagado", 400,  1000, "temperatura"),
            new PadraoAlerta("umid", 3, "1 s aceso, 1 s apagado",       1000, 500,  "umidade"),
            new PadraoAlerta("off",  0, "Apagado",                      0,    0,    null),
        };

        public static Grandeza GrandezaPorChave(string chave) =>
            Grandezas.FirstOrDefault(g => string.Equals(g.Chave, chave, StringComparison.OrdinalIgnoreCase));

        public static Grandeza GrandezaPorAtributo(string atributo) =>
            Grandezas.FirstOrDefault(g => string.Equals(g.Atributo, atributo, StringComparison.OrdinalIgnoreCase));

        public static Grandeza GrandezaPorAlerta(string alerta) =>
            Grandezas.FirstOrDefault(g => string.Equals(g.Alerta, alerta, StringComparison.OrdinalIgnoreCase));

        public static PadraoAlerta Padrao(string alerta) =>
            Padroes.FirstOrDefault(p => p.Alerta == alerta) ?? Padroes.Last();

        public static bool AlertaValido(string alerta) => Padroes.Any(p => p.Alerta == alerta);

        /// <summary>"temp" → "temperatura"; "off" → "normal"</summary>
        public static string NomeAlerta(string alerta)
        {
            var g = GrandezaPorAlerta(alerta);
            if (g != null) return g.Chave;
            return alerta == "off" ? "normal" : alerta ?? "desconhecido";
        }
    }

    /// <summary>Uma grandeza medida pelo ESP32</summary>
    public class Grandeza
    {
        public Grandeza(string chave, string atributo, string objectId, string tipoNgsi, string unidade,
                        string alerta, string nome, string unidadeFalada, int prioridade)
        {
            Chave = chave; Atributo = atributo; ObjectId = objectId; TipoNgsi = tipoNgsi; Unidade = unidade;
            Alerta = alerta; Nome = nome; UnidadeFalada = unidadeFalada; Prioridade = prioridade;
        }

        public string Chave { get; }          // nome no banco (Limite.Grandeza) e nas telas
        public string Atributo { get; }       // atributo da entidade no Orion e no STH-Comet
        public string ObjectId { get; }       // letra no UltraLight 2.0 (t|23.5|h|58|l|42)
        public string TipoNgsi { get; }
        public string Unidade { get; }
        public string Alerta { get; }         // valor do comando setAlert desta grandeza
        public string Nome { get; }
        public string UnidadeFalada { get; }  // usada pela Alexa
        public int Prioridade { get; }        // 1 = maior prioridade
    }

    /// <summary>Como o LED azul e o buzzer se comportam em cada alerta</summary>
    public class PadraoAlerta
    {
        public PadraoAlerta(string alerta, int prioridade, string led, int meioCicloMs, int buzzerHz, string grandeza)
        {
            Alerta = alerta; Prioridade = prioridade; Led = led; MeioCicloMs = meioCicloMs; BuzzerHz = buzzerHz; Grandeza = grandeza;
        }

        public string Alerta { get; }
        public int Prioridade { get; }
        public string Led { get; }
        public int MeioCicloMs { get; }
        public int BuzzerHz { get; }
        public string Grandeza { get; }
    }
}
