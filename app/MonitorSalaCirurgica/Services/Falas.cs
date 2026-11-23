using MonitorSalaCirurgica.Models;
using System;
using System.Globalization;
using System.Linq;

namespace MonitorSalaCirurgica.Services
{
    /// <summary>Frases em português da Alexa (skill e anúncios). "23,5" é lido corretamente em pt-BR.</summary>
    public static class Falas
    {
        private static readonly CultureInfo PtBr = new CultureInfo("pt-BR");

        public static string Numero(double? v) => v.HasValue ? v.Value.ToString("0.#", PtBr) : "sem leitura";

        public static string Valor(double? v, Grandeza g) => v.HasValue ? $"{Numero(v)} {g.UnidadeFalada}" : "sem leitura";

        /// <summary>"há 3 minutos", "há 40 segundos"</summary>
        public static string Ha(DateTime desde)
        {
            var t = DateTime.Now - desde;
            if (t.TotalSeconds < 60) return $"há {Math.Max(1, (int)t.TotalSeconds)} segundos";
            if (t.TotalMinutes < 2) return "há 1 minuto";
            if (t.TotalMinutes < 60) return $"há {(int)t.TotalMinutes} minutos";
            return $"desde as {desde:HH} e {desde:mm}";
        }

        /// <summary>"alerta de temperatura na Sala 01: média de 29,1 graus, acima do máximo de 28 graus."</summary>
        public static string DescreveAlerta(ResumoSalaViewModel r, string alerta)
        {
            var g = Contrato.GrandezaPorAlerta(alerta);
            if (g == null) return $"{r.Nome} sem alertas.";
            var av = r.Grandeza(g.Chave);
            string texto = $"alerta de {g.Chave} na {r.Nome}";
            if (av?.Media == null) return texto + ".";
            string limite = av.Situacao == "abaixo" ? $"abaixo do mínimo de {Valor(av.Minimo, g)}" : $"acima do máximo de {Valor(av.Maximo, g)}";
            return $"{texto}: média de {Valor(av.Media, g)}, {limite}.";
        }

        public static string Resumo(ResumoSalaViewModel r)
        {
            if (r.Estado == null)
                return $"A {r.Nome} ainda não enviou medidas ao FIWARE. Confira se o ESP32 está ligado e conectado.";
            var partes = Contrato.GrandezasExibicao.Select(g => $"{g.Chave} {Valor(r.Grandeza(g.Chave)?.Atual, g)}").ToList();
            return $"Na {r.Nome}: {partes[0]}, {partes[1]} e {partes[2]}. " + Alarme(r);
        }

        /// <summary>Alerta ativo, silenciado, em teste ou tudo normal</summary>
        public static string Alarme(ResumoSalaViewModel r)
        {
            if (r.Intervencao != null)
            {
                int resta = (int)Math.Ceiling((r.Intervencao.Ate - DateTime.Now).TotalMinutes);
                return r.Intervencao.Alerta == "off"
                    ? $"O alarme está silenciado por mais {Math.Max(1, resta)} {(resta == 1 ? "minuto" : "minutos")}."
                    : $"Há um teste do alerta de {Contrato.NomeAlerta(r.Intervencao.Alerta)} em andamento.";
            }
            if (!r.SupervisaoAtiva) return "A supervisão automática desta sala está desligada.";

            var fora = r.Grandezas.Where(g => g.EmAnomalia).ToList();
            if (fora.Count == 0) return $"Tudo dentro dos limites do perfil {r.Perfil}.";

            string texto = "Atenção: " + DescreveAlerta(r, r.AlertaCalculado);
            if (r.AlertaDesde.HasValue && r.UltimoEnviado == r.AlertaCalculado)
                texto += $" O alarme está tocando {Ha(r.AlertaDesde.Value)}.";
            if (fora.Count > 1)
            {
                var outras = fora.Where(f => Contrato.GrandezaPorChave(f.Grandeza)?.Alerta != r.AlertaCalculado).Select(f => f.Grandeza);
                texto += $" Também fora do limite: {string.Join(" e ", outras)}. Pela prioridade, vale o alerta de {Contrato.NomeAlerta(r.AlertaCalculado)}.";
            }
            return texto;
        }

        public static string Grandeza(ResumoSalaViewModel r, Grandeza g)
        {
            var av = r.Grandeza(g.Chave);
            if (av?.Atual == null) return $"Ainda não tenho a {g.Chave} da {r.Nome}.";
            string texto = $"A {g.Chave} da {r.Nome} está em {Valor(av.Atual, g)}.";
            if (av.Media.HasValue && !av.MediaDoOrion)
                texto += $" A média dos últimos {r.JanelaSegundos} segundos é {Valor(av.Media, g)}.";
            if (av.Minimo.HasValue && av.Maximo.HasValue)
            {
                if (av.Situacao == "acima") texto += $" Está acima do máximo de {Valor(av.Maximo, g)}.";
                else if (av.Situacao == "abaixo") texto += $" Está abaixo do mínimo de {Valor(av.Minimo, g)}.";
                else texto += $" Dentro da faixa de {Numero(av.Minimo)} a {Valor(av.Maximo, g)}.";
            }
            return texto;
        }

        public static string Limites(ResumoSalaViewModel r, Grandeza so)
        {
            var itens = Contrato.GrandezasExibicao.Where(g => so == null || g == so)
                .Where(g => r.Grandeza(g.Chave)?.Minimo != null)
                .Select(g => $"{g.Chave} de {Numero(r.Grandeza(g.Chave).Minimo)} a {Valor(r.Grandeza(g.Chave).Maximo, g)}")
                .ToList();
            if (itens.Count == 0) return $"O perfil {r.Perfil} não tem limites cadastrados.";
            return $"No perfil {r.Perfil}, usado na {r.Nome}: {string.Join(", ", itens)}. A média é calculada em {r.JanelaSegundos} segundos.";
        }
    }
}
