# Redmi Buds 5 Monitor

Console app Windows para leitura de bateria dos fones Redmi Buds 5 e engenharia
reversa do protocolo da caixinha via BLE advertisements.

---

## Requisitos

- Windows 10 v1903+ (build 18362) ou Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Bluetooth LE no PC

---

## Build

```bash
dotnet build -c Release

# Executável standalone (sem precisar do runtime instalado)
dotnet publish -c Release -r win-x64 --self-contained -o publish/
```

---

## Estrutura

```
RedmiBudsMonitor/
├── Program.cs                   entry point
├── RedmiBudsMonitor.csproj      net10.0-windows10.0.19041.0
└── src/
    ├── Logger.cs                output colorido com barra de bateria
    ├── BleScanner.cs            captura advertisements + filtra Xiaomi
    ├── BudsAdvertisement.cs     model + análise de candidatos
    └── GattBatteryReader.cs     leitura GATT + enumeração de serviços
```

---

## O que o app faz

### BLE Advertisement Scanner
Filtra `DataSections` tipo `0xFF` (Manufacturer Specific Data) pelo Company ID
`0x038F` (Xiaomi). Loga os bytes brutos completos para análise.

```
[RAW]  AA:BB:CC:DD:EE:FF |  -65 dBm | raw(16): 8F 03 4C 01 ...
[RAW]    payload(14): 4C 01 07 19 01 00 58 5A 47 ...
[DATA]   offset [ 6] =  88  ← candidato
[DATA]   offset [ 7] =  90  ← candidato
[DATA]   offset [ 8] =  71  ← candidato
```

### GATT Battery Service (0x180F)
Se o fone implementar o serviço padrão, exibe a bateria com notificações
em tempo real. O Redmi Buds 5 frequentemente **não** implementa este serviço —
nesse caso a bateria está no advertisement.

### Enumeração GATT completa
Lista todos os serviços e características, incluindo proprietários Xiaomi,
com os valores brutos de cada característica legível.

---

## Engenharia reversa da caixinha

| Passo | Ação |
|-------|------|
| 1 | Execute com caixinha em ~80% → anote os `offset [N]` candidatos |
| 2 | Descarregue a caixinha para ~50% → execute novamente |
| 3 | O offset que foi de ~80 para ~50 é o byte da caixinha |
| 4 | Repita para confirmar o esquerdo e o direito |

Após confirmar, edite `BudsAdvertisement.TryParse` em `BudsAdvertisement.cs`:

```csharp
BatteryLeftC1  = payload[OFFSET_LEFT],
BatteryRightC1 = payload[OFFSET_RIGHT],
BatteryCaseC1  = payload[OFFSET_CASE],
```

---

## Protocolo MiBeacon (referência)

| Offset | Bytes | Campo                  |
|--------|-------|------------------------|
| 0–1    | 2     | Frame control          |
| 2–3    | 2     | Device type            |
| 4      | 1     | Frame counter          |
| 5–10   | 6     | MAC address (reversed) |
| 11+    | var   | Capability + Object data |

O Object data contém status e bateria — os offsets variam por modelo/firmware.