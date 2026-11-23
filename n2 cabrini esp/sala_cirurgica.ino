/*
 * Monitor de Sala Cirúrgica — firmware do ESP32
 * ---------------------------------------------------------------------
 * Projeto N2 · Sistemas Embarcados (Prof. Fábio Cabrini) + Linguagem de Programação I
 * Faculdade Engenheiro Salvador Arena (FESA)
 *
 * O QUE ESTE PROGRAMA FAZ
 *   1. Mede temperatura e umidade (DHT11) e luminosidade (LDR) a cada 5 s.
 *   2. Envia as três medidas ao FIWARE por MQTT (IoT Agent UltraLight 2.0).
 *   3. Recebe o comando setAlert da aplicação ASP.NET Core (via Orion e IoT Agent)
 *      e aciona o alarme da sala: LED azul piscando + buzzer, num padrão diferente
 *      para cada grandeza. Quem DECIDE o alerta é a aplicação, pelas médias;
 *      o ESP32 só mede e obedece.
 *
 * CONTRATO MQTT (igual ao Guia dos desenvolvedores)
 *   publica  /CIRURGIA01/sala01/attrs   t|23.5|h|58|l|42     medidas, a cada 5 s
 *   publica  /CIRURGIA01/sala01/attrs   a|luz                alerta em vigor (ao mudar e a cada 30 s)
 *   assina   /CIRURGIA01/sala01/cmd     sala01@setAlert|temp comando da aplicação
 *   publica  /CIRURGIA01/sala01/cmdexe  sala01@setAlert|ok temp  confirmação (status OK no Orion)
 *
 * PADRÕES DE ALERTA (o buzzer toca enquanto o LED está aceso)
 *   luz   prioridade 1   LED 150 ms aceso / 150 ms apagado   2000 Hz
 *   temp  prioridade 2   LED 400 ms / 400 ms                 1000 Hz
 *   umid  prioridade 3   LED 1 s / 1 s                       500 Hz
 *   off   normalidade    LED apagado, buzzer desligado
 *   (A prioridade é aplicada pela aplicação: ela manda só o alerta que vale.)
 *
 * PINOS (config.h)
 *   DHT11 DATA -> GPIO 4 · LDR (divisor com 10 kΩ) -> GPIO 34 · Buzzer passivo -> GPIO 13
 *   LED RGB: R -> GPIO 25, G -> GPIO 26, B -> GPIO 27 (o azul é o LED de alerta)
 *
 * CONSOLE DE TESTES (Monitor Serial, 115200, "Nova linha")
 *   a luz | a temp | a umid | a off | status | teste
 *
 * BIBLIOTECAS (Arduino IDE > Gerenciar Bibliotecas)
 *   PubSubClient (Nick O'Leary) · DHT sensor library (Adafruit) · Adafruit Unified Sensor
 *   Placa: ESP32 Dev Module (pacote "esp32 by Espressif Systems", versão 2.x ou 3.x)
 */
#include <WiFi.h>
#include <PubSubClient.h>
#include <DHT.h>
#include <math.h>
#include "config.h"
#if __has_include("esp_arduino_version.h")
#include "esp_arduino_version.h"
#endif
#ifndef ESP_ARDUINO_VERSION_MAJOR
#define ESP_ARDUINO_VERSION_MAJOR 2
#endif
#if __has_include("secrets.h")
#include "secrets.h"
#else
#error "Crie secrets.h a partir de secrets.example.h (Wi-Fi e IP do servidor)."
#endif

// =====================================================================
//  Tópicos MQTT (UltraLight 2.0): /<apikey>/<device_id>/...
// =====================================================================
const String TOPIC_BASE   = String("/") + APIKEY + "/" + DEVICE_ID;
const String TOPIC_ATTRS  = TOPIC_BASE + "/attrs";    // medidas e alerta (ESP32 -> FIWARE)
const String TOPIC_CMD    = TOPIC_BASE + "/cmd";      // comandos (FIWARE -> ESP32)
const String TOPIC_CMDEXE = TOPIC_BASE + "/cmdexe";   // confirmações (ESP32 -> FIWARE)

// =====================================================================
//  Alertas: um padrão por grandeza
// =====================================================================
enum Alerta { ALERTA_OFF, ALERTA_LUZ, ALERTA_TEMP, ALERTA_UMID };

struct Padrao {
  const char *nome;      // valor usado no comando e no atributo "a"
  uint16_t meioCicloMs;  // tempo aceso = tempo apagado
  uint16_t freqHz;       // tom do buzzer enquanto o LED está aceso
};

// Índice = valor do enum Alerta
const Padrao PADROES[] = {
  {"off",  0,                 0},
  {"luz",  ALERTA_LUZ_MS,     ALERTA_LUZ_HZ},
  {"temp", ALERTA_TEMP_MS,    ALERTA_TEMP_HZ},
  {"umid", ALERTA_UMID_MS,    ALERTA_UMID_HZ},
};

