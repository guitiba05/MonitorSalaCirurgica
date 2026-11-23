namespace MonitorSalaCirurgica.Models
{
    /// <summary>
    /// Tela Salas. Junta as duas metades de uma sala: o dispositivo no IoT Agent
    /// (Id = device_id, entidade, apikey) e a configuração no banco (nome, perfil, janela, supervisão).
    /// </summary>
    public class SalaViewModel : PadraoFiwareViewModel
    {
        public string NomeEntidade { get; set; }
        public string Apikey { get; set; } = Contrato.ApikeyPadrao;
        public string Nome { get; set; }
        public string PerfilAtivo { get; set; }
        public int JanelaSegundos { get; set; } = 30;
        public bool SupervisaoAtiva { get; set; } = true;
        public bool CriarInscricoes { get; set; } = true;

        // campos só da listagem
        public string Transporte { get; set; }
        public int QuantidadeAtributos { get; set; }
        public bool TemComando { get; set; }
        public bool ExisteNoIotAgent { get; set; } = true;
        public bool ExisteNoBanco { get; set; } = true;
        public EstadoSalaViewModel Estado { get; set; }
    }
}
