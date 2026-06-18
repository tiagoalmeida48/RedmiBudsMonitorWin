---
name: ble-probe
description: Inspecionar advertisements BLE crus dos Redmi Buds e mapear offsets do protocolo. Use ao alterar o parser BudsAdvertisement, ao suportar um novo modelo de fone, ou quando a bateria/estado lido estiver errado e for preciso confirmar o byte cru no ar.
---

# BLE Probe — engenharia reversa do protocolo dos Buds

O protocolo de advertisement dos Redmi Buds 5 **não é documentado**. Os offsets
em `src/Bluetooth/BudsAdvertisement.cs` foram descobertos observando os bytes
crus enquanto se manipulava fisicamente os fones (tirar/pôr na caixa, abrir a
tampa, deixar carregando). Esta skill captura esse processo para não ter que
redescobri-lo do zero.

## Quando usar

- O parser está lendo bateria ou estado errado e você precisa ver o byte cru.
- Vai dar suporte a outro modelo de fone (offsets podem mudar).
- Vai mexer em `BudsAdvertisement.TryParse` e quer validar contra dados reais.

## Ferramenta

`tools/BudsProbe` é um app de console que escuta os advertisements e imprime,
para cada payload novo, os bytes relevantes já decodificados:

```bash
dotnet run --project tools/BudsProbe/BudsProbe.csproj
```

Ele só imprime quando o payload **muda** (dedup por endereço), então para forçar
broadcast: mexa fisicamente nos fones — tirar/pôr na caixa, abrir/fechar a tampa.

## Estrutura conhecida do payload

Filtro de captura (ver `BleScanner` e `BudsProbe`):
1. `DataSection.DataType == 0xFF` (Manufacturer Specific Data)
2. Company ID `0xFFFF` (primeiros 2 bytes, little-endian)
3. Payload (após o company id) começa com `0x16 0x01` e tem ≥ 8 bytes

Layout do payload:

| Offset | Campo    | Bateria  | Bit de status            |
|--------|----------|----------|--------------------------|
| 3      | Tampa    | —        | `& 0x01` = tampa aberta  |
| 5      | Esquerdo | `& 0x7F` | `& 0x80` = na caixinha   |
| 6      | Direito  | `& 0x7F` | `& 0x80` = na caixinha   |
| 7      | Caixinha | `& 0x7F` | `& 0x80` = carregando    |

Convenções do domínio (ver `BatteryColors` e `BatterySnapshot`):
- Bateria = `byte & 0x7F`. O bit alto (`0x80`) é **estado**, nunca bateria.
- `0xFF`, ou qualquer valor `> 100` após a máscara, significa **indisponível**.

## Procedimento para mapear um offset novo / desconhecido

1. Rode o `BudsProbe` e capture a linha `payload:` em **um estado conhecido**
   (ex.: ambos os fones em uso, caixa fechada, bateria que você leu no app do
   fabricante).
2. Mude **uma** variável física por vez (tire só o esquerdo, abra só a tampa,
   ponha pra carregar) e capture de novo. Compare quais bytes mudaram.
3. Para distinguir bateria de flag: o nibble que acompanha o valor numérico em
   `0..100` é bateria; bits que alternam 0/1 conforme um estado físico são flags.
4. Confirme a hipótese repetindo o ciclo. Só então edite as constantes de
   offset em `src/Bluetooth/BudsAdvertisement.cs`.

## Cuidados

- Bateria de fone na caixinha **trava** no último valor antes de entrar — por
  isso o app cruza com a leitura agregada do Windows (ver
  `BatteryState.ApplyConnectedBattery`). Não confunda valor travado com bug.
- Não invente offset sem dado real: registre o ciclo de teste que confirmou.
- Se for criar/editar lógica de parsing complexa a partir de dumps, delegue ao
  agent `ble-protocol-analyzer`.
