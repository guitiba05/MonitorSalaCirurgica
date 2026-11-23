using System;
using System.Collections.Generic;
using System.Linq;

namespace MonitorSalaCirurgica.Models
{
    /// <summary>Situação de uma grandeza: valor atual, média da janela e comparação com os limites</summary>
    public class AvaliacaoGrandeza
    {
        public string Grandeza { get; set; }
        public string Nome { get; set; }
        public string Unidade { get; set; }
        public double? Atual { get; set; }        // valor do Orion
        public double? Media { get; set; }        // média da janela (STH-Comet)
        public int Pontos { get; set; }           // quantas medidas entraram na média
        public bool MediaDoOrion { get; set; }    // true = sem pontos na janela, usou o valor atual
        public double? Minimo { get; set; }
        public double? Maximo { get; set; }
        public double? Histerese { get; set; }
        /// <summary>normal, acima, abaixo, sem-dados ou sem-limite</summary>
        public string Situacao { get; set; } = "sem-dados";
        public bool EmAnomalia => Situacao == "acima" || Situacao == "abaixo";
    }

    /// <summary>
    /// Ação manual que vale por um tempo e passa na frente do cálculo automático:
    /// teste de alerta (botão ou Alexa) ou silêncio do alarme.
    /// </summary>
    public class Intervencao
    {
        public Intervencao(string alerta, DateTime ate, string origem, string descricao)
        {
            Alerta = alerta; Ate = ate; Origem = origem; Descricao = descricao;
        }

        public string Alerta { get; }
        public DateTime Ate { get; }
        public string Origem { get; }
        public string Descricao { get; }
        public bool Valida(DateTime agora) => agora < Ate;
    }

    /// <summary>Tudo o que o painel, a Alexa e o supervisor precisam saber de uma sala</summary>
    public class ResumoSalaViewModel
    {
        public string DeviceId { get; set; }
        public string Nome { get; set; }
        public string EntityId { get; set; }
        public string EntityType { get; set; } = Contrato.TipoEntidade;
        public string Perfil { get; set; }
        public int JanelaSegundos { get; set; }
        public bool SupervisaoAtiva { get; set; }
        public EstadoSalaViewModel Estado { get; set; }
        public List<AvaliacaoGrandeza> Grandezas { get; set; } = new List<AvaliacaoGrandeza>();

        /// <summary>Alerta que vale pela prioridade (o que a supervisão manda)</summary>
        public string AlertaCalculado { get; set; } = "off";
        public string Motivo { get; set; }

        // vindos do estado em memória do supervisor
        public string UltimoEnviado { get; set; }
        public bool Confirmado { get; set; }
        public DateTime? AlertaDesde { get; set; }
        public DateTime? UltimaVerificacao { get; set; }
        public Intervencao Intervencao { get; set; }
        public string Erro { get; set; }

        public AvaliacaoGrandeza Grandeza(string chave) => Grandezas.FirstOrDefault(g => g.Grandeza == chave);
    }
}
