namespace RedmiBudsMonitor;

internal sealed record BudsAdvertisement(byte BatteryLeft, byte BatteryRight, byte BatteryCase, bool IsCaseCharging, bool IsLeftInCase, bool IsRightInCase)
{
    private const byte Unavailable = 0xFF;

    public bool HasLeft => BatteryLeft != Unavailable;
    public bool HasRight => BatteryRight != Unavailable;
    public bool HasCase => BatteryCase != Unavailable;

    public static BudsAdvertisement? TryParse(byte[] payload)
    {
        if (payload.Length < 8) return null;
        if (payload[0] != 0x16 || payload[1] != 0x01) return null;

        var left = (byte)(payload[5] & 0x7F);
        var right = (byte)(payload[6] & 0x7F);
        var ccase = (byte)(payload[7] & 0x7F);

        var leftValid = left <= 100;
        var rightValid = right <= 100;
        var caseValid = ccase <= 100;

        if (!leftValid && !rightValid && !caseValid) return null;

        return new BudsAdvertisement(
            BatteryLeft: leftValid ? left : Unavailable,
            BatteryRight: rightValid ? right : Unavailable,
            BatteryCase: caseValid ? ccase : Unavailable,
            IsCaseCharging: (payload[7] & 0x80) != 0,
            IsLeftInCase: (payload[5] & 0x80) != 0,
            IsRightInCase: (payload[6] & 0x80) != 0);
    }
}