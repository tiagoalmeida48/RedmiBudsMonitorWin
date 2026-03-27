# Redmi Buds Monitor

Aplicativo de bandeja (system tray) para Windows que exibe o nível de bateria
dos Redmi Buds 5 (fone esquerdo, fone direito e caixinha) em tempo real via
BLE advertisements.

---

## Funcionalidades

- Ícone na bandeja do sistema visível apenas quando o fone está conectado
- Ícone mostra o percentual mínimo de bateria quando está abaixo de 50%
- Popup ao clicar no ícone: exibe esquerdo, caixinha e direito com cores indicativas
- Indicador de carregamento (`⚡`) quando os fones estão dentro da caixinha
- Instância única (mutex) — não abre duplicado
- Atualização automática a cada 10 segundos

### Cores de bateria

| Nível      | Cor     |
|------------|---------|
| >= 50%     | Verde   |
| >= 20%     | Laranja |
| < 20%      | Vermelho|
| Indisponível | Cinza |

---

## Requisitos

- Windows 10 v1903+ (build 19041) ou Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Bluetooth LE no PC
- Redmi Buds 5 pareado no sistema

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
├── Program.cs                        entry point — STAThread, single instance, inicia TrayApp
├── RedmiBudsMonitor.csproj           WinExe, net10.0-windows10.0.19041.0, win-x64
├── Bluetooth/
│   ├── BleScanner.cs                 escuta BLE advertisements, filtra por Company ID
│   ├── BudsAdvertisement.cs          parseia o payload do advertisement (L/R/Case)
│   ├── BluetoothConnectionWatcher.cs monitora conexão/desconexão via DeviceWatcher
│   ├── EarbudData.cs                 record: Battery (byte) + InCase (bool)
│   └── CaseData.cs                   record: Battery (byte) + Charging (bool)
├── Domain/
│   ├── BatteryState.cs               estado thread-safe, agrega leituras do scanner
│   ├── BatterySnapshot.cs            snapshot imutável com L/R/Case + MinPercent
│   ├── BatteryEntry.cs               par (Pct, Label) por dispositivo
│   ├── BatteryColors.cs              extension methods em byte: IsValid, ToColor, ToLabel
│   └── BatteryDevice.cs              enum: Left, Case, Right
└── UI/
    ├── TrayApp.cs                    orquestra scanner, watcher, ícone e popup
    ├── TrayIconRenderer.cs           renderiza ícone 32×32 (headphone + % se < 50)
    └── BatteryPopup.cs               form 220×155 sem borda, próximo à bandeja
```

---

## Protocolo do advertisement

O scanner filtra `DataSections` do tipo `0xFF` (Manufacturer Specific Data) pelo
Company ID `0xFFFF`. O payload após os 2 bytes de Company ID deve começar com o
cabeçalho `0x16 0x01` e ter ao menos 8 bytes.

| Offset | Campo    | Máscara |
|--------|----------|---------|
| 5      | Esquerdo | `& 0x7F` = bateria; `& 0x80` = na caixinha |
| 6      | Direito  | `& 0x7F` = bateria; `& 0x80` = na caixinha |
| 7      | Caixinha | `& 0x7F` = bateria; `& 0x80` = carregando  |

Valor `0xFF` (ou > 100 após aplicar a máscara) indica dado indisponível.

### Detecção de carregamento dos fones

Um fone é considerado carregando quando:
- está dentro da caixinha (`InCase = true`)
- seu percentual é menor que 100%
- a caixinha tem bateria disponível (> 0%)
