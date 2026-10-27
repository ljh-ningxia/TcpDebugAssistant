using System.Text;

namespace TcpDebugAssistant;

/// <summary>
/// 数据格式（ASCII / HEX）转换与解析工具。
/// </summary>
public static class DataConvert
{
    /// <summary>严格 UTF-8 解码器（无效序列抛异常，用于编码探测）。</summary>
    private static readonly Encoding Utf8Strict = Encoding.GetEncoding("utf-8", new EncoderExceptionFallback(), new DecoderExceptionFallback());

    /// <summary>严格 GBK 解码器（国产设备常用编码；编码提供器未注册时为 null）。</summary>
    private static readonly Encoding? GbkStrict = TryCreateStrict(936);

    /// <summary>宽松 GBK 编码器（发送用，无法编码的字符替换为 '?'）。</summary>
    private static readonly Encoding? GbkLoose = TryCreate(936);

    private static Encoding? TryCreateStrict(int codePage)
    {
        try
        {
            return Encoding.GetEncoding(codePage, new EncoderExceptionFallback(), new DecoderExceptionFallback());
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static Encoding? TryCreate(int codePage)
    {
        try
        {
            return Encoding.GetEncoding(codePage);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

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

    /// <summary>
    /// 按文本显示：自动识别 UTF-8 / GBK 编码（国产设备常用 GBK），
    /// 控制字符替换为 '.'；两种编码均无法按文本解析时退化为逐字节显示。
    /// </summary>
    public static string BytesToAscii(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return string.Empty;
        }

        var hasHigh = false;
        foreach (var b in data)
        {
            if (b >= 0x80)
            {
                hasHigh = true;
                break;
            }
        }

        // 纯 ASCII（含 \r\n 等控制字符替换）
        if (!hasHigh)
        {
            return SanitizeBytes(data);
        }

        // 含高位字节：依次按“UTF-8 中文 → GBK 中文 → UTF-8 → GBK”判定。
        // 先要求解出中文，避免把 GBK 汉字（如“你”= C4 E3）误判成 UTF-8 的拉丁扩展字符。
        if (TryDecode(data, Utf8Strict, out var utf8Text) && ContainsCjk(utf8Text))
        {
            return SanitizeText(utf8Text);
        }

        if (TryDecode(data, GbkStrict, out var gbkText) && ContainsCjk(gbkText))
        {
            return SanitizeText(gbkText);
        }

        if (TryDecode(data, Utf8Strict, out var utf8Any))
        {
            return SanitizeText(utf8Any);
        }

        if (TryDecode(data, GbkStrict, out var gbkAny))
        {
            return SanitizeText(gbkAny);
        }

        // 二进制数据：逐字节兜底
        return SanitizeBytes(data);
    }

    /// <summary>把待发送文本按指定编码转为字节（GBK 用于只认国标码的设备）。</summary>
    public static byte[] EncodeText(string text, bool gbk)
    {
        var enc = gbk ? (GbkLoose ?? Encoding.UTF8) : Encoding.UTF8;
        return enc.GetBytes(text);
    }

    /// <summary>尝试把 HEX 字符串解析为字节数组。</summary>
    public static bool TryParseHex(string text, out byte[] data)
    {
        data = Array.Empty<byte>();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        // 去掉常见分隔符（空格、逗号、换行、0x 前缀、连字符）
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

    private static bool TryDecode(ReadOnlySpan<byte> data, Encoding? enc, out string text)
    {
        text = string.Empty;
        if (enc is null)
        {
            return false;
        }

        try
        {
            text = enc.GetString(data);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>是否包含 CJK 汉字（基本区 + 扩展 A）。</summary>
    private static bool ContainsCjk(string text)
    {
        foreach (var ch in text)
        {
            if (ch is >= '\u4E00' and <= '\u9FFF' || ch is >= '\u3400' and <= '\u4DBF')
            {
                return true;
            }
        }

        return false;
    }

    private static string SanitizeBytes(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder(data.Length);
        foreach (var b in data)
        {
            sb.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
        }

        return sb.ToString();
    }

    /// <summary>控制字符（保留 \t \r \n）替换为 '.'，避免破坏日志排版。</summary>
    private static string SanitizeText(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            sb.Append(ch is '\t' or '\r' or '\n' || (ch >= ' ' && ch != '\u007F') ? ch : '.');
        }

        return sb.ToString();
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
