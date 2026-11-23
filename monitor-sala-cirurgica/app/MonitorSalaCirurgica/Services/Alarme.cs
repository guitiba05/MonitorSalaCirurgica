using MonitorSalaCirurgica.DAO;
using MonitorSalaCirurgica.Models;
using System;

namespace MonitorSalaCirurgica.Services
{
    /// <summary>
    /// Único lugar que envia o comando setAlert: usado pelo SupervisorService (automático),
    /// pelos botões de teste, pelo painel (silenciar) e pela Alexa. Cada envio vai para o AlertaLog.
    /// </summary>
    public static class Alarme
    {
        private static readonly object umEnvioPorVez = new object();

        /// <summary>PATCH setAlert no Orion + registro "Enviado" no AlertaLog</summary>
        public static void Envia(SalaViewModel sala, string alerta, string origem, string motivo)
        {
            var est = EstadoSupervisao.Obtem(sala.Id);
            lock (umEnvioPorVez)
            {
                var registro = new AlertaLogViewModel
                {
                    DataHora = DateTime.Now, DeviceId = sala.Id, Alerta = alerta, Origem = origem, Motivo = motivo
                };
                try
                {
                    new EntidadeSalaDAO().EnviaComandoAlerta(sala.NomeEntidade, alerta);
                    registro.Status = StatusAlerta.Enviado;
                }
                catch (Exception erro)
                {
                    registro.Status = StatusAlerta.Falha;
                    registro.Motivo = motivo + " | " + erro.Message;
                    GravaLog(registro);
                    throw;
                }

                int? id = GravaLog(registro);
                lock (est)
                {
                    if (est.UltimoEnviado != alerta)
                        est.AlertaDesde = alerta == "off" ? (DateTime?)null : DateTime.Now;
                    est.UltimoEnviado = alerta;
                    est.UltimoEnvio = DateTime.Now;
                    est.UltimoLogId = id;
                    est.Confirmado = false;
                    est.Reenvios = 0;
                }
                Console.WriteLine($"[Alarme] setAlert {alerta} -> {sala.Id} ({origem}): {motivo}");
            }
        }

        /// <summary>O ESP32 não confirmou (atributo "alert" diferente): manda o mesmo comando de novo</summary>
        public static void Reenvia(SalaViewModel sala)
        {
            var est = EstadoSupervisao.Obtem(sala.Id);
            string alerta; int? logId; int vezes;
            lock (est)
            {
                if (est.UltimoEnviado == null) return;
                alerta = est.UltimoEnviado;
                logId = est.UltimoLogId;
                vezes = ++est.Reenvios;
                est.UltimoEnvio = DateTime.Now;
            }
            new EntidadeSalaDAO().EnviaComandoAlerta(sala.NomeEntidade, alerta);
            if (logId.HasValue) AtualizaLog(logId.Value, StatusAlerta.Reenviado(vezes));
            Console.WriteLine($"[Alarme] setAlert {alerta} reenviado para {sala.Id} ({vezes}x): o ESP32 ainda não confirmou");
        }

        /// <summary>O atributo "alert" do Orion ficou igual ao enviado: o ESP32 está mostrando o alerta certo</summary>
        public static void MarcaConfirmado(string deviceId)
        {
            var est = EstadoSupervisao.Obtem(deviceId);
            int? logId;
            lock (est)
            {
                if (est.Confirmado) return;
                est.Confirmado = true;
                logId = est.UltimoLogId;
            }
            if (logId.HasValue) AtualizaLog(logId.Value, StatusAlerta.Confirmado);
        }

        // ------------------------------------------------------------------
        //  Intervenções manuais: valem por um tempo e passam na frente do cálculo
        // ------------------------------------------------------------------

        /// <summary>Liga um alerta por alguns segundos (botão da tela Salas ou Alexa)</summary>
        public static void Testa(SalaViewModel sala, string alerta, string origem)
        {
            int segundos = ConfigSupervisao.Atual.TesteSegundos;
            string descricao = alerta == "off" ? "Teste: alarme desligado" : $"Teste do alerta {alerta} por {segundos} s";
            EstadoSupervisao.Obtem(sala.Id).Intervencao = new Intervencao(alerta, DateTime.Now.AddSeconds(segundos), origem, descricao);
            Envia(sala, alerta, origem, descricao);
        }

        /// <summary>Desliga LED e buzzer por N minutos; depois a supervisão volta sozinha</summary>
        public static int Silencia(SalaViewModel sala, int minutos, string quem)
        {
            minutos = Math.Max(1, Math.Min(minutos, ConfigSupervisao.Atual.SilencioMaximoMinutos));
            string descricao = $"Alarme silenciado por {minutos} min ({quem})";
            EstadoSupervisao.Obtem(sala.Id).Intervencao =
                new Intervencao("off", DateTime.Now.AddMinutes(minutos), OrigemAlerta.Silencio, descricao);
            Envia(sala, "off", OrigemAlerta.Silencio, descricao);
            return minutos;
        }

        /// <summary>Encerra o teste ou o silêncio; o supervisor recalcula na hora</summary>
        public static void Reativa(string deviceId)
        {
            EstadoSupervisao.Obtem(deviceId).Intervencao = null;
            EstadoSupervisao.Acorda();
        }

        // o AlertaLog é registro: se o banco falhar, o alarme continua funcionando
        private static int? GravaLog(AlertaLogViewModel registro)
        {
            try { return new AlertaLogDAO().Insert(registro); }
            catch (Exception erro) { Console.WriteLine("[Alarme] não foi possível gravar o AlertaLog: " + erro.Message); return null; }
        }

        private static void AtualizaLog(int id, string status)
        {
            try { new AlertaLogDAO().AtualizaStatus(id, status); }
            catch (Exception erro) { Console.WriteLine("[Alarme] não foi possível atualizar o AlertaLog: " + erro.Message); }
        }
    }
}
