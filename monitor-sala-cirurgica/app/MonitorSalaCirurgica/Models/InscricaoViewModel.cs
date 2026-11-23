using System;

namespace MonitorSalaCirurgica.Models
{
    /// <summary>Inscrição (subscription) do Orion que alimenta o histórico do STH-Comet</summary>
    public class InscricaoViewModel : PadraoFiwareViewModel
    {
        public string Descricao { get; set; }
        public string SalaId { get; set; }          // device_id escolhido no cadastro
        public string EntidadeId { get; set; }
        public string EntidadeTipo { get; set; }
        public string Atributo { get; set; }        // temperature, humidity, luminosity ou "todas" no cadastro
        public string Url { get; set; }
        public string Status { get; set; } = "active";
        public int Throttling { get; set; }
        public string Formato { get; set; }

        // campos só da listagem
        public int VezesEnviada { get; set; }
        public DateTime? UltimaNotificacao { get; set; }
        public DateTime? UltimoSucesso { get; set; }
        public DateTime? UltimaFalha { get; set; }
        public string MotivoFalha { get; set; }
        public bool ComFalha => UltimaFalha.HasValue && (!UltimoSucesso.HasValue || UltimaFalha > UltimoSucesso);
    }
}
