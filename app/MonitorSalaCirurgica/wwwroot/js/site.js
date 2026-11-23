// =====================================================================
//  Monitor de Sala Cirúrgica: todas as funções javascript,
//  organizadas por tela. As chamadas ao servidor usam $.ajax do jQuery.
// =====================================================================

// ---------------------------------------------------------------------
//  Funções gerais
// ---------------------------------------------------------------------

// Excluir com confirmação antes de apagar
function apagarRegistro(controller, id, descricao) {
    if (confirm('Confirma a exclusão de ' + descricao + '?'))
        location.href = '/' + controller + '/Delete?id=' + encodeURIComponent(id);
}

// Sala: pergunta também se a entidade e as inscrições devem sair do Orion
function apagarSala(id, entidade) {
    if (!confirm('Excluir a sala ' + id + ' do IoT Agent e do banco?'))
        return;
    var completa = entidade && confirm('Excluir também a entidade ' + entidade + ' do Orion e as inscrições do STH-Comet?\n' +
                                       '(OK = sim; Cancelar = manter a entidade e o histórico)');
    location.href = '/sala/Delete?id=' + encodeURIComponent(id) +
                    (completa ? '&excluirEntidade=true&entidade=' + encodeURIComponent(entidade) : '');
}

function formataNumero(valor, casas) {
    if (valor === null || valor === undefined || isNaN(valor)) return '—';
    return Number(valor).toLocaleString('pt-BR', { minimumFractionDigits: 0, maximumFractionDigits: casas === undefined ? 1 : casas });
}

function formataDataHora(valor, comData) {
    if (!valor) return '—';
    var d = new Date(valor);
    return comData === false ? d.toLocaleTimeString('pt-BR') : d.toLocaleString('pt-BR');
}

