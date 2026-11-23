# Alexa: skill "monitor cirúrgico"

A Alexa é mais uma interface da aplicação de supervisão, ao lado do painel. Ela segue o mesmo padrão do projeto: **não fala com o ESP32**. A skill chama a aplicação ASP.NET Core, que lê o Orion e o STH-Comet e, para silenciar ou testar, manda o comando `setAlert` pelo Orion.

```
Echo ──voz──▶ Alexa (nuvem da Amazon) ──HTTPS──▶ ngrok ──▶ aplicação :5000 /api/alexa
                                                              │
                                     Orion :1026 ◀── GET/PATCH ┤
                                 STH-Comet :8666 ◀── GET ──────┘
```

Há duas partes, independentes:

| Parte | O que faz | Precisa de |
| --- | --- | --- |
| **Skill personalizada** | Responde quando alguém fala com o Echo ("Alexa, pergunte ao monitor cirúrgico se tem algum alerta") | Conta de desenvolvedor Amazon e um túnel HTTPS (ngrok) |
| **Anúncios** (opcional) | O Echo fala sozinho quando a supervisão dispara ou encerra um alerta | Um serviço de anúncio por URL, como o Voice Monkey |

## O que a skill entende

| Pedido (exemplos) | Intent | Resposta |
| --- | --- | --- |
| "como está a sala", "status da sala dois" | `EstadoSalaIntent` | As três grandezas e a situação do alarme |
| "qual a temperatura", "quanto está a umidade", "como está a luz" | `GrandezaIntent` | Valor atual, média da janela e faixa do perfil |
| "tem algum alerta", "por que o alarme está tocando" | `AlertaIntent` | Alerta ativo, motivo (média × limite), desde quando, padrão do LED/buzzer e se o ESP32 confirmou |
| "quais são os limites", "qual a faixa de umidade" | `LimitesIntent` | Limites do perfil ativo e janela da média |
| "silenciar o alarme por cinco minutos" | `SilenciarIntent` | **Pede confirmação** e então manda `setAlert off`; a supervisão volta sozinha depois |
| "reativar o alarme" | `ReativarIntent` | Encerra o silêncio ou o teste; a supervisão recalcula na hora |
| "testar o alerta de luz" | `TestarAlertaIntent` | Liga o padrão daquela grandeza por 15 s |
| "ajuda", "parar" | `AMAZON.HelpIntent`, `StopIntent` | |

Todos aceitam o número da sala ("sala dois" → `sala02`). Sem número, vale `Alexa:SalaPadrao` (`sala01`). O silêncio e os testes ficam registrados no **Histórico de alertas** com a origem `Alexa` ou `Silêncio`.

O modelo de interação (frases e slots) está em [`alexa/skill-package/interactionModels/custom/pt-BR.json`](../alexa/skill-package/interactionModels/custom/pt-BR.json). O código está em `app/MonitorSalaCirurgica/Services/Alexa/` (`AlexaSkill`, `AlexaVerificador`) e o endpoint em `Controllers/AlexaApiController.cs`.

## Testar sem o Echo

A tela **Alexa** da aplicação tem um simulador: cada botão monta o mesmo JSON que a Alexa enviaria e passa pela mesma lógica da skill, sem precisar de conta Amazon nem de túnel. A resposta aparece na tela e é lida em voz alta pelo navegador.

## Ligar a skill no Echo

### 1. Túnel HTTPS para a aplicação

A Alexa só chama endpoints **HTTPS** públicos. A aplicação roda no computador da equipe (`http://localhost:5000`), então um túnel dá a ela um endereço HTTPS:

