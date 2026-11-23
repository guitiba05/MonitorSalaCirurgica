namespace MonitorSalaCirurgica.Models
{
    /// <summary>Tabela ConfiguracaoSala: o que a aplicação sabe de cada sala</summary>
    public class ConfiguracaoSalaViewModel : PadraoViewModel
    {
        public string DeviceId { get; set; }
        public string Nome { get; set; }
        public string PerfilAtivo { get; set; }
        public int JanelaSegundos { get; set; } = 30;
        public bool SupervisaoAtiva { get; set; } = true;
    }
}
