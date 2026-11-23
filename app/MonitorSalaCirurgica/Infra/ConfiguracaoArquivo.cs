using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace MonitorSalaCirurgica.Infra
{
    /// <summary>
    /// Grava o novo IP da EC2 no appsettings.json (tela Conexão). Se o appsettings.json não
    /// existir, ele é criado a partir do appsettings.example.json.
    /// </summary>
    public static class ConfiguracaoArquivo
    {
        public static string PastaDoProjeto { get; set; } = Directory.GetCurrentDirectory();

        public static void GravaHostFiware(string host)
        {
            string arquivo = Path.Combine(PastaDoProjeto, "appsettings.json");
            string exemplo = Path.Combine(PastaDoProjeto, "appsettings.example.json");
            if (!File.Exists(arquivo) && File.Exists(exemplo))
                File.Copy(exemplo, arquivo);

            JObject raiz = File.Exists(arquivo) ? JObject.Parse(File.ReadAllText(arquivo)) : new JObject();
            if (!(raiz["Fiware"] is JObject fiware))
                raiz["Fiware"] = fiware = new JObject();
            fiware["Host"] = host;
            File.WriteAllText(arquivo, raiz.ToString(Formatting.Indented));
        }
    }
}
