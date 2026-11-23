using Microsoft.Extensions.Hosting;
using MonitorSalaCirurgica.DAO;
using MonitorSalaCirurgica.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MonitorSalaCirurgica.Services
{
    /// <summary>
    /// Supervisão automática (critério H5). Roda em segundo plano enquanto a aplicação estiver no ar.
    /// A cada ciclo (10 s), para cada sala cadastrada no IoT Agent:
    ///   1. calcula a média da janela de cada grandeza no STH-Comet (sem pontos: valor do Orion);
    ///   2. compara com os limites do perfil ativo, com histerese;
    ///   3. escolhe o alerta pela prioridade (luz > temp > umid) ou "off" se tudo está normal;
    ///   4. envia setAlert SÓ quando o alerta muda;
    ///   5. confere a confirmação: se o atributo "alert" do Orion não ficar igual em 10 s, reenvia;
    ///   6. registra tudo no AlertaLog.
    /// Testes e silêncio (intervenções) passam na frente do cálculo enquanto valem.
    /// </summary>
    public class SupervisorService : BackgroundService
    {
        // o BackgroundService exige um Task; o trabalho em si é síncrono, como o resto do projeto
        protected override Task ExecuteAsync(CancellationToken token) => Task.Run(() => Executa(token), token);

        private void Executa(CancellationToken token)
        {
            Console.WriteLine("[Supervisão] iniciada");
            Thread.Sleep(3000);   // deixa a aplicação terminar de subir
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (ConfigSupervisao.Atual.Ligada)
                        Ciclo();
                    else
                        EstadoSupervisao.ErroGeral = "Supervisão desligada em Supervisao:Ligada (appsettings.json).";
                }
                catch (Exception erro)
                {
                    EstadoSupervisao.ErroGeral = erro.Message;
                    Console.WriteLine("[Supervisão] ciclo falhou: " + erro.Message);
                }
                EstadoSupervisao.Aguarda(TimeSpan.FromSeconds(Math.Max(2, ConfigSupervisao.Atual.IntervaloSegundos)), token);
            }
        }

        private void Ciclo()
        {
            if (!HelperFiware.Config.Configurado)
            {
                EstadoSupervisao.ErroGeral = "IP do FIWARE não configurado (Fiware:Host).";
                return;
            }
            foreach (var sala in new SalaDAO().ListagemDoContrato())
            {
                var est = EstadoSupervisao.Obtem(sala.Id);
                try
                {
                    Supervisiona(sala, est);
                    est.UltimoErro = null;
                }
                catch (Exception erro)
                {
                    est.UltimoErro = erro.Message;
                    Console.WriteLine($"[Supervisão] {sala.Id}: {erro.Message}");
                }
                est.UltimaVerificacao = DateTime.Now;
            }
            EstadoSupervisao.UltimoCiclo = DateTime.Now;
            EstadoSupervisao.ErroGeral = null;
        }

        private void Supervisiona(SalaViewModel sala, EstadoSala est)
        {
            var cfg = AnaliseSala.ObtemOuCriaConfiguracao(sala);
            var resumo = AnaliseSala.Analisa(sala, cfg, true);
            DateTime agora = DateTime.Now;

            // ---------- 1. O que a sala deveria estar mostrando ----------
            string desejado, origem, motivo;
            var intervencao = est.Intervencao;
            if (intervencao != null && intervencao.Valida(agora))
            {
                desejado = intervencao.Alerta; origem = intervencao.Origem; motivo = intervencao.Descricao;
            }
            else
            {
                if (intervencao != null) est.Intervencao = null;      // venceu: volta ao automático
                origem = OrigemAlerta.Supervisao;
                if (cfg.SupervisaoAtiva) { desejado = resumo.AlertaCalculado; motivo = resumo.Motivo; }
                else { desejado = "off"; motivo = "Supervisão desligada para esta sala"; }
            }

            // ---------- 2. Enviar, reenviar ou confirmar ----------
            string mostrado = resumo.Estado?.Alerta;    // o que o ESP32 publicou por último (atributo "alert")
            string ultimoEnviado; DateTime? ultimoEnvio;
            lock (est) { ultimoEnviado = est.UltimoEnviado; ultimoEnvio = est.UltimoEnvio; }

            if (ultimoEnviado == null && mostrado == desejado)
            {
                // a aplicação acabou de subir e a placa já mostra o alerta certo: nada a enviar
                lock (est)
                {
                    est.UltimoEnviado = desejado; est.Confirmado = true;
                    est.AlertaDesde = desejado == "off" ? (DateTime?)null : agora;
                }
            }
            else if (desejado != ultimoEnviado)
            {
                // o alerta mudou: envia (só aqui um comando novo é registrado)
                Alarme.Envia(sala, desejado, origem, motivo);
                if (origem == OrigemAlerta.Supervisao)
                    Anuncia(cfg.Nome, desejado, resumo, est);
            }
            else if (mostrado != desejado)
            {
                // enviado, mas o ESP32 ainda não mostra: reenvia a cada ReenvioSegundos
                if (ultimoEnvio == null || (agora - ultimoEnvio.Value).TotalSeconds >= ConfigSupervisao.Atual.ReenvioSegundos)
                    Alarme.Reenvia(sala);
            }
            else
                Alarme.MarcaConfirmado(sala.Id);
        }

        // anúncio por voz no Echo (opcional): só para mudanças decididas pela supervisão
        private void Anuncia(string nomeSala, string alerta, ResumoSalaViewModel resumo, EstadoSala est)
        {
            if (est.UltimoAnuncio == alerta) return;
            est.UltimoAnuncio = alerta;
            if (alerta == "off" && !ConfigAlexa.Atual.AnunciarNormalizacao) return;
            string texto = alerta == "off" ? $"{nomeSala} voltou ao normal." : "Atenção: " + Falas.DescreveAlerta(resumo, alerta);
            AnuncioAlexa.AnunciaEmSegundoPlano(texto);
        }
    }
}
