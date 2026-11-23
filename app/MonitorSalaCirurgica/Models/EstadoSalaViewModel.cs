using System;

namespace MonitorSalaCirurgica.Models
{
    /// <summary>Estado atual da entidade urn:ngsi-ld:SalaCirurgica:00N no Orion</summary>
    public class EstadoSalaViewModel
    {
        public string EntityId { get; set; }
        public double? Temperatura { get; set; }
        public double? Umidade { get; set; }
        public double? Luminosidade { get; set; }
        public string Alerta { get; set; }          // atributo "alert": o que o ESP32 está mostrando
        public string StatusComando { get; set; }   // setAlert_status: PENDING, OK...
        public string InfoComando { get; set; }     // setAlert_info: "ok temp"
        public DateTime? AtualizadoEm { get; set; } // TimeInstant (horário local)

        public double? Valor(Grandeza g)
        {
            switch (g.Chave)
            {
                case "temperatura": return Temperatura;
                case "umidade": return Umidade;
                case "luminosidade": return Luminosidade;
                default: return null;
            }
        }
    }
}
