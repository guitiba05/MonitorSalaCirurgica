namespace MonitorSalaCirurgica.Models
{
    /// <summary>Resultado do health check de um componente do FIWARE (requisições 0.1 a 0.3)</summary>
    public class StatusComponenteViewModel
    {
        public string Nome { get; set; }
        public string Url { get; set; }
        public bool Ok { get; set; }
        public string Versao { get; set; }
        public long Milissegundos { get; set; }
        public string Erro { get; set; }
    }
}
