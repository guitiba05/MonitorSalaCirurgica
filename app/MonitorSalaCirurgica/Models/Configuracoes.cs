namespace MonitorSalaCirurgica.Models
{
    /// <summary>Seção "Supervisao" do appsettings.json: tempos do SupervisorService</summary>
    public class ConfigSupervisao
    {
        public static ConfigSupervisao Atual { get; } = new ConfigSupervisao();

        public bool Ligada { get; set; } = true;               // false = nenhum comando automático
        public int IntervaloSegundos { get; set; } = 10;       // ciclo de verificação
        public int ReenvioSegundos { get; set; } = 10;         // reenvia se o ESP32 não confirmar
        public int TesteSegundos { get; set; } = 15;           // duração do botão "Testar"
        public int SilencioPadraoMinutos { get; set; } = 5;
        public int SilencioMaximoMinutos { get; set; } = 30;
        public string PerfilPadrao { get; set; } = "Demonstração";
        public int JanelaPadraoSegundos { get; set; } = 30;
    }

    /// <summary>Seção "Alexa" do appsettings.json</summary>
    public class ConfigAlexa
    {
        public static ConfigAlexa Atual { get; } = new ConfigAlexa();

        /// <summary>ID da skill (amzn1.ask.skill....). Vazio = aceita qualquer skill (só para testes)</summary>
        public string SkillId { get; set; } = "";
        /// <summary>Confere a assinatura da Amazon em cada pedido</summary>
        public bool VerificarAssinatura { get; set; } = true;
        /// <summary>Sala usada quando a pessoa não diz o número</summary>
        public string SalaPadrao { get; set; } = Contrato.DeviceIdPadrao;
        /// <summary>URL de anúncio (ex.: Voice Monkey) com {texto} no lugar da frase. Vazio = desligado</summary>
        public string UrlAnuncio { get; set; } = "";
        public bool AnunciarNormalizacao { get; set; } = true;
        /// <summary>Pelo túnel (ngrok) só /api/alexa responde</summary>
        public bool SomenteSkillPeloTunel { get; set; } = true;
    }
}