// =====================================================================
//  Estado do programa
// =====================================================================
WiFiClient   wifiClient;
PubSubClient mqtt(wifiClient);
DHT          dht(PIN_DHT, DHT_TYPE);

Alerta alerta = ALERTA_OFF;        // alerta em vigor (último comando recebido)
unsigned long alertaT0 = 0;        // quando o alerta atual começou (base do piscar)
int    faseAtual = -1;             // 1 = LED aceso, 0 = apagado, -1 = ainda não desenhado
bool   alertaParaPublicar = true;  // o atributo "a" precisa ser enviado ao FIWARE

float  temperatura = NAN;          // °C (NAN = sem leitura válida ainda)
float  umidade = NAN;              // %
int    luminosidade = 0;           // 0-100 %

String clientId;
String serialBuf;
unsigned long tMedida = 0, tHeartbeat = 0, tMqttRetry = 0;

// =====================================================================
//  Saídas: LED RGB (só o azul é usado) e buzzer, por PWM (LEDC)
//  Canais fixos: R=0, G=1, B=2 e buzzer=4. O buzzer fica num timer
//  separado dos LEDs, então mudar o tom não interfere no LED.
// =====================================================================
const uint8_t LED_PINS[3] = {PIN_LED_R, PIN_LED_G, PIN_LED_B};
#define CANAL_BUZZER 4

void saidasBegin() {
  for (int i = 0; i < 3; i++) {
#if ESP_ARDUINO_VERSION_MAJOR >= 3
    ledcAttachChannel(LED_PINS[i], LED_PWM_FREQ, LED_PWM_BITS, i);
#else
    ledcSetup(i, LED_PWM_FREQ, LED_PWM_BITS);
    ledcAttachPin(LED_PINS[i], i);
#endif
  }
#if ESP_ARDUINO_VERSION_MAJOR >= 3
  ledcAttachChannel(PIN_BUZZER, 2000, LED_PWM_BITS, CANAL_BUZZER);
#else
  ledcSetup(CANAL_BUZZER, 2000, LED_PWM_BITS);
  ledcAttachPin(PIN_BUZZER, CANAL_BUZZER);
#endif
}

// Escreve um canal do LED (0 = R, 1 = G, 2 = B) com valor 0-255
void ledCanal(int i, uint32_t duty) {
  if (LED_COMMON_ANODE) duty = 255 - duty;   // ânodo comum acende com nível baixo
#if ESP_ARDUINO_VERSION_MAJOR >= 3
  ledcWrite(LED_PINS[i], duty);
#else
  ledcWrite(i, duty);
#endif
}

void ledRGB(uint8_t r, uint8_t g, uint8_t b) {
  ledCanal(0, r); ledCanal(1, g); ledCanal(2, b);
}

// Liga o buzzer na frequência dada; 0 silencia
void buzzer(uint32_t freqHz) {
#if ESP_ARDUINO_VERSION_MAJOR >= 3
  ledcWriteTone(PIN_BUZZER, freqHz);
#else
  ledcWriteTone(CANAL_BUZZER, freqHz);
#endif
}

// Desenha a fase do alarme: LED azul aceso + tom, ou tudo apagado
void desenharFase(bool aceso) {
  ledRGB(0, 0, aceso ? LED_ALERTA_BRILHO : 0);
  buzzer(aceso ? PADROES[alerta].freqHz : 0);
}

// Chamado a cada volta do loop: troca de fase quando passa o meio ciclo (sem delay)
void atualizarAlarme() {
  int fase = 0;
  if (alerta != ALERTA_OFF) {
    unsigned long t = millis() - alertaT0;
    fase = ((t / PADROES[alerta].meioCicloMs) % 2 == 0) ? 1 : 0;
  }
  if (fase != faseAtual) {          // só mexe no hardware quando a fase muda
    faseAtual = fase;
    desenharFase(fase == 1);
  }
}

void definirAlerta(Alerta novo) {
  if (novo != alerta) {
    alerta = novo;
    alertaT0 = millis();
    faseAtual = -1;                 // força redesenhar já na próxima volta do loop
    alertaParaPublicar = true;
  }
  Serial.printf("[ALERTA] %s\n", PADROES[alerta].nome);
}

// Converte "luz", "temp", "umid" ou "off" (com ou sem aspas) no enum; false se inválido
bool lerAlerta(String v, Alerta &saida) {
  v.trim();
  v.replace("\"", "");
  for (int i = 0; i < 4; i++) {
    if (v == PADROES[i].nome) { saida = (Alerta)i; return true; }
  }
  return false;
}