function escapaHtml(texto) {
    return String(texto === null || texto === undefined ? '' : texto)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

// Bolinha do rodapé: verde se IoT Agent, Orion e STH-Comet respondem
function verificaStatusFiware() {
    $.ajax({
        url: '/home/StatusServicos',
        success: function (dados) {
            var ok = dados.configurado && dados.componentes.every(function (c) { return c.ok; });
            $('#pontoFiware').attr('class', 'ponto ' + (ok ? 'ok' : 'falha'))
                .attr('title', dados.componentes.map(function (c) { return c.nome + ': ' + (c.ok ? 'ok ' + (c.versao || '') : 'falha'); }).join(' · ') || 'IP não configurado');
        },
        error: function () { $('#pontoFiware').attr('class', 'ponto falha'); }
    });
    setTimeout(verificaStatusFiware, 60000);
}

// Cartões de situação dos três componentes (telas Início e Conexão)
function exibeStatusServicos() {
    $.ajax({
        url: '/home/StatusServicos',
        success: function (dados) {
            dados.componentes.forEach(function (c) {
                var cartao = $('.status-servico[data-nome="' + c.nome + '"]');
                cartao.addClass(c.ok ? 'ok' : 'falha');
                cartao.find('.ponto').addClass(c.ok ? 'ok' : 'falha');
                cartao.find('[data-campo=url]').text(c.url);
                cartao.find('[data-campo=detalhe]').text(c.ok ? 'versão ' + (c.versao || '?') + ' · ' + c.milissegundos + ' ms' : c.erro);
            });
            if (!dados.configurado)
                $('.status-servico [data-campo=detalhe]').text('IP do FIWARE não configurado');
            var sup = dados.supervisao;
            $('#infoSupervisao').text(sup.erro ? 'Supervisão: ' + sup.erro
                : sup.ultimoCiclo ? 'Último ciclo da supervisão: ' + formataDataHora(sup.ultimoCiclo) : 'A supervisão ainda não rodou.');
        }
    });
}

// ---------------------------------------------------------------------
//  Cadastro de sala: sugere a entidade e mostra o tópico do firmware
// ---------------------------------------------------------------------
function sugereEntidade() {
    var id = $('#Id').val();
    var numero = (id.match(/(\d+)$/) || [])[1];
    if (numero) {
        $('#NomeEntidade').val('urn:ngsi-ld:SalaCirurgica:' + ('00' + parseInt(numero, 10)).slice(-3));
        $('#Nome').val('Sala ' + ('0' + parseInt(numero, 10)).slice(-2));
    }
    $('#topicosFirmware').html('<strong>Medidas:</strong> <code>/' + escapaHtml($('#Apikey').val()) + '/' + escapaHtml(id) +
                               '/attrs</code> → <code>t|23.5|h|58|l|42</code>');
}

// ---------------------------------------------------------------------
//  Consulta de mensagens IoT (consulta avançada com filtros)
// ---------------------------------------------------------------------
function paraCampoData(d) {     // Date -> valor de <input type="datetime-local">
    function dois(n) { return (n < 10 ? '0' : '') + n; }
    return d.getFullYear() + '-' + dois(d.getMonth() + 1) + '-' + dois(d.getDate()) + 'T' + dois(d.getHours()) + ':' + dois(d.getMinutes());
}

// Data do campo (horário do navegador) -> ISO UTC; o servidor converte para o horário dele
function paraIsoUtc(valor) {
    return valor ? new Date(valor).toISOString() : '';
}

function consultaUltimos(minutos) {
    var agora = new Date();
    document.getElementById('dataInicial').value = paraCampoData(new Date(agora.getTime() - minutos * 60000));
    document.getElementById('dataFinal').value = paraCampoData(new Date(agora.getTime() + 60000));
    aplicaFiltroConsultaAvancada(1);
}

function aplicaFiltroConsultaAvancada(pagina) {
    var vSala = document.getElementById('sala').value;
    var vGrandeza = document.getElementById('grandeza').value;
    var vDataInicial = paraIsoUtc(document.getElementById('dataInicial').value);
    var vDataFinal = paraIsoUtc(document.getElementById('dataFinal').value);
    var vOrdem = document.getElementById('ordem').value;
    var vTamanho = document.getElementById('tamanho').value;
    $.ajax({
        url: '/mensagem/ObtemDadosConsultaAvancada',
        data: { sala: vSala, grandeza: vGrandeza, dataInicial: vDataInicial, dataFinal: vDataFinal,
                pagina: pagina || 1, tamanho: vTamanho, ordem: vOrdem },
        beforeSend: function () {
            document.getElementById('resultadoConsulta').innerHTML = '<div class="text-muted p-3">Consultando o STH-Comet…</div>';
        },
        success: function (dados) {
            if (dados.erro != undefined) {
                document.getElementById('resultadoConsulta').innerHTML =
                    '<div class="alert alert-danger">' + escapaHtml(dados.msg) + '</div>';
            }
            else {
                document.getElementById('resultadoConsulta').innerHTML = dados;
            }
        },
        error: function () {
            document.getElementById('resultadoConsulta').innerHTML = '<div class="alert alert-danger">A aplicação não respondeu.</div>';
        }
    });
}

// ---------------------------------------------------------------------
//  Dashboard: valores a cada 5 s, histórico a cada 15 s
// ---------------------------------------------------------------------
var salaDashboard = null;
var minutosHistorico = 15;
var graficos = {};
var graficoHistograma = null;
var ultimoHistorico = null;
var NOMES_ALERTA = { luz: 'luminosidade', temp: 'temperatura', umid: 'umidade' };
var TEXTO_SITUACAO = { normal: 'normal', acima: 'acima do máximo', abaixo: 'abaixo do mínimo', 'sem-dados': 'sem dados', 'sem-limite': 'sem limite' };

function corDaGrandeza(grandeza) {
    var variavel = { temperatura: '--cor-temp', umidade: '--cor-umid', luminosidade: '--cor-luz' }[grandeza];
    return getComputedStyle(document.documentElement).getPropertyValue(variavel).trim();
}

function iniciaDashboard() {
    salaDashboard = $('#dashboard').data('sala');
    if (!salaDashboard) return;
    $('.seletor-periodo [data-minutos]').click(function () {
        $('.seletor-periodo .btn').removeClass('active');
        $(this).addClass('active');
        minutosHistorico = Number($(this).data('minutos'));
        carregaHistorico();
    });
    carregaDashboard();
    carregaHistorico();
    setInterval(carregaHistorico, 15000);
}

function carregaDashboard() {
    $.ajax({
        url: '/dashboard/ObtemDadosDashboard',
        data: { salaId: salaDashboard },
        success: function (dados) {
            if (dados.erro != undefined) {
                $('#erroDashboard').text(dados.msg).removeClass('d-none');
                return;
            }
            $('#erroDashboard').addClass('d-none');
            montaCabecalho(dados);
            montaFaixaAlerta(dados);
            dados.grandezas.forEach(montaCartao);
            if (dados.aviso) $('#erroDashboard').text(dados.aviso).removeClass('d-none');
        },
        complete: function () { setTimeout(carregaDashboard, 5000); }
    });
}

function montaCabecalho(d) {
    var partes = ['<strong>' + escapaHtml(d.nome) + '</strong> · ' + escapaHtml(d.deviceId) + ' · <code>' + escapaHtml(d.entityId) + '</code>',
                  'perfil <strong>' + escapaHtml(d.perfil) + '</strong>', 'média de ' + d.janelaSegundos + ' s',
                  d.supervisaoAtiva ? 'supervisão ligada' : '<span class="text-danger">supervisão desligada</span>'];
    if (d.estado && d.estado.atualizadoEm) partes.push('última medida ' + formataDataHora(d.estado.atualizadoEm, false));
    if (d.ultimaVerificacao) partes.push('verificado ' + formataDataHora(d.ultimaVerificacao, false));
    $('#infoSala').html(partes.join(' · '));
}

// Faixa do alerta + LED virtual: mostra o que a placa confirmou (atributo "alert")
function montaFaixaAlerta(d) {
    var mostrado = d.alertaMostrado, p = d.padrao, linhas = [];
    var faixa = $('#faixa').attr('class', 'faixa alerta-' + mostrado);
    var led = $('#led');
    if (p.meioCicloMs > 0) led.addClass('piscando').css('--meio', p.meioCicloMs + 'ms');
    else led.removeClass('piscando');

    if (!d.estado) {
        faixa.attr('class', 'faixa indefinido');
        $('#faixaTitulo').text('Sala sem dados no Orion');
        linhas.push('O ESP32 ainda não publicou medidas. Confira se ele está ligado e conectado ao Mosquitto.');
    } else if (mostrado === 'off') {
        $('#faixaTitulo').text('Sala dentro dos limites');
        linhas.push('LED azul apagado · buzzer desligado');
    } else {
        $('#faixaTitulo').text('ALERTA DE ' + (NOMES_ALERTA[mostrado] || mostrado).toUpperCase());
        linhas.push('LED azul ' + p.led + ' · buzzer ' + p.buzzerHz + ' Hz' + (d.alertaDesde ? ' · desde ' + formataDataHora(d.alertaDesde, false) : ''));
    }
    if (d.motivo && d.alertaCalculado !== 'off') linhas.push(escapaHtml(d.motivo));
    if (d.ultimoEnviado && d.ultimoEnviado !== mostrado) linhas.push('aguardando o ESP32 confirmar "' + d.ultimoEnviado + '"');
    else if (d.estado && d.estado.statusComando) linhas.push('último comando: ' + d.estado.statusComando + (d.estado.infoComando ? ' (' + escapaHtml(d.estado.infoComando) + ')' : ''));

    if (d.intervencao) {
        if (d.intervencao.alerta === 'off') faixa.addClass('silencio');
        linhas.unshift(escapaHtml(d.intervencao.descricao) + ' · até ' + formataDataHora(d.intervencao.ate, false));
        $('#btnReativar').removeClass('d-none');
    } else {
        $('#btnReativar').addClass('d-none');
    }
    $('#faixaDetalhe').html(linhas.map(function (l) { return '<div>' + l + '</div>'; }).join(''));
}

function montaCartao(g) {
    var c = $('.cartao-grandeza[data-grandeza="' + g.grandeza + '"]');
    var casas = g.grandeza === 'temperatura' ? 1 : 0;
    c.find('[data-campo=atual]').text(formataNumero(g.atual, casas));
    c.find('[data-campo=media]').text(g.media === null ? '—'
        : formataNumero(g.media, 1) + ' ' + g.unidade + (g.mediaDoOrion ? ' (valor do Orion)' : ' (' + g.pontos + ' pontos)'));
    c.find('[data-campo=faixa]').text(g.minimo === null ? 'sem limite'
        : formataNumero(g.minimo) + ' a ' + formataNumero(g.maximo) + ' ' + g.unidade + (g.histerese ? ' · histerese ' + formataNumero(g.histerese) : ''));
    c.find('[data-campo=situacao]').attr('class', 'situacao ' + g.situacao).text(TEXTO_SITUACAO[g.situacao] || g.situacao);
    c.toggleClass('anomalia', g.emAnomalia);
}

// Gráfico 1: um gráfico de linha por grandeza, com as linhas de limite
function criaGraficoLinha(grandeza) {
    var cor = corDaGrandeza(grandeza);
    var limite = { borderColor: '#8b98a3', borderDash: [6, 4], borderWidth: 1.5, pointRadius: 0, fill: false };
    return new Chart(document.getElementById('grafico-' + grandeza), {
        type: 'line',
        data: {
            datasets: [
                { label: grandeza, data: [], borderColor: cor, backgroundColor: cor + '22', borderWidth: 2, pointRadius: 0, fill: true, tension: 0.25 },
                $.extend({ label: 'máximo', data: [] }, limite),
                $.extend({ label: 'mínimo', data: [] }, limite)
            ]
        },
        options: {
            animation: false, maintainAspectRatio: false, parsing: false, normalized: true,
            interaction: { mode: 'nearest', axis: 'x', intersect: false },
            scales: {
                x: { type: 'linear', ticks: { maxTicksLimit: 6, callback: function (v) { return new Date(v).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }); } } },
                y: { ticks: { callback: function (v) { return formataNumero(v); } } }
            },
            plugins: {
                legend: { display: false },
                title: { display: true, text: '', align: 'start' },
                tooltip: { callbacks: {
                    title: function (itens) { return itens.length ? formataDataHora(itens[0].parsed.x) : ''; },
                    label: function (item) { return item.dataset.label + ': ' + formataNumero(item.parsed.y, 2); }
                } }
            }
        }
    });
}

