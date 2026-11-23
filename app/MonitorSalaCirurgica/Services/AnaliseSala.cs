using MonitorSalaCirurgica.DAO;
using MonitorSalaCirurgica.Models;
using System;
using System.Globalization;
using System.Linq;

namespace MonitorSalaCirurgica.Services
{
    /// <summary>
    /// Calcula a situação de uma sala: valores atuais (Orion), médias da janela (STH-Comet),
    /// comparação com os limites do perfil ativo (com histerese) e o alerta pela prioridade.
    /// O mesmo cálculo serve ao SupervisorService, ao painel e à Alexa.
    /// </summary>
    public static class AnaliseSala
    {
        private static readonly CultureInfo PtBr = new CultureInfo("pt-BR");

        /// <summary>
        /// Configuração da sala no banco. Se a sala foi cadastrada direto no IoT Agent (Postman),
        /// cria uma configuração padrão para ela entrar na supervisão.
        /// </summary>
        public static ConfiguracaoSalaViewModel ObtemOuCriaConfiguracao(SalaViewModel sala)
        {
            var dao = new ConfiguracaoSalaDAO();
            var cfg = dao.ConsultaPorDevice(sala.Id);
            if (cfg != null)
                return cfg;
            cfg = new ConfiguracaoSalaViewModel
            {
                DeviceId = sala.Id,
                Nome = sala.Nome ?? sala.Id,
                PerfilAtivo = ConfigSupervisao.Atual.PerfilPadrao,
                JanelaSegundos = ConfigSupervisao.Atual.JanelaPadraoSegundos,
                SupervisaoAtiva = true
            };
            cfg.Id = dao.Insert(cfg);
            return cfg;
        }

        /// <summary>Atalho para telas e Alexa: null se a sala não existe no IoT Agent</summary>
        public static ResumoSalaViewModel AnalisaPorDevice(string deviceId)
        {
            var sala = new SalaDAO().Consulta(deviceId);
            if (sala == null)
                return null;
            return Analisa(sala, ObtemOuCriaConfiguracao(sala), false);
        }

        /// <param name="atualizaHisterese">true só no SupervisorService: grava a nova situação de cada grandeza</param>
        public static ResumoSalaViewModel Analisa(SalaViewModel sala, ConfiguracaoSalaViewModel cfg, bool atualizaHisterese)
        {
            var est = EstadoSupervisao.Obtem(sala.Id);
            var limites = new LimiteDAO().ListagemPorPerfil(cfg.PerfilAtivo);
            var estado = new EntidadeSalaDAO().ConsultaEstado(sala.NomeEntidade);
            var mensagens = new MensagemIoTDAO();

            var resumo = new ResumoSalaViewModel
            {
                DeviceId = sala.Id,
                Nome = cfg.Nome,
                EntityId = sala.NomeEntidade,
                Perfil = cfg.PerfilAtivo,
                JanelaSegundos = cfg.JanelaSegundos,
                SupervisaoAtiva = cfg.SupervisaoAtiva,
                Estado = estado
            };

            string erroSth = null;
            foreach (var g in Contrato.GrandezasExibicao)
            {
                // média da janela no STH-Comet; sem pontos (ou STH fora), usa o valor atual do Orion
                double? media = null;
                int pontos = 0;
                try { media = mensagens.MediaJanela(sala.NomeEntidade, g, cfg.JanelaSegundos, out pontos); }
                catch (Exception erro) when (erro is ErroFiwareException || erro is FiwareIndisponivelException)
                {
                    erroSth = erroSth ?? erro.Message;
                }

                var limite = limites.FirstOrDefault(l => l.Grandeza == g.Chave);
                double? atual = estado?.Valor(g);
                var av = new AvaliacaoGrandeza
                {
                    Grandeza = g.Chave,
                    Nome = g.Nome,
                    Unidade = g.Unidade,
                    Atual = atual,
                    Media = media ?? atual,
                    Pontos = pontos,
                    MediaDoOrion = media == null && atual != null,
                    Minimo = limite?.Minimo,
                    Maximo = limite?.Maximo,
                    Histerese = limite?.Histerese
                };
                lock (est)
                {
                    string anterior;
                    est.Situacoes.TryGetValue(g.Chave, out anterior);
                    av.Situacao = Avalia(av.Media, limite, anterior);
                    if (atualizaHisterese)
                        est.Situacoes[g.Chave] = av.Situacao;
                }
                resumo.Grandezas.Add(av);
            }

            // prioridade do contrato: luz (1ª) > temp (2ª) > umid (3ª)
            var vencedora = Contrato.Grandezas.FirstOrDefault(g => resumo.Grandeza(g.Chave)?.EmAnomalia == true);
            resumo.AlertaCalculado = vencedora?.Alerta ?? "off";
            resumo.Motivo = DescreveMotivo(resumo);

            lock (est)
            {
                resumo.UltimoEnviado = est.UltimoEnviado;
                resumo.Confirmado = est.Confirmado;
                resumo.AlertaDesde = est.AlertaDesde;
                resumo.UltimaVerificacao = est.UltimaVerificacao;
                resumo.Intervencao = est.Intervencao != null && est.Intervencao.Valida(DateTime.Now) ? est.Intervencao : null;
                resumo.Erro = erroSth != null ? "Histórico indisponível, usando o valor atual do Orion: " + erroSth : est.UltimoErro;
            }
            return resumo;
        }

        /// <summary>
        /// Compara a média com os limites. Histerese: para ENTRAR em alerta basta passar do limite;
        /// para SAIR, a média precisa voltar uma margem para dentro da faixa.
        /// Ex.: máx 28 °C e histerese 0,5 → alarma acima de 28,0 e só desliga em 27,5 ou menos.
        /// </summary>
        public static string Avalia(double? media, LimiteViewModel limite, string anterior)
        {
            if (media == null) return "sem-dados";
            if (limite?.Minimo == null || limite.Maximo == null) return "sem-limite";
            double min = limite.Minimo.Value, max = limite.Maximo.Value, h = limite.Histerese ?? 0, m = media.Value;

            if (m > max) return "acima";
            if (m < min) return "abaixo";
            if (anterior == "acima" && m > max - h) return "acima";     // ainda não voltou o suficiente
            if (anterior == "abaixo" && m < min + h) return "abaixo";
            return "normal";
        }

        /// <summary>Texto para o AlertaLog: "temperatura média 29,1 °C acima do máximo 28 (6 pontos)"</summary>
        public static string DescreveMotivo(ResumoSalaViewModel r)
        {
            var fora = Contrato.Grandezas.Select(g => r.Grandeza(g.Chave)).Where(a => a != null && a.EmAnomalia).ToList();
            if (fora.Count == 0)
                return "Todas as grandezas dentro dos limites";
            return string.Join("; ", fora.Select(f =>
                $"{f.Grandeza} média {Numero(f.Media)} {f.Unidade} " +
                (f.Situacao == "acima" ? $"acima do máximo {Numero(f.Maximo)}" : $"abaixo do mínimo {Numero(f.Minimo)}") +
                (f.MediaDoOrion ? " (valor atual do Orion)" : $" ({f.Pontos} pontos)")));
        }

        public static string Numero(double? v) => v.HasValue ? v.Value.ToString("0.#", PtBr) : "—";
    }
}
