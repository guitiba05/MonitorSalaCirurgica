using MonitorSalaCirurgica.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace MonitorSalaCirurgica.Services
{
    /// <summary>
    /// Memória do supervisor para UMA sala: o que já foi enviado, se o ESP32 confirmou,
    /// a situação de cada grandeza (para a histerese) e a intervenção manual em vigor.
    /// </summary>
    public class EstadoSala
    {
        public string DeviceId { get; set; }
        public Dictionary<string, string> Situacoes { get; } = new Dictionary<string, string>();
        public string UltimoEnviado { get; set; }      // null = nada enviado desde que a aplicação subiu
        public DateTime? UltimoEnvio { get; set; }
        public int? UltimoLogId { get; set; }
        public bool Confirmado { get; set; }
        public int Reenvios { get; set; }
        public DateTime? AlertaDesde { get; set; }
        public Intervencao Intervencao { get; set; }
        public DateTime? UltimaVerificacao { get; set; }
        public string UltimoErro { get; set; }
        public string UltimoAnuncio { get; set; }
    }

    /// <summary>
    /// Estado compartilhado entre o SupervisorService (segundo plano) e as telas, a Alexa
    /// e os botões de teste.
    /// </summary>
    public static class EstadoSupervisao
    {
        private static readonly ConcurrentDictionary<string, EstadoSala> salas = new ConcurrentDictionary<string, EstadoSala>();
        private static readonly AutoResetEvent sinal = new AutoResetEvent(false);

        public static DateTime? UltimoCiclo { get; set; }
        public static string ErroGeral { get; set; }

        public static EstadoSala Obtem(string deviceId) =>
            salas.GetOrAdd(deviceId, id => new EstadoSala { DeviceId = id });

        public static void Remove(string deviceId)
        {
            EstadoSala removido;
            salas.TryRemove(deviceId, out removido);
        }

        /// <summary>Faz o supervisor rodar um ciclo agora, sem esperar os 10 s</summary>
        public static void Acorda() => sinal.Set();

        /// <summary>Espera o intervalo do ciclo, um Acorda() ou o fim da aplicação</summary>
        public static void Aguarda(TimeSpan intervalo, CancellationToken token) =>
            WaitHandle.WaitAny(new[] { sinal, token.WaitHandle }, intervalo);
    }
}
