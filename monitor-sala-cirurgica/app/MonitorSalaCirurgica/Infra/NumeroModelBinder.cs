using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace MonitorSalaCirurgica.Infra
{
    /// <summary>
    /// Aceita "28,5" e "28.5" nos campos double e decimal. Sem isso, com a cultura pt-BR,
    /// o ponto é lido como separador de milhar e "28.5" viraria 285.
    /// </summary>
    public class NumeroModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext contexto)
        {
            string valor = contexto.ValueProvider.GetValue(contexto.ModelName).FirstValue;
            Type tipo = Nullable.GetUnderlyingType(contexto.ModelType) ?? contexto.ModelType;
            if (string.IsNullOrWhiteSpace(valor))
                return Task.CompletedTask;    // fica null: o ValidaDados do controller avisa

            string texto = valor.Trim().Replace(" ", "");
            if (texto.Contains(","))
                texto = texto.Replace(".", "").Replace(",", ".");   // "1.234,5" -> "1234.5"
            double numero;
            if (double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out numero))
                contexto.Result = ModelBindingResult.Success(tipo == typeof(decimal) ? (object)(decimal)numero : numero);
            else
                contexto.ModelState.TryAddModelError(contexto.ModelName, $"\"{valor}\" não é um número válido.");
            return Task.CompletedTask;
        }
    }

    public class NumeroModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder GetBinder(ModelBinderProviderContext contexto)
        {
            Type t = contexto.Metadata.ModelType;
            return t == typeof(double) || t == typeof(double?) || t == typeof(decimal) || t == typeof(decimal?)
                ? new NumeroModelBinder() : null;
        }
    }
}