// =====================================================================
//  Sensores
// =====================================================================
// LDR: divisor 3V3 -> LDR -> GPIO34 -> 10 kΩ -> GND. Mais luz = tensão maior.
// A média de LDR_SAMPLES leituras (ADC de 12 bits, 0-4095) vira 0-100 %.
int lerLuminosidade() {
  uint32_t soma = 0;
  for (int i = 0; i < LDR_SAMPLES; i++) soma += analogRead(PIN_LDR);
  int bruto = soma / LDR_SAMPLES;
  int pct = map(bruto, 0, 4095, 0, 100);
  return LDR_INVERT ? 100 - pct : pct;
}

// DHT11: a biblioteca devolve NAN quando a leitura falha (fio solto, pino errado).
// Leitura inválida não substitui a última boa: só é registrada no Serial.
void lerDHT() {
  float t = dht.readTemperature();   // °C
  float h = dht.readHumidity();      // %
  if (isnan(t) || isnan(h)) {
    Serial.println("[DHT11] leitura inválida: confira os fios (−, +, S) e o pino");
    return;
  }
  temperatura = t;
  umidade = h;
}

// =====================================================================
//  Publicação (ESP32 -> FIWARE)
// =====================================================================
// Envia as medidas numa linha só: t|23.5|h|58|l|42. Sem DHT válido, envia só l.
void publicarMedidas() {
  luminosidade = lerLuminosidade();
  lerDHT();

  char payload[48];
  if (isnan(temperatura) || isnan(umidade)) {
    snprintf(payload, sizeof(payload), "l|%d", luminosidade);
  } else {
    snprintf(payload, sizeof(payload), "t|%.1f|h|%.0f|l|%d", temperatura, umidade, luminosidade);
  }
  Serial.printf("[MEDIDAS] %s\n", payload);
  if (mqtt.connected()) mqtt.publish(TOPIC_ATTRS.c_str(), payload);
}

// Envia o alerta em vigor (atributo "alert" no Orion). A aplicação usa esse valor
// para confirmar que o ESP32 realmente está mostrando o alerta que ela pediu.
bool publicarAlerta() {
  if (!mqtt.connected()) return false;
  String payload = String("a|") + PADROES[alerta].nome;
  Serial.printf("[MQTT] %s -> %s\n", TOPIC_ATTRS.c_str(), payload.c_str());
  return mqtt.publish(TOPIC_ATTRS.c_str(), payload.c_str());
}

// =====================================================================
//  Comandos (aplicação -> Orion -> IoT Agent -> MQTT -> ESP32)
// =====================================================================
String executarComando(const String &cmd, const String &valor) {
  if (cmd == "setAlert") {
    Alerta novo;
    if (!lerAlerta(valor, novo)) return "erro: use luz, temp, umid ou off";
    definirAlerta(novo);
    return String("ok ") + PADROES[novo].nome;
  }
  return "erro: comando desconhecido";
}

// Chamada pela biblioteca quando chega mensagem num tópico assinado
void onMqttMessage(char *topic, byte *payload, unsigned int length) {
  if (TOPIC_CMD != topic) return;
  String msg;
  for (unsigned int i = 0; i < length; i++) msg += (char)payload[i];
  Serial.printf("[CMD] recebido: %s\n", msg.c_str());

  // Formato UltraLight: <device_id>@<comando>|<valor>
  int arroba = msg.indexOf('@');
  int barra  = msg.indexOf('|', arroba + 1);
  if (arroba < 0 || barra < 0 || msg.substring(0, arroba) != DEVICE_ID) return;
  String cmd = msg.substring(arroba + 1, barra);
  String resultado = executarComando(cmd, msg.substring(barra + 1));

  // SEMPRE confirmar: é isso que muda setAlert_status de PENDING para OK no Orion
  String ack = String(DEVICE_ID) + "@" + cmd + "|" + resultado;
  mqtt.publish(TOPIC_CMDEXE.c_str(), ack.c_str());
  Serial.printf("[CMD] confirmado: %s\n", ack.c_str());
}

// =====================================================================
//  Console de testes no Monitor Serial
// =====================================================================
void testeSaidas();

void tratarSerial() {
  while (Serial.available()) {
    char ch = (char)Serial.read();
    if (ch != '\n' && ch != '\r') { if (serialBuf.length() < 20) serialBuf += ch; continue; }
    serialBuf.trim();
    if (serialBuf.startsWith("a ")) {
      Serial.println("[CONSOLE] " + executarComando("setAlert", serialBuf.substring(2)));
    } else if (serialBuf == "status") {
      Serial.printf("[STATUS] temp %.1f °C | umid %.0f %% | luz %d %% | alerta %s | Wi-Fi %s | MQTT %s\n",
                    temperatura, umidade, luminosidade, PADROES[alerta].nome,
                    WiFi.status() == WL_CONNECTED ? "ok" : "desconectado",
                    mqtt.connected() ? "conectado" : "desconectado");
    } else if (serialBuf == "teste") {
      testeSaidas();
    } else if (serialBuf.length()) {
      Serial.println("[CONSOLE] use: a luz, a temp, a umid, a off, status, teste");
    }
    serialBuf = "";
  }
}