function carregaHistorico() {
    $.ajax({
        url: '/dashboard/ObtemHistorico',
        data: { salaId: salaDashboard, minutos: minutosHistorico },
        success: function (h) {
            if (h.erro != undefined) {
                $('#infoHistorico').text('Histórico indisponível: ' + h.msg);
                return;
            }
            ultimoHistorico = h;
            h.series.forEach(function (s) {
                var ch = graficos[s.grandeza] || (graficos[s.grandeza] = criaGraficoLinha(s.grandeza));
                ch.data.datasets[0].data = s.pontos;
                ch.data.datasets[0].label = s.nome;
                ch.data.datasets[1].data = s.maximo === null ? [] : [{ x: h.de, y: s.maximo }, { x: h.ate, y: s.maximo }];
                ch.data.datasets[2].data = s.minimo === null ? [] : [{ x: h.de, y: s.minimo }, { x: h.ate, y: s.minimo }];
                ch.options.scales.x.min = h.de;
                ch.options.scales.x.max = h.ate;
                ch.options.plugins.title.text = s.nome + ' (' + s.unidade + ') · ' + s.pontos.length + (h.agregado ? ' médias por minuto' : ' medidas');
                ch.update('none');
            });
            $('#infoHistorico').text('Perfil ' + h.perfil + ': as linhas tracejadas são o mínimo e o máximo. ' +
                (h.agregado ? 'Períodos longos mostram a média de cada minuto (aggrMethod=sum).' : 'Medidas brutas do STH-Comet.'));
            montaHistograma();
        },
        error: function () { $('#infoHistorico').text('Histórico indisponível: a aplicação não respondeu.'); }
    });
}

