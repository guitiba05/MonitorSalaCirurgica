using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace MonitorSalaCirurgica.Services.Alexa
{
    /// <summary>
    /// Confere se o pedido veio mesmo da Alexa (exigência da Amazon para skills com endpoint HTTPS):
    ///   1. a URL do certificado (cabeçalho SignatureCertChainUrl) é https://s3.amazonaws.com/echo.api/...
    ///   2. o certificado está na validade, é de echo-api.amazon.com e forma cadeia até uma CA confiável;
    ///   3. a assinatura (cabeçalho Signature-256) do corpo bruto confere com a chave pública.
    /// O horário (±150 s) e o ID da skill são conferidos no AlexaApiController.
    /// </summary>
    public static class AlexaVerificador
    {
        private static readonly HttpClient cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        private static readonly ConcurrentDictionary<string, X509Certificate2> cache = new ConcurrentDictionary<string, X509Certificate2>();

        /// <summary>Devolve null se o pedido é válido; senão, o motivo</summary>
        public static string Verifica(string urlCadeia, string assinatura256, byte[] corpo)
        {
            if (string.IsNullOrWhiteSpace(urlCadeia) || string.IsNullOrWhiteSpace(assinatura256))
                return "Cabeçalhos SignatureCertChainUrl e Signature-256 ausentes.";
            if (!UrlCadeiaValida(urlCadeia))
                return "SignatureCertChainUrl fora do endereço da Amazon.";

            var certificado = ObtemCertificado(urlCadeia);
            if (certificado == null)
                return "Certificado da Amazon inválido.";

            byte[] assinatura;
            try { assinatura = Convert.FromBase64String(assinatura256); }
            catch (FormatException) { return "Signature-256 mal formada."; }

            using (var rsa = certificado.GetRSAPublicKey())
            {
                if (rsa == null || !rsa.VerifyData(corpo, assinatura, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                    return "A assinatura não confere com o corpo do pedido.";
            }
            return null;
        }

        /// <summary>Regras da Amazon para a URL do certificado (o caminho diferencia maiúsculas)</summary>
        public static bool UrlCadeiaValida(string url)
        {
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u)) return false;
            return u.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
                && u.Host.Equals("s3.amazonaws.com", StringComparison.OrdinalIgnoreCase)
                && u.Port == 443
                && u.AbsolutePath.StartsWith("/echo.api/", StringComparison.Ordinal);   // já normalizado (sem ../)
        }

        private static X509Certificate2 ObtemCertificado(string url)
        {
            X509Certificate2 guardado;
            if (cache.TryGetValue(url, out guardado) && DateTime.Now < guardado.NotAfter)
                return guardado;

            string pem;
            try { pem = cliente.GetStringAsync(url).Result; }
            catch (Exception) { return null; }

            // o arquivo traz o certificado da Amazon seguido dos intermediários, em PEM
            var certificados = Regex.Matches(pem, "-----BEGIN CERTIFICATE-----(.*?)-----END CERTIFICATE-----", RegexOptions.Singleline)
                .Cast<Match>()
                .Select(m => new X509Certificate2(Convert.FromBase64String(Regex.Replace(m.Groups[1].Value, @"\s", ""))))
                .ToList();
            if (certificados.Count == 0) return null;
            var folha = certificados[0];

            if (DateTime.Now < folha.NotBefore || DateTime.Now > folha.NotAfter) return null;

            // Subject Alternative Name (OID 2.5.29.17) precisa ter echo-api.amazon.com
            var san = folha.Extensions.Cast<X509Extension>().FirstOrDefault(e => e.Oid.Value == "2.5.29.17");
            if (san == null || !san.Format(false).Contains("echo-api.amazon.com")) return null;

            using (var cadeia = new X509Chain())
            {
                cadeia.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                foreach (var intermediario in certificados.Skip(1))
                    cadeia.ChainPolicy.ExtraStore.Add(intermediario);
                if (!cadeia.Build(folha)) return null;
            }
            cache[url] = folha;
            return folha;
        }
    }
}