1. Crie uma conta gratuita em [ngrok.com](https://ngrok.com), instale o ngrok e faça `ngrok config add-authtoken <token>`.
2. No painel do ngrok, em **Domains**, pegue o domínio estático gratuito (ex.: `monitor-sala.ngrok-free.app`). Com ele, a URL não muda a cada execução.
3. Com a aplicação rodando, abra o túnel:

```bash
ngrok http --url=monitor-sala.ngrok-free.app 5000
```

Teste no navegador: `https://monitor-sala.ngrok-free.app/api/alexa` deve mostrar "Endpoint da skill ... ativo".
Pelo túnel, **só esse endereço responde**: o painel e os cadastros continuam acessíveis apenas em `http://localhost:5000`
(`Alexa:SomenteSkillPeloTunel`).

### 2. Criar a skill

1. Entre no [Alexa Developer Console](https://developer.amazon.com/alexa/console/ask) com a **mesma conta Amazon do Echo**.
2. **Create Skill** → nome `Monitor de Sala Cirúrgica`, idioma **Portuguese (BR)**, tipo **Other** → **Custom** → hospedagem **Provision your own** → template **Start from Scratch**.
3. Em **Interaction Model › JSON Editor**, apague o conteúdo, cole o `pt-BR.json` deste repositório e clique em **Save** e depois em **Build skill**.
4. Em **Endpoint**, escolha **HTTPS** e preencha a região padrão:
   - URL: `https://monitor-sala.ngrok-free.app/api/alexa`
   - Certificado: **My development endpoint is a sub-domain of a domain that has a wildcard certificate from a certificate authority**
5. Clique em **Save**. Copie o **Skill ID** (`amzn1.ask.skill....`), que aparece no topo da página Endpoint.

### 3. Configurar a aplicação

No `appsettings.json`:

```json
"Alexa": {
  "SkillId": "amzn1.ask.skill.xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "VerificarAssinatura": true,
  "SalaPadrao": "sala01",
  "UrlAnuncio": "",
  "AnunciarNormalizacao": true
}
```

| Chave | Para que serve |
| --- | --- |
| `SkillId` | Só aceita pedidos desta skill. Vazio = aceita qualquer uma (só para testes) |
| `VerificarAssinatura` | Confere a assinatura da Amazon (certificado e `Signature-256`) e o horário do pedido (±150 s). Deixe `true` |
| `SalaPadrao` | Sala usada quando a pessoa não diz o número |
| `UrlAnuncio` | URL do serviço de anúncio, com `{texto}` no lugar da frase (seção abaixo) |
| `AnunciarNormalizacao` | Anunciar também quando a sala volta ao normal |
| `SomenteSkillPeloTunel` | Pelo túnel, só `/api/alexa` responde (o painel fica só no computador local). Deixe `true` |

### 4. Testar

1. No console, aba **Test**, mude para **Development**.
2. Digite ou fale: `abra o monitor cirúrgico`, depois `tem algum alerta`.
3. No Echo logado na mesma conta, a skill já funciona: "Alexa, abra o monitor cirúrgico".

| Problema | Causa provável |
| --- | --- |
| "Houve um problema com a resposta da skill" | O túnel não está aberto, a aplicação está parada ou a URL do endpoint está errada |
| HTTP 400 no log da aplicação: `Pedido de outra skill` | `Alexa:SkillId` diferente do ID do console |
| HTTP 400: `Pedido fora da janela de 150 s` | Relógio do computador atrasado: sincronize a hora do Windows |
| A skill responde "Não consegui falar com o FIWARE" | A EC2 está desligada ou o IP mudou (tela Conexão) |
| O console não aceita o nome de invocação | Troque `monitor cirúrgico` por `monitor cirurgico` (sem acento) no JSON |

## Anúncios por voz (opcional)

Uma skill personalizada só fala quando alguém pergunta. Para o Echo **avisar sozinho** quando a supervisão dispara um alerta, a aplicação chama uma URL de anúncio. Serve qualquer serviço que receba a frase pela URL. O mais simples é o **Voice Monkey** (gratuito para uso pessoal):

1. Em [voicemonkey.io](https://voicemonkey.io), entre com a conta Amazon e ative a skill **Voice Monkey** no app Alexa.
2. Crie um dispositivo (ex.: `eco-sala`) e, no **API Playground**, gere a URL de anúncio.
3. No `appsettings.json`, cole a URL em `Alexa:UrlAnuncio` trocando o texto da frase por `{texto}`.
4. Na tela **Alexa**, clique em **Testar anúncio no Echo**.

O que o Echo fala:

- ao disparar: "Atenção: alerta de temperatura na Sala 01: média de 29,1 graus, acima do máximo de 28 graus."
- ao normalizar: "Sala 01 voltou ao normal." (desligue com `AnunciarNormalizacao: false`)

Só as mudanças decididas pela **supervisão automática** são anunciadas; testes e silêncio, não. Se o serviço de anúncio falhar, o alarme continua funcionando normalmente: o erro só aparece no log.

## Segurança

- O endpoint `/api/alexa` não usa login: a proteção é a verificação da Amazon. Com `VerificarAssinatura: true`, a aplicação confere se a URL do certificado é `https://s3.amazonaws.com/echo.api/...`, se o certificado está válido e emitido para `echo-api.amazon.com`, se a cadeia chega a uma CA confiável e se a assinatura `Signature-256` confere com o corpo do pedido.
- Silenciar exige confirmação por voz e tem limite (`Supervisao:SilencioMaximoMinutos`, 30 min). Depois disso, a supervisão volta sozinha.
- Pelo túnel do ngrok só o endpoint da skill responde (`SomenteSkillPeloTunel`); pedidos para qualquer outra página recebem 404. Mesmo assim, feche o túnel quando não estiver usando.