// Gráfico 2: histograma das leituras do período; faixas fora dos limites em vermelho
function montaHistograma() {
    if (!ultimoHistorico) return;
    var chave = $('#grandezaHistograma').val();
    var serie = ultimoHistorico.series.filter(function (s) { return s.grandeza === chave; })[0];
    if (!serie) return;
    var valores = serie.pontos.map(function (p) { return p.y; });
    montaResumoPeriodo(serie, valores);
    var faixas = calculaFaixas(valores, serie.minimo, serie.maximo);
    var cor = corDaGrandeza(chave);
    var perigo = getComputedStyle(document.documentElement).getPropertyValue('--perigo').trim();
    var dados = {
        labels: faixas.map(function (f) { return formataNumero(f.de) + '–' + formataNumero(f.ate); }),
        datasets: [{
            label: 'leituras', data: faixas.map(function (f) { return f.quantidade; }),
            backgroundColor: faixas.map(function (f) { return f.foraDoLimite ? perigo : cor; }), borderRadius: 4
        }]
    };
    if (!graficoHistograma) {
        graficoHistograma = new Chart(document.getElementById('histograma'), {
            type: 'bar', data: dados,
            options: {
                animation: false, maintainAspectRatio: false,
                scales: { x: { title: { display: true, text: serie.unidade } }, y: { beginAtZero: true, ticks: { precision: 0 } } },
                plugins: {
                    legend: { display: false },
                    tooltip: { callbacks: { label: function (item) { return item.parsed.y + ' leitura(s)'; } } }
                }
            }
        });
    } else {
        graficoHistograma.data = dados;
        graficoHistograma.options.scales.x.title.text = serie.unidade;
        graficoHistograma.update('none');
    }
}

