#pragma once
// =====================================================================
//  Monitor de Sala Cirúrgica — configurações do firmware
//  Wi-Fi e IP do servidor ficam em secrets.h (fora do GitHub)
// =====================================================================

// ---------- Identidade no FIWARE (Contrato do Guia dos desenvolvedores) ----------
// Têm que ser IGUAIS ao cadastro feito pela aplicação (tela Salas) ou pelo Postman.
// Errado aqui = o IoT Agent ignora as mensagens sem avisar.
#define APIKEY     "CIRURGIA01"
#define DEVICE_ID  "sala01"

// ---------- Pinos ----------
#define PIN_DHT         4    // DATA do DHT11
#define DHT_TYPE        DHT11   // troque para DHT22 se usar o sensor mais preciso
#define PIN_LDR         34   // ponto do meio do divisor LDR + 10 kΩ (ADC1: funciona com Wi-Fi)
#define PIN_BUZZER      13   // buzzer PASSIVO, com resistor de 100 Ω
#define PIN_LED_R       25   // LED RGB: vermelho (fica apagado no N2)
#define PIN_LED_G       26   // LED RGB: verde (fica apagado no N2)
#define PIN_LED_B       27   // LED RGB: azul = LED de alerta
#define PIN_STATUS_LED  2    // LED da placa: aceso = conectado ao FIWARE
#define LED_COMMON_ANODE  false   // true se o LED RGB for de ânodo comum (perna longa no 3V3)

// ---------- Padrões de alerta (iguais à tabela do README) ----------
// meio ciclo = tempo aceso = tempo apagado; o buzzer toca enquanto o LED está aceso
#define ALERTA_LUZ_MS    150
#define ALERTA_LUZ_HZ    2000
#define ALERTA_TEMP_MS   400
#define ALERTA_TEMP_HZ   1000
#define ALERTA_UMID_MS   1000
#define ALERTA_UMID_HZ   500
#define LED_ALERTA_BRILHO  255    // 0-255

// ---------- LDR ----------
// Divisor do esquema: 3V3 -> LDR -> GPIO34 -> 10 kΩ -> GND (mais luz = valor maior).
// Calibração: cubra o sensor; a luminosidade deve CAIR. Se subir, use true.
#define LDR_INVERT   false
#define LDR_SAMPLES  8              // média de N leituras (reduz ruído)

// ---------- Tempos ----------
#define MEDIDA_INTERVAL_MS      5000    // medidas a cada 5 s (o DHT11 aceita no máximo 1 por segundo)
#define HEARTBEAT_INTERVAL_MS   30000   // reenvia o alerta em vigor a cada 30 s
#define MQTT_RETRY_INTERVAL_MS  5000
#define MQTT_SOCKET_TIMEOUT_S   5

// ---------- PWM ----------
#define LED_PWM_FREQ  5000   // Hz
#define LED_PWM_BITS  8      // resolução 0-255
