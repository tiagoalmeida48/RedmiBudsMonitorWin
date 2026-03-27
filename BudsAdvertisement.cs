namespace RedmiBudsMonitor;

/// <summary>
/// Payload do Redmi Buds 5 — protocolo confirmado via engenharia reversa.
///
/// Company ID : 0xFFFF
/// Header     : payload[0]=0x16, payload[1]=0x01
///
/// Layout confirmado:
///   [0]  = 0x16  — identificador fixo
///   [1]  = 0x01  — identificador fixo
///   [2]  = 0x18  — varia em alguns estados
///   [3]  = flags de status (charging, in-ear, in-case — bits a confirmar)
///   [4]  = counter / flags secundários
///   [5]  = bateria esquerdo (0–100)  ✓ confirmado
///   [6]  = bateria direito  (0–100)  ✓ confirmado
///   [7]  = bateria caixinha (0–100)  ✓ confirmado  (0 = na caixa/indisponível)
///   [8+] = MAC + outros
///
/// BITS DE CHARGING (byte[3]) — a confirmar via debug:
///   Hipótese baseada em protocolo BLE earbuds padrão:
///   bit 0 = esquerdo carregando
///   bit 1 = direito carregando
///   bit 2 = caixinha carregando
///   bit 3 = esquerdo na caixa
///   bit 4 = direito na caixa
/// </summary>
internal sealed record BudsAdvertisement(
    byte  BatteryLeft,
    byte  BatteryRight,
    byte  BatteryCase,
    bool  IsLeftCharging,
    bool  IsRightCharging,
    bool  IsCaseCharging,
    bool  IsLeftInCase,
    bool  IsRightInCase,
    byte  FlagByte,
    byte[] RawPayload)
{
    private const byte Unavailable = 0xFF;

    public bool HasCase    => BatteryCase != Unavailable;
    public bool HasLeft    => BatteryLeft  != Unavailable;
    public bool HasRight   => BatteryRight != Unavailable;

    public static BudsAdvertisement? TryParse(byte[] payload)
    {
        if (payload.Length < 8) return null;
        if (payload[0] != 0x16 || payload[1] != 0x01) return null;

        byte flagByte = payload[3];

        // Bits 0-6 = percentual de bateria (0-100)
        // Bit  7   = flag de status por canal (ex: na caixa / carregando)
        // Sem a máscara 0x7F, valores como 0xCB (75%) e 0xE4 (100%) seriam
        // lidos como 203 e 228, incorretamente descartados como inválidos.
        byte left  = (byte)(payload[5] & 0x7F);
        byte right = (byte)(payload[6] & 0x7F);
        byte ccase = (byte)(payload[7] & 0x7F);

        // MSB de cada byte de bateria — a confirmar se é "in-case" ou "charging"
        bool leftMsb  = (payload[5] & 0x80) != 0;
        bool rightMsb = (payload[6] & 0x80) != 0;
        bool caseMsb  = (payload[7] & 0x80) != 0;

        // 0x7F = 127 = byte inválido/ausente após mascarar 0xFF
        // 0–100 = valores válidos
        bool leftValid  = left  <= 100;
        bool rightValid = right <= 100;
        bool caseValid  = ccase <= 100;

        // Descarta packets sem nenhum valor útil
        if (!leftValid && !rightValid && !caseValid) return null;

        // Fones: MSB do byte de bateria = na caixa (confirmado)
        bool leftInCase  = leftMsb;
        bool rightInCase = rightMsb;

        // Caixinha: MSB do byte [7] = USB conectado / carregando
        bool caseCharging = caseMsb;

        // Bits do flagByte[3] ainda não confirmados — preservados para investigação
        bool leftCharging  = (flagByte & 0x01) != 0;
        bool rightCharging = (flagByte & 0x02) != 0;

        return new BudsAdvertisement(
            BatteryLeft:    leftValid  ? left  : Unavailable,
            BatteryRight:   rightValid ? right : Unavailable,
            BatteryCase:    caseValid  ? ccase : Unavailable,
            IsLeftCharging:  leftCharging,
            IsRightCharging: rightCharging,
            IsCaseCharging:  caseCharging,
            IsLeftInCase:    leftInCase,
            IsRightInCase:   rightInCase,
            FlagByte:        flagByte,
            RawPayload:      payload
        );
    }

    public void Print(byte lastCase = 0xFF)
    {
        Logger.Section("Redmi Buds 5");

        byte caseDisplay = HasCase ? BatteryCase : lastCase;

        PrintEarbud("Esquerdo", BatteryLeft,  IsLeftInCase,  caseDisplay);
        PrintEarbud("Direito ", BatteryRight, IsRightInCase, caseDisplay);

        if (caseDisplay <= 100)
            PrintCase("Caixinha", caseDisplay, IsCaseCharging);
        else
            Logger.Warn("Caixinha : aguardando leitura...");
    }

    // Fone na caixa + caixa > 10% → "carregando"
    // Fone na caixa + bateria < 100% + caixinha > 0% → "⚡ carregando"
    // Qualquer outro caso                            → apenas a porcentagem
    private static void PrintEarbud(string name, byte battery, bool inCase, byte casePercent)
    {
        if (battery > 100)
        {
            Logger.Warn($"{name} : indisponível");
            return;
        }

        string status = inCase && battery < 100 && casePercent > 0
            ? " ⚡ carregando"
            : string.Empty;

        Logger.Battery($"{name}{status}", battery);
    }

    // Caixinha: indica se está carregando via USB
    private static void PrintCase(string name, byte battery, bool charging)
    {
        if (battery > 100)
        {
            Logger.Warn($"{name} : indisponível");
            return;
        }

        string status = charging ? " ⚡ carregando" : string.Empty;

        Logger.Battery($"{name}{status}", battery);
    }
}