// Cartão "Resumo do período": mínima, média, máxima e leituras fora do limite
function montaResumoPeriodo(serie, valores) {
    var u = ' ' + serie.unidade;
    $('#periodoResumo').text(serie.nome + ' · ' + (ultimoHistorico.agregado ? 'médias por minuto' : 'medidas brutas') +
                             ' dos últimos ' + (minutosHistorico >= 60 ? minutosHistorico / 60 + ' h' : minutosHistorico + ' min'));
    if (valores.length === 0) {
        $('#resumoLeituras').text('0'); $('#resumoMinimo, #resumoMedia, #resumoMaximo, #resumoFora').text('—');
        return;
    }
    var soma = valores.reduce(function (a, b) { return a + b; }, 0);
    var fora = valores.filter(function (v) { return (serie.maximo !== null && v > serie.maximo) || (serie.minimo !== null && v < serie.minimo); }).length;
    $('#resumoLeituras').text(valores.length);
    $('#resumoMinimo').text(formataNumero(Math.min.apply(null, valores), 1) + u);
    $('#resumoMedia').text(formataNumero(soma / valores.length, 1) + u);
    $('#resumoMaximo').text(formataNumero(Math.max.apply(null, valores), 1) + u);
    $('#resumoFora').html(fora === 0 ? '<span class="text-success">nenhuma</span>'
        : '<span class="text-danger">' + fora + ' (' + formataNumero(100 * fora / valores.length, 0) + ' %)</span>');
}

