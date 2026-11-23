using Microsoft.AspNetCore.Mvc;
using MonitorSalaCirurgica.Models;
using MonitorSalaCirurgica.Services.Alexa;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;

namespace MonitorSalaCirurgica.Controllers
{
    /// <summary>
    /// Endpoint HTTPS da skill (configurado no Alexa Developer Console):
    ///   POST https://SEU-DOMINIO.ngrok-free.app/api/alexa
    /// Não exige login: a proteção é a verificação da Amazon (assinatura, horário e ID da skill).
    /// </summary>
    [Route("api/alexa")]
    public class AlexaApiController : Controller
    {
        private const int ToleranciaSegundos = 150;   // exigência da Amazon

        /// <summary>Para testar o túnel no navegador</summary>
        [HttpGet]
        public IActionResult Get()
        {
            return Content("Endpoint da skill Monitor de Sala Cirúrgica ativo. A Alexa usa POST neste endereço.", "text/plain; charset=utf-8");
        }

        [HttpPost]
        public IActionResult Post()
        {
            // a assinatura é calculada sobre os bytes EXATOS do corpo: lê sem converter
            byte[] corpo;
            using (var memoria = new MemoryStream())
            {
                Request.Body.CopyToAsync(memoria).Wait();
                corpo = memoria.ToArray();
            }
            var op = ConfigAlexa.Atual;

            if (op.VerificarAssinatura)
            {
                string erro = AlexaVerificador.Verifica(Request.Headers["SignatureCertChainUrl"], Request.Headers["Signature-256"], corpo);
                if (erro != null) return Recusa(erro);
            }

            JObject envelope;
            try { envelope = JObject.Parse(System.Text.Encoding.UTF8.GetString(corpo)); }
            catch (JsonException) { return Recusa("Corpo não é JSON."); }
            if (envelope["request"] == null) return Recusa("Pedido sem o campo request.");

            if (op.VerificarAssinatura && !HorarioValido((string)envelope.SelectToken("request.timestamp")))
                return Recusa("Pedido fora da janela de 150 s (timestamp).");

            string idSkill = (string)(envelope.SelectToken("context.System.application.applicationId") ??
                                      envelope.SelectToken("session.application.applicationId"));
            if (!string.IsNullOrWhiteSpace(op.SkillId) && idSkill != op.SkillId)
                return Recusa($"Pedido de outra skill ({idSkill}).");

            var resposta = new AlexaSkill().Processa(envelope);
            return Content(resposta.ToString(Formatting.None), "application/json; charset=utf-8");
        }

        private IActionResult Recusa(string motivo)
        {
            Console.WriteLine("[Alexa] pedido recusado: " + motivo);
            return BadRequest(new { erro = motivo });
        }

        private static bool HorarioValido(string timestamp)
        {
            DateTime quando;
            return DateTime.TryParse(timestamp, CultureInfo.InvariantCulture,
                       DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out quando) &&
                   Math.Abs((DateTime.UtcNow - quando).TotalSeconds) <= ToleranciaSegundos;
        }
    }
}
