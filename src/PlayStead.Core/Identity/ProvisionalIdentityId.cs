using System.Buffers.Binary;
using System.Security.Cryptography;

namespace PlayStead.Core.Identity;

public readonly record struct ProvisionalIdentityId
{
    private const string Prefix = "PS-TEMP-";
    private const string Alphabet =
        "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int UlidLength = 26;

    private ProvisionalIdentityId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ProvisionalIdentityId New() =>
        New(DateTimeOffset.UtcNow);

    public static ProvisionalIdentityId New(
        DateTimeOffset timestampUtc)
    {
        var timestamp = timestampUtc.ToUnixTimeMilliseconds();

        if (timestamp is < 0 or > 0xFFFFFFFFFFFFL)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timestampUtc));
        }

        Span<byte> bytes = stackalloc byte[16];
        bytes[0] = (byte)(timestamp >> 40);
        bytes[1] = (byte)(timestamp >> 32);
        bytes[2] = (byte)(timestamp >> 24);
        bytes[3] = (byte)(timestamp >> 16);
        bytes[4] = (byte)(timestamp >> 8);
        bytes[5] = (byte)timestamp;
        RandomNumberGenerator.Fill(bytes[6..]);

        var value = BinaryPrimitives.ReadUInt128BigEndian(bytes);
        Span<char> encoded = stackalloc char[UlidLength];

        for (var index = UlidLength - 1;
             index >= 0;
             index--)
        {
            encoded[index] = Alphabet[(int)(value & 31)];
            value >>= 5;
        }

        return new ProvisionalIdentityId(
            string.Concat(Prefix, encoded));
    }

    public static ProvisionalIdentityId Parse(string value)
    {
        if (!TryParse(value, out var result))
        {
            throw new FormatException(
                $"Invalid provisional identity id: '{value}'.");
        }

        return result;
    }

    public static bool TryParse(
        string? value,
        out ProvisionalIdentityId result)
    {
        result = default;

        if (value is null ||
            value.Length != Prefix.Length + UlidLength ||
            !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var ulid = value.AsSpan(Prefix.Length);

        if (ulid[0] is < '0' or > '7')
        {
            return false;
        }

        foreach (var character in ulid)
        {
            if (!Alphabet.Contains(character))
            {
                return false;
            }
        }

        result = new ProvisionalIdentityId(value);
        return true;
    }

    public override string ToString() =>
        Value;
}
