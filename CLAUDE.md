# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## O que é

App de bandeja (system tray) Windows que mostra a bateria dos Redmi Buds 5
(fone esquerdo, direito e caixinha) em tempo real, mais a bateria de outros
dispositivos Bluetooth conectados. WinForms sobre .NET 10, usando as APIs
WinRT do Windows (`Windows.Devices.Bluetooth`).

> Nota: o `README.md` está **desatualizado** — referencia o nome antigo
> `RedmiBudsMonitor`/`.slnx` e não cobre o enumerador WinRT nem a seção "outros
> dispositivos". O código (namespace `TrayBatt`) é a fonte de verdade.

## Comandos

```bash
# Build
dotnet build TrayBatt.slnx -c Release

# Rodar o app
dotnet run --project src/TrayBatt.csproj

# Executável standalone (não exige runtime instalado)
dotnet publish src/TrayBatt.csproj -c Release -r win-x64 --self-contained

# Ferramenta de diagnóstico: imprime os advertisements BLE crus dos Buds
# (offsets, byte da tampa, estado em-uso/na-caixa). Use ao mexer no parser.
dotnet run --project tools/BudsProbe/BudsProbe.csproj
```

Não há testes. Requer Windows 10 v1903+ (build 19041), Bluetooth LE e os Buds
pareados. O target framework (`net10.0-windows10.0.19041.0`) é o que dá acesso
às APIs WinRT — não baixe a versão do SDK do Windows no TFM.

## Arquitetura

O ponto central é que a bateria dos Buds vem de **duas fontes independentes que
convergem em `BatteryState`**, e há três threads envolvidas. Entender essa
fusão é o que torna o resto do código legível.

### Fluxo de dados

```
BleScanner ───────────► BatteryState.Update()          (thread do watcher BLE)
  (advertisements BLE,     fonte rica: L/R/caixa separados,
   passivo, push)          flags na-caixa / carregando / tampa

BluetoothBatteryEnumerator ─► BatteryState.ApplyConnectedBattery()   (Task pool)
  (poll a cada 10s via       fonte pobre: só 1 % do dispositivo "Redmi Buds";
   GATT/PnP WinRT, pull)      aplicado a L e R só se NÃO estiverem na caixa

BluetoothConnectionWatcher ─► _budsConnected            (thread do DeviceWatcher)
  (eventos add/update/remove de conexão)
```

`TrayApp` orquestra tudo, faz a fusão final em `RefreshUi()` e empurra para a UI
via `SynchronizationContext.Post` (a única forma segura de tocar no `NotifyIcon`
e no `BatteryPopup` a partir das threads de background).

### Por que duas fontes

- **BLE advertisements** (`BleScanner` → `BudsAdvertisement.TryParse`) são a
  fonte principal e detalhada: dão bateria de cada fone, da caixinha, e os bits
  de estado (na-caixa, carregando, tampa aberta). Mas só chegam quando os Buds
  fazem broadcast, e o valor de um fone fica "preso" quando ele entra na caixa.
- **`BluetoothBatteryEnumerator.QueryConnectedAsync`** consulta o Windows
  (PnP `{104EA319-...} 2` e, em fallback, GATT Battery Service) por bateria de
  *qualquer* dispositivo pareado-e-conectado. Para os Buds isso dá só um número
  agregado, mas é confiável quando os fones estão em uso. Esse mesmo poll também
  alimenta a lista "outros dispositivos" do popup.

`BatteryState` resolve o conflito: mantém o último valor BLE por componente e,
em `ApplyConnectedBattery`, só sobrescreve L/R com o número agregado quando
aquele fone **não** está na caixa (`_lastLeftInCase`/`_lastRightInCase`). Toda
mutação é protegida por `Lock`; leitores chamam `Snapshot()` que copia sob lock.

### Convenções de domínio

- **`0xFF` = "indisponível"** em todo lugar (`BatterySnapshot.Unavailable`).
  Nunca use 255 como bateria real. `byte.IsValid()` (em `BatteryColors`) é
  `pct <= 100` e é o gate canônico antes de usar qualquer valor.
- Os percentuais reais cabem em 7 bits porque o bit alto (`0x80`) carrega
  estado: nos fones = "na caixa", na caixinha = "carregando". O parser sempre
  aplica `& 0x7F` para bateria e testa `& 0x80` para o status.
- `Color`/`Label` por percentual ficam centralizados em `BatteryColors`
  (extension methods em `byte`): `ToColor()` e `ToLabel(charging)`. Não
  reimplemente os limiares de cor (verde ≥50, laranja ≥20, vermelho <20) em
  outro lugar.

### Protocolo do advertisement BLE

Filtro: `DataSection` tipo `0xFF` (Manufacturer Specific) com Company ID
`0xFFFF`. Após os 2 bytes de company id, o payload precisa começar com
`0x16 0x01` e ter ≥8 bytes. Offsets dentro do payload (ver `BudsAdvertisement`):

| Offset | Campo    | Bateria   | Bit `0x80` / `0x01`        |
|--------|----------|-----------|----------------------------|
| 3      | Tampa    | —         | `& 0x01` = tampa aberta    |
| 5      | Esquerdo | `& 0x7F`  | `& 0x80` = na caixa        |
| 6      | Direito  | `& 0x7F`  | `& 0x80` = na caixa        |
| 7      | Caixinha | `& 0x7F`  | `& 0x80` = carregando      |

Ao alterar o parser, valide contra dados reais com `tools/BudsProbe` — os
offsets foram descobertos por engenharia reversa, não documentados.

### Persistência

`BatteryStore` salva os últimos níveis válidos em
`%LocalAppData%\TrayBatt\battery-state.json` para que o popup mostre algo
plausível logo na inicialização, antes do primeiro advertisement. É um
"baseline", best-effort; toda I/O é envolvida em try/catch silencioso (padrão
do projeto: falha de Bluetooth/arquivo nunca derruba o app).

### UI

- `TrayIconRenderer.Render` desenha um ícone 32×32 (headphone) e sobrepõe o
  percentual **só quando o mínimo geral < 50%**. Ele faz P/Invoke de
  `DestroyIcon` — o handle GDI do ícone precisa ser destruído manualmente; ao
  trocar `_tray.Icon`, sempre faça `Dispose()` do antigo (ver `RefreshUi`).
- `BatteryPopup` é um `Form` sem borda desenhado inteiro em `OnPaint` (GDI+).
  A altura é dinâmica conforme a contagem de "outros dispositivos"
  (`ApplyLayout`). Fecha em `OnDeactivate`.

## Convenções de código

- Tudo `internal sealed`, namespace único `TrayBatt`, file-scoped. `Nullable` e
  `ImplicitUsings` ligados.
- Trabalho de background (BLE, WinRT, I/O) **engole exceções de propósito** —
  esse é o contrato, não um descuido. A UI degrada para "indisponível" em vez
  de crashar. Mantenha esse padrão ao adicionar chamadas a APIs do Windows.
- Cross-thread para a UI passa **sempre** por `_ctx.Post`. Estado compartilhado
  lido por múltiplas threads usa `volatile` (snapshots imutáveis) ou `Lock`
  (`BatteryState`).
```