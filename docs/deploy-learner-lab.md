# Deploy do FIWARE no AWS Learner Lab

Passo a passo para subir a plataforma usada pelo projeto (Orion, IoT Agent UltraLight, STH-Comet, Mosquitto e MongoDB) numa instância EC2 do **AWS Academy Learner Lab**, do zero, numa sessão nova.

Base: [FIWARE Descomplicado](https://github.com/fabiocabrini/fiware) (Prof. Fábio Cabrini) — `docker-compose.yml` com seis containers:

| Container | Imagem | Porta no host | Papel |
| --- | --- | --- | --- |
| `fiware-orion` | `fiware/orion:3.11.0` | 1026 | estado atual (NGSI-v2) |
| `fiware-iot-agent` | `fiware/iotagent-ul` | 4041 | provisionamento e tradução UltraLight → NGSI |
| `fiware-sth-comet` | `telefonicaiot/fiware-sth-comet` | 8666 | histórico consultado pela aplicação |
| `fiware-mosquitto` | `eclipse-mosquitto` | 1883 (e 9001, não usada) | broker MQTT do ESP32 |
| `fiware-mongo-historical` | `mongo:4.4` | 27017 | com senha: séries do STH-Comet **e** entidades/inscrições do Orion |
| `fiware-mongo-internal` | `mongo:4.4` | — | sem senha, só rede interna: registro de grupos e dispositivos do IoT Agent |

> **Nota:** O kickoff desenha o Orion ligado a um "MongoDB de contexto" separado. No `docker-compose.yml` do repositório de referência, porém, o Orion sobe com `-dbhost mongo-db-historical` (com usuário e senha), e o `mongo-db-internal` fica só com o IoT Agent. O diagrama deste projeto ([`arquitetura.svg`](arquitetura.svg)) mostra a ligação como ela roda de fato na EC2.

> **Importante:** O IP público da EC2 muda a cada sessão do Learner Lab. Não existe IP elástico na conta do laboratório. Por isso o endereço é configurável nos dois lados: `Fiware:Host` no `appsettings.json` (ou tela **Conexão**) e `MQTT_BROKER` no `secrets.h` do firmware. Anote o IP novo no início de cada sessão.

---

## Passo 0 — Iniciar a sessão do laboratório

1. Entre no **AWS Academy** (pelo Canvas) e abra o curso **Learner Lab – Foundational Services**.
2. Aba **Modules** → abra o laboratório → **Start Lab**.
3. Espere o círculo ao lado de **AWS** ficar **verde** (leva de 1 a 3 minutos). Só então o console abre.
4. Clique em **AWS Details** → **SSH Key** → **Download key pair**. Guarde o `vockey.pem` (é a chave do par `vockey`, que já existe na conta).
5. Deixe a página do laboratório aberta: ela mostra o **tempo restante da sessão** (cerca de 4 h) e os botões **Start/End Lab**.

> **Atenção:** End Lab encerra a sessão e libera os recursos. A instância EC2 criada continua existindo, mas fica parada — e o IP público será outro na próxima sessão.

## Passo 1 — Criar a instância EC2

No console da AWS, abra **EC2** (região **us-east-1**, a única liberada no laboratório em geral):

1. **Launch instance**.
2. **Name**: `monitor-sala-cirurgica`.
3. **Application and OS Images**: **Ubuntu Server 22.04 LTS (HVM), SSD Volume Type**, arquitetura **64-bit (x86)**.
4. **Instance type**: **t3.micro**. Se o `docker-compose` reclamar de memória (o stack tem seis containers), use **t3.small**.
5. **Key pair**: **vockey** (o par do laboratório, cujo `.pem` você baixou).
6. **Network settings** → **Edit** → **Create security group** e marque as regras do Passo 2. O grupo `default` não libera nada.
7. **Configure storage**: **20 GiB**, tipo **gp3** (o padrão de 8 GiB fica apertado para as imagens e os dois volumes do MongoDB).
8. **Launch instance** e espere o estado **Running** e os **2/2 status checks** passarem.
9. Copie o **Public IPv4 address** do resumo da instância — é o IP da sessão.

## Passo 2 — Security Group: só as portas usadas

Edite o grupo de segurança da instância (**Security** → **Security groups** → **Edit inbound rules**) e deixe exatamente estas regras:

| Porta | Protocolo | Origem sugerida | Para quê |
| --- | --- | --- | --- |
| 22 | TCP | **My IP** (IP da equipe) | SSH na EC2 |
| 1883 | TCP | **Anywhere** durante a montagem | MQTT do ESP32 (o broker é o Mosquitto) |
| 1026 | TCP | **Anywhere** durante a montagem | Orion: estado atual e comandos (aplicação) |
| 4041 | TCP | **Anywhere** durante a montagem | IoT Agent: CRUD de salas e inscrições (aplicação) |
| 8666 | TCP | **Anywhere** durante a montagem | STH-Comet: histórico e médias (aplicação) |
| 7896 | TCP | **My IP** | **Só durante o desenvolvimento**, para simular medidas sem hardware |
| 27017 | TCP | **My IP** | **Opcional**, só para inspecionar o MongoDB histórico |

Notas:

- A porta **1883** precisa aceitar o IP da rede de onde o ESP32 está (a rede da faculdade muda), por isso `Anywhere` na montagem. Nas outras portas, troque `Anywhere` pelo IP da equipe quando o ambiente estabilizar.
- A **7896** (UltraLight por HTTP) só é liberada enquanto a aplicação for testada sem a placa. Depois, feche.
- A **27017** exposta é risco desnecessário: use só na depuração e feche em seguida.
- A aplicação roda **no computador da equipe**, não na EC2: ela sai pela internet e chega nessas portas pelo IP público.

## Passo 3 — Conectar por SSH

No terminal do computador (PowerShell, WSL ou Git Bash), na pasta do `vockey.pem`:

```bash
# Linux / macOS / WSL / Git Bash
chmod 400 vockey.pem
ssh -i vockey.pem ubuntu@<IP_PUBLICO>
```

```powershell
# PowerShell: se reclamar da permissão do arquivo
icacls vockey.pem /inheritance:r /grant:r "$($env:USERNAME):(R)"
ssh -i vockey.pem ubuntu@<IP_PUBLICO>
```

O usuário da AMI Ubuntu é **`ubuntu`** (na Amazon Linux seria `ec2-user`). Na primeira conexão, responda `yes`.

## Passo 4 — Instalar Docker, Compose e Git

Dentro da EC2:

```bash
sudo apt update
sudo apt install -y docker.io docker-compose git curl
sudo usermod -aG docker ubuntu
```

Saia e entre de novo no SSH para o grupo `docker` valer:

```bash
exit
ssh -i vockey.pem ubuntu@<IP_PUBLICO>
docker --version
docker-compose --version
```

> **Nota:** O pacote `docker-compose` do Ubuntu 22.04 é a **v1.29** (comando `docker-compose`). O `--wait`, citado no Guia dos desenvolvedores, **só existe na v2** (`docker compose`, com espaço). Na v1, suba com `up -d` e confira com `ps`, como nos passos seguintes. Se quiser a v2: `sudo apt install docker-compose-v2` (Ubuntu 24.04) ou instale o plugin oficial.

## Passo 5 — Subir o FIWARE

```bash
git clone https://github.com/fabiocabrini/fiware.git
cd fiware
sudo docker-compose up -d
sudo docker-compose ps
```

Espere todos os containers com estado **Up** (o `mongo` pode reiniciar uma ou duas vezes na primeira execução, enquanto cria os volumes).

Se algum container não subir, veja o log dele:

```bash
sudo docker-compose logs --tail=50 orion
sudo docker-compose logs --tail=50 iot-agent
```

## Passo 6 — Health check (requisições 0.1 a 0.3 da coleção)

Na própria EC2:

```bash
curl -s http://localhost:1026/version       # Orion
curl -s http://localhost:4041/iot/about     # IoT Agent
curl -s http://localhost:8666/version       # STH-Comet
```

Do computador da equipe, trocando `localhost` pelo IP público — é isso que a tela **Conexão** da aplicação faz (`GET /version` e `GET /iot/about`):

```bash
curl -s http://<IP_PUBLICO>:1026/version
curl -s http://<IP_PUBLICO>:4041/iot/about
curl -s http://<IP_PUBLICO>:8666/version
```

> **Dica:** Se o `8666/version` responder **404**, não quer dizer que o STH-Comet está fora: algumas versões da imagem não expõem `/version`. Confirme com `sudo docker-compose ps` e com uma consulta real ao histórico (`/STH/v1/...`) antes de concluir que há problema.

## Passo 7 — Provisionar a sala

Há dois caminhos equivalentes. O primeiro é o do Guia (Postman, para adiantar o teste do firmware); o segundo é a própria aplicação.

### 7.1 Pelo Postman

A numeração abaixo (0.1, 2.1, 2.3, 2.6, 3.1) é a da coleção usada na disciplina, citada no Guia dos desenvolvedores e nos comentários do código. A coleção pública do repositório de referência, `FIWARE Descomplicado.postman_collection.json`, traz as mesmas chamadas com outros nomes e usa a variável `url` para o IP:

| Passo | Coleção da disciplina | Coleção pública (pasta › requisição) | Chamada |
| --- | --- | --- | --- |
| Health check | 0.1 a 0.3 | *IOT Agent MQTT › 1.1*, *STH-Comet › 1*, *Orion › 1.Version* | `GET :4041/iot/about`, `:8666/version`, `:1026/version` |
| Grupo de serviço | 2.1 | *IOT Agent MQTT › 2. Provisioning a Service Group* | `POST :4041/iot/services` |
| Dispositivo | 2.3 | *IOT Agent MQTT › 3. Provisioning a Smart Lamp* | `POST :4041/iot/devices` |
| Inscrição | 2.6 | *STH-Comet › 2. Subscribe Luminosity* | `POST :1026/v2/subscriptions` |
| Medida simulada | 3.1 | — (usar o `curl` da seção *Sem hardware* do README) | `POST :7896/iot/d` |

Nas duas coleções, **troque os corpos** pelos do **contrato** abaixo: os da coleção pública são da PoC Smart Lamp (apikey `TEF`, entidade `Lamp`) e não servem para a sala. Execute, nesta ordem: health check, grupo de serviço, dispositivo e uma inscrição para cada grandeza. É exatamente o que a aplicação envia:

**Grupo de serviço** — `POST http://<IP>:4041/iot/services`

```json
{
  "services": [{
    "apikey": "CIRURGIA01",
    "resource": "/iot/d",
    "entity_type": "SalaCirurgica",
    "autoprovision": false
  }]
}
```

**Dispositivo** — `POST http://<IP>:4041/iot/devices`

```json
{
  "devices": [{
    "device_id": "sala01",
    "entity_name": "urn:ngsi-ld:SalaCirurgica:001",
    "entity_type": "SalaCirurgica",
    "apikey": "CIRURGIA01",
    "protocol": "PDI-IoTA-UltraLight",
    "transport": "MQTT",
    "attributes": [
      { "object_id": "t", "name": "temperature", "type": "Number" },
      { "object_id": "h", "name": "humidity",    "type": "Number" },
      { "object_id": "l", "name": "luminosity",  "type": "Integer" },
      { "object_id": "a", "name": "alert",       "type": "Text" }
    ],
    "commands": [{ "name": "setAlert", "type": "command" }],
    "static_attributes": [{ "name": "nome", "type": "Text", "value": "Sala 01" }]
  }]
}
```

**Inscrição** — `POST http://<IP>:1026/v2/subscriptions` (uma por grandeza: `temperature`, `humidity`, `luminosity`)

```json
{
  "description": "STH-Comet: temperature de urn:ngsi-ld:SalaCirurgica:001",
  "subject": {
    "entities": [{ "id": "urn:ngsi-ld:SalaCirurgica:001", "type": "SalaCirurgica" }],
    "condition": { "attrs": ["temperature"] }
  },
  "notification": {
    "httpCustom": {
      "url": "http://sth-comet:8666/notify",
      "json": {
        "contextResponses": [{
          "contextElement": {
            "id": "${id}",
            "type": "${type}",
            "isPattern": "false",
            "attributes": [{ "name": "temperature", "type": "Number", "value": "${temperature}" }]
          },
          "statusCode": { "code": "200", "reasonPhrase": "OK" }
        }]
      }
    },
    "attrs": ["temperature"]
  }
}
```

> **Importante:** A URL da notificação é a interna do Docker (`http://sth-comet:8666/notify`), nunca o IP público: quem chama é o Orion, de dentro da EC2. E o formato é **`httpCustom` com JSON NGSIv1**, porque o STH-Comet só entende NGSIv1. A coleção pública usa `"attrsFormat": "legacy"`, que funciona no Orion 3.11 do compose, mas foi removido no Orion 4.0; montando o corpo NGSIv1 à mão no `httpCustom`, a inscrição funciona nas duas versões.
>
> **Uma inscrição por grandeza.** Inscrição repetida grava cada medida duas vezes no histórico e distorce as médias da supervisão.

Todas as requisições levam os cabeçalhos:

```text
Content-Type: application/json
fiware-service: smart
fiware-servicepath: /
```

### 7.2 Pela aplicação

Com o `appsettings.json` já apontando para o IP, abra a aplicação, entre com `admin` / `1234` e use **Salas › Novo registro** (`sala01`, `urn:ngsi-ld:SalaCirurgica:001`, `CIRURGIA01`, marcando **Criar as três inscrições**). A tela faz os mesmos `POST` acima por meio de `SalaDAO` e `InscricaoDAO`.

Ao migrar do Postman para a aplicação, **apague o cadastro feito pelo Postman** antes de recriar pela tela, para não duplicar o dispositivo nem as inscrições.

## Passo 8 — Conferir a entidade no Orion

Depois que o ESP32 estiver publicando (ou depois de uma medida simulada), a entidade deve existir com as três grandezas:

```bash
curl -s "http://<IP>:1026/v2/entities/urn:ngsi-ld:SalaCirurgica:001?options=keyValues" \
     -H "fiware-service: smart" -H "fiware-servicepath: /"
```

Resposta esperada (valores variam):

```json
{
  "id": "urn:ngsi-ld:SalaCirurgica:001",
  "type": "SalaCirurgica",
  "temperature": 23.5,
  "humidity": 58,
  "luminosity": 42,
  "alert": "off",
  "setAlert_status": "OK",
  "nome": "Sala 01"
}
```

Confira também o histórico:

```bash
curl -s "http://<IP>:8666/STH/v1/contextEntities/type/SalaCirurgica/id/urn:ngsi-ld:SalaCirurgica:001/attributes/temperature?lastN=5" \
     -H "fiware-service: smart" -H "fiware-servicepath: /"
```

## Passo 9 — Ligar o device

1. Atualize `MQTT_BROKER` no `secrets.h` do firmware com o **IP desta sessão** e grave a placa.
2. No Monitor Serial (115200), confira `[WIFI] conectado` e `[MQTT] conectado`.
3. A cada 5 s deve aparecer `[MEDIDAS] t|23.5|h|58|l|42` e, no Orion, a entidade deve mudar sozinha.

## Passo 10 — Testar o comando de alerta

Sem esperar a supervisão, mande um alerta direto pelo Orion:

```bash
curl -s -X PATCH "http://<IP>:1026/v2/entities/urn:ngsi-ld:SalaCirurgica:001/attrs?type=SalaCirurgica" \
     -H "Content-Type: application/json" \
     -H "fiware-service: smart" -H "fiware-servicepath: /" \
     -d '{"setAlert":{"type":"command","value":"temp"}}'
```

Fluxo esperado: o Orion repassa ao IoT Agent, que publica `sala01@setAlert|temp` em `/CIRURGIA01/sala01/cmd`; o ESP32 acende o padrão `temp` (400 ms aceso / 400 ms apagado, 1000 Hz), publica `a|temp` em `attrs` e confirma em `cmdexe`. O atributo `setAlert_status` passa a **OK** e `alert` fica `temp`.

Repita com `luz`, `umid` e `off`. Se `setAlert_status` ficar em **PENDING**, o comando chegou ao Orion mas não voltou do dispositivo: confira se o ESP32 está inscrito no tópico `/cmd` e se o `device_id` na mensagem é `sala01`.

## Passo 11 — Encerrar a sessão

Antes de sair, no terminal da EC2:

```bash
cd ~/fiware
sudo docker-compose down      # os volumes são preservados
```

Depois, na página do laboratório, **End Lab**. Isso encerra a sessão e para a instância.

> **Atenção:** `docker-compose down` preserva os dados (volumes). Ele só remove os containers. Para apagar tudo, veja *Master reset*.

## Master reset

Só quando for preciso começar do zero (dados corrompidos, dispositivo duplicado, apresentação):

```bash
cd ~/fiware
sudo docker-compose down
sudo docker volume rm fiware_mongo-historical-data fiware_mongo-internal-data
sudo docker-compose up -d
```

> **Atenção:** Isso apaga **todas** as entidades, inscrições, registros de dispositivo e séries históricas. Depois do reset, provisione a sala de novo (Passo 7).

## Roteiro de cada sessão (cola da equipe)

Na ordem, a cada vez que a sessão do Learner Lab é reiniciada:

1. **Start Lab** e esperar o círculo verde.
2. EC2 → **Start instance** (se estiver parada) → copiar o **novo IP público**.
3. SSH na EC2 → `cd ~/fiware && sudo docker-compose up -d` → `sudo docker-compose ps`.
4. Health check das três portas (Passo 6).
5. Conferir o dispositivo: `curl -s -H "fiware-service: smart" -H "fiware-servicepath: /" http://localhost:4041/iot/devices`. Se a lista vier vazia, provisionar (Passo 7).
6. Na aplicação, tela **Conexão**: colar o IP novo e salvar (grava no `appsettings.json` e vale na hora, sem reiniciar).
7. No firmware, atualizar `MQTT_BROKER` no `secrets.h` e regravar a placa.
8. Dashboard: conferir os três valores atualizando e provocar um alerta de cada grandeza.

## Problemas comuns

| Sintoma | Causa provável | O que fazer |
| --- | --- | --- |
| Aplicação: "O Orion não respondeu em 5 s" | IP da sessão mudou, EC2 parada ou porta fechada | Conferir o IP em **Conexão** e as regras do Security Group |
| ESP32: `[MQTT] falhou (rc=-2)` | IP errado no `secrets.h` ou porta 1883 fechada | Regravar o firmware com o IP atual; liberar 1883 |
| ESP32: `[MQTT] falhou (rc=4/5)` | Usuário/senha no broker | O `mosquitto.conf` do laboratório não usa autenticação: não preencha usuário |
| Orion: `setAlert_status` fica **PENDING** | O comando não voltou do ESP32 | Conferir `device_id`, tópico `/cmd` e a assinatura MQTT da placa |
| IoT Agent: `DUPLICATE_DEVICE_ID` | Sala já cadastrada (pelo Postman ou por outra sessão) | Usar a tela **Salas › Editar**, ou `DELETE /iot/devices/sala01` antes de recriar |
| Histórico vazio, estado atual preenchido | Inscrição faltando, inativa ou com URL errada | Recriar as três inscrições na tela **Inscrições** e conferir `http://sth-comet:8666/notify` |
| Histórico com valores dobrados | Inscrição duplicada para a mesma grandeza | Apagar as repetidas na tela **Inscrições** |
| `docker-compose up` falha por memória | t3.micro com seis containers | Usar t3.small ou parar containers não usados |
| `permission denied` ao rodar `docker` | Grupo `docker` não aplicado | Sair e entrar de novo no SSH (Passo 4) ou usar `sudo` |

## Referências

- [FIWARE Descomplicado](https://github.com/fabiocabrini/fiware) — imagens, `docker-compose.yml`, coleção do Postman.
- [FIWARE – IoT over MQTT (tutorial oficial)](https://github.com/FIWARE/tutorials.IoT-over-MQTT).
- [Orion Context Broker – API NGSI-v2](https://fiware-orion.readthedocs.io/).
- Arquitetura e fluxos deste projeto: [`arquitetura.md`](arquitetura.md) e [`arquitetura.svg`](arquitetura.svg).