#pragma once
// =====================================================================
//  Copie este arquivo para secrets.h e preencha.
//  secrets.h NÃO vai para o GitHub (está no .gitignore).
// =====================================================================

// Wi-Fi: o ESP32 só conecta em rede 2,4 GHz
// (iPhone: ative "Maximizar Compatibilidade"; Android: banda de 2,4 GHz)
#define WIFI_SSID      "nome-da-rede"
#define WIFI_PASSWORD  "senha-da-rede"

// IP público da instância EC2 do Learner Lab (muda a cada sessão!)
// O Analista 1 passa o IP novo; atualize aqui e grave de novo.
#define MQTT_BROKER    "0.0.0.0"
#define MQTT_PORT      1883
