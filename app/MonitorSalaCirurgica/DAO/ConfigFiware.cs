using System.Text.RegularExpressions;

namespace MonitorSalaCirurgica.DAO
{
    /// <summary>
    /// Configurações de acesso ao FIWARE (seção "Fiware" do appsettings.json).
    /// Faz para o FIWARE o papel que a ConexaoBD faz para o banco.
    /// No Learner Lab o IP da EC2 muda a cada sessão: troque em Host ou pela tela Conexão.
    /// </summary>
    public class ConfigFiware
    {
        /// <summary>IP público da EC2 (aceita "54.1.2.3" ou "http://54.1.2.3")</summary>
        public string Host { get; set; } = "";
        public int PortaOrion { get; set; } = 1026;
        public int PortaIotAgent { get; set; } = 4041;
        public int PortaSth { get; set; } = 8666;
        public string Service { get; set; } = "smart";       // cabeçalho fiware-service
        public string ServicePath { get; set; } = "/";       // cabeçalho fiware-servicepath

        /// <summary>
        /// URL INTERNA que o Orion usa para notificar o STH-Comet (nome do container na rede do Docker).
        /// Não use o IP público aqui: quem chama é o Orion, dentro da EC2.
        /// </summary>
        public string UrlNotificacaoSth { get; set; } = "http://sth-comet:8666/notify";

        /// <summary>Recurso do grupo de serviço no IoT Agent (requisição 2.1 da coleção)</summary>
        public string Recurso { get; set; } = "/iot/d";

        /// <summary>Porta do Mosquitto (só para exibir no cadastro da sala)</summary>
        public int PortaMqtt { get; set; } = 1883;

        /// <summary>Proxy opcional (ex.: http://proxy.empresa.local:8080)</summary>
        public string Proxy { get; set; } = "";
        public int TimeoutSegundos { get; set; } = 5;

        /// <summary>Só o endereço, sem http:// e sem barra no fim</summary>
        public string HostLimpo => Regex.Replace(Host ?? "", "^https?://", "").Trim().TrimEnd('/');

        /// <summary>"IP_PUBLICO_DA_EC2" é o texto do appsettings.example.json: ainda não configurado</summary>
        public bool Configurado => HostLimpo.Length > 0 && !HostLimpo.StartsWith("IP_");
    }
}