// =====================================================================
//  Conectividade (não bloqueante: nunca trava o alarme nem as medidas)
// =====================================================================
void manterConexoes() {
  static bool wifiEstavaOk = false;
  bool wifiOk = (WiFi.status() == WL_CONNECTED);
  if (wifiOk != wifiEstavaOk) {
    wifiEstavaOk = wifiOk;
    if (wifiOk) Serial.printf("[WIFI] conectado, IP %s\n", WiFi.localIP().toString().c_str());
    else        Serial.println("[WIFI] desconectado, tentando reconectar...");
  }
  if (!wifiOk || mqtt.connected()) return;

  unsigned long agora = millis();
  if (tMqttRetry != 0 && agora - tMqttRetry < MQTT_RETRY_INTERVAL_MS) return;
  tMqttRetry = agora;

  Serial.printf("[MQTT] conectando a %s:%d...\n", MQTT_BROKER, MQTT_PORT);
  if (mqtt.connect(clientId.c_str())) {
    mqtt.subscribe(TOPIC_CMD.c_str());
    alertaParaPublicar = true;      // ressincroniza o alerta no Orion depois de reconectar
    Serial.printf("[MQTT] conectado. Escutando %s\n", TOPIC_CMD.c_str());
  } else {
    Serial.printf("[MQTT] falhou (rc=%d). Nova tentativa em %d s\n",
                  mqtt.state(), MQTT_RETRY_INTERVAL_MS / 1000);
  }
}

// LED da placa (GPIO 2): aceso = conectado ao FIWARE; piscando = offline
void atualizarLedStatus() {
  digitalWrite(PIN_STATUS_LED, mqtt.connected() ? HIGH : (millis() / 500) % 2);
}

// Teste de ligação: R, G, B e os três tons do buzzer (do mais grave ao mais agudo).
// Usa delay de propósito: só roda ao ligar a placa ou pelo comando "teste".
void testeSaidas() {
  const char *cores[3] = {"vermelho (R)", "verde (G)", "azul (B)"};
  for (int i = 0; i < 3; i++) {
    Serial.printf("[TESTE] LED %s\n", cores[i]);
    ledRGB(i == 0 ? 255 : 0, i == 1 ? 255 : 0, i == 2 ? 255 : 0);
    delay(350);
  }
  ledRGB(0, 0, 0);
  const uint16_t tons[3] = {ALERTA_UMID_HZ, ALERTA_TEMP_HZ, ALERTA_LUZ_HZ};
  for (int i = 0; i < 3; i++) {
    Serial.printf("[TESTE] buzzer %d Hz\n", tons[i]);
    buzzer(tons[i]);
    delay(200);
    buzzer(0);
    delay(80);
  }
  faseAtual = -1;                   // devolve o LED e o buzzer ao estado do alerta atual
}

// =====================================================================
void setup() {
  Serial.begin(115200);
  delay(200);
  Serial.println("\n=== Monitor de Sala Cirúrgica (ESP32 + FIWARE) ===");

  saidasBegin();
  pinMode(PIN_STATUS_LED, OUTPUT);
  analogReadResolution(12);
  dht.begin();

  // Teste de ligação: se as cores saírem em outra ordem, os fios do LED estão trocados;
  // se os três bipes tiverem o mesmo som, o buzzer é ativo (precisa ser passivo).
  testeSaidas();
  definirAlerta(ALERTA_OFF);

  clientId = "salacir-" + String((uint32_t)ESP.getEfuseMac(), HEX);

  WiFi.mode(WIFI_STA);
  WiFi.setAutoReconnect(true);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
  Serial.printf("[WIFI] conectando a '%s'...\n", WIFI_SSID);

  mqtt.setServer(MQTT_BROKER, MQTT_PORT);
  mqtt.setCallback(onMqttMessage);
  mqtt.setSocketTimeout(MQTT_SOCKET_TIMEOUT_S);
  Serial.println("[CONSOLE] comandos de teste: a luz, a temp, a umid, a off, status, teste");
}

void loop() {
  manterConexoes();
  mqtt.loop();
  tratarSerial();

  unsigned long agora = millis();
  if (agora - tMedida >= MEDIDA_INTERVAL_MS) { tMedida = agora; publicarMedidas(); }
  if (agora - tHeartbeat >= HEARTBEAT_INTERVAL_MS) { tHeartbeat = agora; alertaParaPublicar = true; }
  if (alertaParaPublicar && publicarAlerta()) alertaParaPublicar = false;

  atualizarAlarme();
  atualizarLedStatus();
}
