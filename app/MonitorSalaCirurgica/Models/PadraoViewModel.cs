namespace MonitorSalaCirurgica.Models
{
    /// <summary>Base dos modelos gravados no SQL Server (igual ao CadAluno): chave numérica</summary>
    public abstract class PadraoViewModel
    {
        public virtual int Id { get; set; }
    }

    /// <summary>
    /// Base dos modelos gravados direto no FIWARE: a chave é texto
    /// (device_id no IoT Agent, id da inscrição no Orion).
    /// </summary>
    public abstract class PadraoFiwareViewModel
    {
        public virtual string Id { get; set; }
    }
}
