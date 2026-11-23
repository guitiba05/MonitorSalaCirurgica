# Firmware do ESP32: Monitor de Sala Cirúrgica

O ESP32 mede temperatura e umidade (DHT11) e luminosidade (LDR) a cada 5 s, envia as medidas ao FIWARE por MQTT e obedece ao comando `setAlert` da aplicação, acionando o LED azul e o buzzer num padrão diferente para cada grandeza. Quem decide o alerta é a aplicação ASP.NET Core; o ESP32 só mede e obedece.

## Arquivos

| Arquivo | Conteúdo |
| --- | --- |
| `sala_cirurgica.ino` | Programa principal |
| `config.h` | Identidade no FIWARE (`APIKEY`, `DEVICE_ID`), pinos, tempos e padrões de alerta |
| `secrets.example.h` | Modelo do Wi-Fi e do IP do servidor; copiar para `secrets.h` |

## Pinagem

| Componente | Pino | Observação |
| --- | --- | --- |
| DHT11 (DATA) | GPIO 4 | VCC no 3V3, GND no GND |
| LDR | GPIO 34 | Divisor: 3V3 → LDR → GPIO 34 → 10 kΩ → GND |
| LED RGB | GPIO 25 (R), 26 (G), 27 (B) | Só o azul é usado; resistores de 220 Ω (R) e 100 Ω (G, B) |
| Buzzer passivo | GPIO 13 | Resistor de 100 Ω em série |
| LED da placa | GPIO 2 | Aceso = conectado ao FIWARE; piscando = offline |

## Como gravar

1. Na Arduino IDE, instalar a placa **esp32 by Espressif Systems** (versão 2.x ou 3.x) e as bibliotecas **PubSubClient** (Nick O'Leary), **DHT sensor library** e **Adafruit Unified Sensor** (Adafruit).
2. Copiar `secrets.example.h` para `secrets.h` e preencher o Wi-Fi (2,4 GHz) e o IP público atual da EC2.
3. Abrir `sala_cirurgica.ino`, escolher **ESP32 Dev Module** e a porta, e clicar em **Carregar**.
4. Abrir o Monitor Serial em **115200**, com **Nova linha**.

## O que aparece ao ligar

O LED pisca vermelho, verde e azul, e o buzzer toca três bipes, do mais grave ao mais agudo (500, 1000 e 2000 Hz). Depois aparecem `[WIFI] conectado` e `[MQTT] conectado`, e a cada 5 s uma linha `[MEDIDAS] t|23.5|h|58|l|42`.

| Se acontecer | Causa provável |
| --- | --- |
| Cores em outra ordem | Fios do LED trocados entre D25, D26 e D27 |
| LED aceso quando deveria apagar | LED de ânodo comum: `LED_COMMON_ANODE true` no `config.h` |
| Os três bipes com o mesmo som | Buzzer ativo; o projeto precisa de um **passivo** |
| `[DHT11] leitura inválida` | Pinos do módulo trocados (siga a marcação −, +, S) ou fio solto |
| Luminosidade sobe ao cobrir o LDR | LDR montado do lado do GND: `LDR_INVERT true` |
| `[MQTT] falhou (rc=-2)` | IP errado no `secrets.h`, instância desligada ou porta 1883 fechada |

## Mensagens MQTT

| Direção | Tópico | Exemplo |
| --- | --- | --- |
| Publica, a cada 5 s | `/CIRURGIA01/sala01/attrs` | `t\|23.5\|h\|58\|l\|42` |
| Publica, ao mudar e a cada 30 s | `/CIRURGIA01/sala01/attrs` | `a\|luz` |
| Recebe | `/CIRURGIA01/sala01/cmd` | `sala01@setAlert\|temp` |
| Confirma | `/CIRURGIA01/sala01/cmdexe` | `sala01@setAlert\|ok temp` |

## Padrões de alerta

| Alerta | LED azul | Buzzer |
| --- | --- | --- |
| `luz` | 150 ms aceso, 150 ms apagado | 2000 Hz |
| `temp` | 400 ms aceso, 400 ms apagado | 1000 Hz |
| `umid` | 1 s aceso, 1 s apagado | 500 Hz |
| `off` | Apagado | Desligado |

O buzzer toca enquanto o LED está aceso. Os tempos e as frequências ficam no `config.h`.

## Console de testes

Digite no Monitor Serial:

| Comando | O que faz |
| --- | --- |
| `a luz`, `a temp`, `a umid`, `a off` | Aciona o alerta sem depender da aplicação |
| `status` | Mostra as últimas medidas, o alerta e a conexão |
| `teste` | Repete o teste de cores e bipes |
