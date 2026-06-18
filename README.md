# TrayBatt

Aplicativo de bandeja (system tray) para Windows que exibe o nível de bateria
dos Redmi Buds 5 (fone esquerdo, fone direito e caixinha) em tempo real via
BLE advertisements, além da bateria de outros dispositivos Bluetooth conectados.

---

## Funcionalidades

- Ícone na bandeja do sistema sempre visível (cinza quando os Buds estão
  desconectados, branco quando conectados)
- Ícone mostra o percentual mínimo de bateria quando está abaixo de 50%
- Popup ao clicar no ícone: exibe esquerdo, caixinha e direito com cores
  indicativas e a tag de estado (`em uso` / `na caixa`)
- Seção "Outros dispositivos": bateria de qualquer dispositivo Bluetooth
  pareado e conectado (mouse, teclado, etc.)
- Indicador de carregamento (`⚡`) quando os fones estão dentro da caixinha
- Instância única (mutex) — não abre duplicado
- Atualização automática a cada 10 segundos
- Persiste o último nível conhecido para mostrar algo plausível ao iniciar

### Cores de bateria

| Nível      | Cor     |
|------------|---------|
| >= 50%     | Verde   |
| >= 20%     | Laranja |
| < 20%      | Vermelho|
| Indisponível | Cinza |

---

## Desenvolvido com IA

Este projeto foi construído com assistência do [Claude Code](https://claude.com/claude-code).
A parte mais interessante não foi escrever C#, e sim a **engenharia reversa do
protocolo BLE** dos Redmi Buds 5 — que não é documentado: os offsets de bateria,
os bits de estado (na caixa / carregando / tampa) e a estratégia de cruzar duas
fontes de bateria (advertisements BLE + leitura agregada do Windows) foram
descobertos observando os bytes crus no ar enquanto se manipulava os fones.

Esse conhecimento ficou versionado como ferramentas de IA reutilizáveis, dentro
de `.claude/` (commitado de propósito, não ignorado):

| Arquivo | O que é |
|---------|---------|
| [`CLAUDE.md`](CLAUDE.md) | Guia de arquitetura para qualquer instância de IA que trabalhe no repo |
| [`.claude/skills/ble-probe/`](.claude/skills/ble-probe/SKILL.md) | Skill que documenta como inspecionar advertisements crus e mapear offsets do protocolo |
| [`.claude/agents/ble-protocol-analyzer.md`](.claude/agents/ble-protocol-analyzer.md) | Subagente que analisa dumps de payload e deduz/valida offsets contra o parser |
| [`tools/BudsProbe`](tools/BudsProbe) | Utilitário de console que alimenta a skill com dados reais |

> A config pessoal (`.claude/settings.local.json`) fica fora do git; o
> `.claude/settings.json` versionado traz só permissões seguras de build/run.

---

## Requisitos

- Windows 10 v1903+ (build 19041) ou Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Bluetooth LE no PC
- Redmi Buds 5 pareado no sistema

---

## Build

```bash
dotnet build TrayBatt.slnx -c Release

# Rodar diretamente
dotnet run --project src/TrayBatt.csproj

# Executável standalone (sem precisar do runtime instalado)
dotnet publish src/TrayBatt.csproj -c Release -r win-x64 --self-contained
```

---

## Estrutura

```
TrayBatt/
├── TrayBatt.slnx                        solução (formato XML)
├── src/
│   ├── Program.cs                        entry point — STAThread, single instance, inicia TrayApp
│   ├── TrayBatt.csproj                   WinExe, net10.0-windows10.0.19041.0, win-x64
│   ├── Bluetooth/
│   │   ├── BleScanner.cs                 escuta BLE advertisements, filtra por Company ID
│   │   ├── BudsAdvertisement.cs          parseia o payload do advertisement (L/R/Case/tampa)
│   │   ├── BluetoothConnectionWatcher.cs monitora conexão/desconexão dos Buds via DeviceWatcher
│   │   ├── BluetoothBatteryEnumerator.cs lê bateria de dispositivos conectados via PnP/GATT (WinRT)
│   │   ├── EarbudData.cs                 record: Battery (byte) + InCase (bool)
│   │   └── CaseData.cs                   record: Battery (byte) + Charging (bool)
│   ├── Domain/
│   │   ├── BatteryState.cs               estado thread-safe, funde leituras das duas fontes
│   │   ├── BatterySnapshot.cs            snapshot imutável com L/R/Case + MinPercent
│   │   ├── BatteryEntry.cs               (Pct, Label, InCase) por componente
│   │   ├── BatteryDevice.cs             enum: Left, Case, Right
│   │   ├── DeviceBattery.cs              (Name, Pct) de um dispositivo Bluetooth genérico
│   │   ├── BatteryStore.cs               persiste o último nível em %LocalAppData%\TrayBatt
│   │   └── BatteryColors.cs              extension methods em byte: IsValid, ToColor, ToLabel
│   └── UI/
│       ├── TrayApp.cs                    orquestra scanner, watcher, enumerador, ícone e popup
│       ├── TrayIconRenderer.cs           renderiza ícone 32×32 (headphone + % se < 50)
│       └── BatteryPopup.cs               form sem borda desenhado em GDI+, próximo à bandeja
└── tools/
    └── BudsProbe/                        utilitário de console para inspecionar os advertisements crus
```

---

## Fontes de bateria

A bateria dos Buds vem de **duas fontes independentes que convergem em
`BatteryState`**:

- **BLE advertisements** (`BleScanner`): fonte principal e detalhada — bateria
  de cada fone, da caixinha e os bits de estado (na caixa, carregando, tampa).
  Chega por push, mas o valor de um fone "trava" quando ele entra na caixa.
- **Enumerador WinRT** (`BluetoothBatteryEnumerator`): consulta o Windows
  (PnP e, em fallback, GATT Battery Service) a cada 10s. Dá um número agregado
  confiável dos Buds quando estão em uso, e alimenta a lista "outros
  dispositivos".

`BatteryState` resolve o conflito: só sobrescreve a bateria de um fone com o
número agregado quando ele **não** está na caixinha.

---

## Protocolo do advertisement

O scanner filtra `DataSections` do tipo `0xFF` (Manufacturer Specific Data) pelo
Company ID `0xFFFF`. O payload após os 2 bytes de Company ID deve começar com o
cabeçalho `0x16 0x01` e ter ao menos 8 bytes.

| Offset | Campo    | Bateria  | Bit de status              |
|--------|----------|----------|----------------------------|
| 3      | Tampa    | —        | `& 0x01` = tampa aberta    |
| 5      | Esquerdo | `& 0x7F` | `& 0x80` = na caixinha     |
| 6      | Direito  | `& 0x7F` | `& 0x80` = na caixinha     |
| 7      | Caixinha | `& 0x7F` | `& 0x80` = carregando      |

Valor `0xFF` (ou > 100 após aplicar a máscara) indica dado indisponível.

Os offsets foram descobertos por engenharia reversa. Para inspecionar os
advertisements crus ao mexer no parser, use a ferramenta de diagnóstico:

```bash
dotnet run --project tools/BudsProbe/BudsProbe.csproj
```

### Detecção de carregamento dos fones

Um fone é considerado carregando quando:
- está dentro da caixinha (`InCase = true`)
- seu percentual é menor que 100%
- a caixinha tem bateria disponível (> 0%)
