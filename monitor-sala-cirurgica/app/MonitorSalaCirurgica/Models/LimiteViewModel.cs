namespace MonitorSalaCirurgica.Models
{
    /// <summary>Tabela Limite: faixa aceitável de uma grandeza dentro de um perfil</summary>
    public class LimiteViewModel : PadraoViewModel
    {
        public string Perfil { get; set; }
        public string Grandeza { get; set; }
        public double? Minimo { get; set; }
        public double? Maximo { get; set; }
        /// <summary>Margem para SAIR do alerta (evita o alarme ligar e desligar perto do limite)</summary>
        public double? Histerese { get; set; }

        // campo que não existe na tabela
        public Grandeza Info => Contrato.GrandezaPorChave(Grandeza);
    }
}
