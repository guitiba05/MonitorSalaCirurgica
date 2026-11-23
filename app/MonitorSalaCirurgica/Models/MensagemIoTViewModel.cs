using System;
using System.Collections.Generic;

namespace MonitorSalaCirurgica.Models
{
    /// <summary>Uma medida gravada pelo STH-Comet</summary>
    public class MensagemIoTViewModel
    {
        public int Numero { get; set; }
        public DateTime DataHora { get; set; }     // horário local
        public string Sala { get; set; }
        public string Atributo { get; set; }
        public double Valor { get; set; }
        public string Unidade { get; set; }
    }

    /// <summary>Uma página da consulta de mensagens (partial view pvGridMensagens)</summary>
    public class PaginaMensagensViewModel
    {
        public List<MensagemIoTViewModel> Itens { get; set; } = new List<MensagemIoTViewModel>();
        public int Total { get; set; }
        public int Pagina { get; set; }
        public int Paginas { get; set; }
        public int Tamanho { get; set; }
        public string NomeGrandeza { get; set; }
        public string Unidade { get; set; }
        public string Entidade { get; set; }
    }

    /// <summary>Ponto de um gráfico: x = milissegundos (javascript), y = valor</summary>
    public class PontoGrafico
    {
        public long X { get; set; }
        public double Y { get; set; }
    }
}
