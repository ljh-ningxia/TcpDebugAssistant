using System.Text;

namespace TcpDebugAssistant;

/// <summary>
/// 数据格式（ASCII / HEX）转换与解析工具。
/// </summary>
public static class DataConvert
{
    /// <summary>按 HEX 格式显示：每字节以空格分隔的两位十六进制。</summary>
    public static string BytesToHex(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder(data.Length * 3);
        for (var i = 0; i < data.Length; i++)
        {
            sb.Append(data[i].ToString("X2"));
            sb.Append(' ');
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>按 ASCII 显示，不可打印字符替换为 '.'。</summary>
    public static string BytesToAscii(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder(data.Length);
        foreach (var b in data)
        {
            sb.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
        }

        return sb.ToString();
    }

    /// <summary>尝试把用户输入的字符串按指定格式解析为字节数组。</summary>
    public static bool TryParseInput(string text, DisplayFormat format, out byte[] data)
    {
        data = Array.Empty<byte>();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (format == DisplayFormat.Ascii)
        {
            data = Encoding.UTF8.GetBytes(text);
            return true;
        }

        // HEX：去掉常见分隔符（空格、逗号、换行、0x 前缀、连字符）
        var hex = text
            .Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("0X", string.Empty, StringComparison.OrdinalIgnoreCase);

        var sb = new StringBuilder(hex.Length);
        foreach (var ch in hex)
        {
            if (Uri.IsHexDigit(ch))
            {
                sb.Append(ch);
            }
        }

        var compact = sb.ToString();
        if (compact.Length == 0 || compact.Length % 2 != 0)
        {
            return false;
        }

        var bytes = new byte[compact.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(compact.AsSpan(i * 2, 2).ToString(), 16);
        }

        data = bytes;
        return true;
    }
}

/// <summary>显示格式。</summary>
public enum DisplayFormat
{
    /// <summary>ASCII 文本。</summary>
    Ascii,

    /// <summary>十六进制字节。</summary>
    Hex
}
