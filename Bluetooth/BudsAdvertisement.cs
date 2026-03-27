namespace RedmiBudsMonitor;

internal sealed record BudsAdvertisement(EarbudData Left, EarbudData Right, CaseData Case)
{
    private const byte Header0 = 0x16;
    private const byte Header1 = 0x01;
    private const int MinPayloadLength = 8;
    private const int LeftIndex = 5;
    private const int RightIndex = 6;
    private const int CaseIndex = 7;
    private const byte BatteryMask = 0x7F;
    private const byte StatusBit = 0x80;

    public bool HasLeft => Left.Battery != BatterySnapshot.Unavailable;
    public bool HasRight => Right.Battery != BatterySnapshot.Unavailable;
    public bool HasCase => Case.Battery != BatterySnapshot.Unavailable;

    public static BudsAdvertisement? TryParse(byte[] payload)
    {
        if (payload.Length < MinPayloadLength) return null;
        if (payload[0] != Header0 || payload[1] != Header1) return null;

        var left = (byte)(payload[LeftIndex] & BatteryMask);
        var right = (byte)(payload[RightIndex] & BatteryMask);
        var caseVal = (byte)(payload[CaseIndex] & BatteryMask);

        if (!left.IsValid && !right.IsValid && !caseVal.IsValid) return null;

        return new BudsAdvertisement(
            Left: new EarbudData(left.IsValid ? left : BatterySnapshot.Unavailable, (payload[LeftIndex] & StatusBit) != 0),
            Right: new EarbudData(right.IsValid ? right : BatterySnapshot.Unavailable, (payload[RightIndex] & StatusBit) != 0),
            Case: new CaseData(caseVal.IsValid ? caseVal : BatterySnapshot.Unavailable, (payload[CaseIndex] & StatusBit) != 0));
    }
}