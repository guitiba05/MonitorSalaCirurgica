using System;

namespace MonitorSalaCirurgica.Models
{
    /// <summary>Tabela AlertaLog: um comando setAlert e o que aconteceu com ele</summary>
    public class AlertaLogViewModel : PadraoViewModel
    {
        public DateTime DataHora { get; set; } = DateTime.Now;
        public string DeviceId { get; set; }
        public string Alerta { get; set; } = "off";
        public string Origem { get; set; }      // Supervisão, Teste, Alexa, Silêncio
        public string Motivo { get; set; }
        public string Status { get; set; }      // Enviado, Confirmado, Reenviado 2x, Falha

        public PadraoAlerta Padrao => Contrato.Padrao(Alerta);
    }

    public static class OrigemAlerta
    {
        public const string Supervisao = "Supervisão";
        public const string Teste = "Teste";
        public const string Alexa = "Alexa";
        public const string Silencio = "Silêncio";
        public static readonly string[] Todas = { Supervisao, Teste, Alexa, Silencio };
    }

    public static class StatusAlerta
    {
        public const string Enviado = "Enviado";
        public const string Confirmado = "Confirmado";
        public const string Falha = "Falha";
        public static string Reenviado(int vezes) => $"Reenviado {vezes}x";
    }
}