// Divide os valores em até 10 faixas de mesma largura, incluindo os limites
function calculaFaixas(valores, minimo, maximo) {
    if (valores.length === 0) return [];
    var menor = Math.min.apply(null, valores), maior = Math.max.apply(null, valores);
    if (minimo !== null) menor = Math.min(menor, minimo);
    if (maximo !== null) maior = Math.max(maior, maximo);
    if (maior === menor) maior = menor + 1;
    var n = 10, largura = (maior - menor) / n, faixas = [];
    for (var i = 0; i < n; i++) {
        var de = menor + i * largura, ate = de + largura;
        faixas.push({ de: de, ate: ate, quantidade: 0,
                      foraDoLimite: (maximo !== null && de >= maximo) || (minimo !== null && ate <= minimo) });
    }
    valores.forEach(function (v) { faixas[Math.min(n - 1, Math.floor((v - menor) / largura))].quantidade++; });
    return faixas;
}

function silenciaAlarme() {
    $.ajax({
        url: '/dashboard/Silenciar', type: 'POST',
        data: { salaId: salaDashboard, minutos: $('#minutosSilencio').val() },
        success: function (dados) {
            if (dados.erro != undefined) alert(dados.msg);
            else carregaDashboardAgora();
        }
    });
}

function reativaAlarme() {
    $.ajax({
        url: '/dashboard/Reativar', type: 'POST', data: { salaId: salaDashboard },
        success: function () { setTimeout(carregaDashboardAgora, 1500); }   // o supervisor recalcula e reenvia
    });
}

function carregaDashboardAgora() {
    $.ajax({
        url: '/dashboard/ObtemDadosDashboard', data: { salaId: salaDashboard },
        success: function (dados) {
            if (dados.erro == undefined) { montaFaixaAlerta(dados); dados.grandezas.forEach(montaCartao); }
        }
    });
}

// ---------------------------------------------------------------------
//  Simulador da skill da Alexa
// ---------------------------------------------------------------------
var ultimoPedidoAlexa = null;

function simulaAlexa(tipo, intent, usaGrandeza, usaMinutos) {
    var pedido = { tipo: tipo, intent: intent || '', sala: $('#slotSala').val(), confirmacao: 'NONE' };
    if (usaGrandeza) pedido.grandeza = $('#slotGrandeza').val();
    if (usaMinutos) pedido.minutos = $('#slotMinutos').val();
    enviaPedidoAlexa(pedido);
}

function confirmaAlexa(resposta) {
    if (ultimoPedidoAlexa)
        enviaPedidoAlexa($.extend({}, ultimoPedidoAlexa, { confirmacao: resposta }));
}

function enviaPedidoAlexa(pedido) {
    ultimoPedidoAlexa = pedido;
    $.ajax({
        url: '/alexa/Simular', type: 'POST', data: pedido,
        beforeSend: function () { $('#falaAlexa').text('…'); },
        success: function (r) {
            if (r.erro != undefined) { $('#falaAlexa').text('Erro: ' + r.msg); return; }
            $('#falaAlexa').text(r.fala || '(sem fala)');
            $('#jsonAlexa').text(r.json);
            $('#confirmaAlexa').toggleClass('d-none', !r.confirmar);
            if ($('#falarAlexa').is(':checked') && r.fala && 'speechSynthesis' in window) {
                speechSynthesis.cancel();
                var u = new SpeechSynthesisUtterance(r.fala);
                u.lang = 'pt-BR';
                speechSynthesis.speak(u);
            }
        }
    });
}

function testaAnuncioAlexa() {
    $.ajax({
        url: '/alexa/TestarAnuncio', type: 'POST',
        beforeSend: function () { $('#resultadoAnuncio').text('Enviando…'); },
        success: function (r) { $('#resultadoAnuncio').text(r.msg); }
    });
}
