namespace DocxCore;

/// <summary>
/// 轻量图片头部解析器。仅读取宽高，不解码像素。
/// 支持 PNG、JPEG、GIF、BMP、TIFF。
/// 替代 SixLabors.ImageSharp / System.Drawing.Common 的外部依赖。
/// </summary>
public static class ImageHeaderReader
{
    /// <summary>从文件路径读取图片尺寸。</summary>
    public static (int Width, int Height) GetDimensions(string path)
    {
        using var fs = File.OpenRead(path);
        Span<byte> header = stackalloc byte[64];
        int read = fs.Read(header);
        if (read < 24) throw new InvalidDataException("File too small to be a valid image.");

        return header switch
        {
            // PNG: 89 50 4E 47 0D 0A 1A 0A
            [0x89, 0x50, 0x4E, 0x47, ..] => ReadPng(header),
            // JPEG: FF D8 FF
            [0xFF, 0xD8, 0xFF, ..] => ReadJpeg(fs),
            // GIF: GIF87a or GIF89a
            [0x47, 0x49, 0x46, 0x38, ..] => ReadGif(header),
            // BMP: 42 4D
            [0x42, 0x4D, ..] => ReadBmp(header),
            // TIFF: II (little-endian) or MM (big-endian)
            [0x49, 0x49, 0x2A, 0x00, ..] => ReadTiffLE(fs, header),
            [0x4D, 0x4D, 0x00, 0x2A, ..] => ReadTiffBE(fs, header),
            _ => throw new NotSupportedException("Unsupported image format. Supported: PNG, JPEG, GIF, BMP, TIFF."),
        };
    }

    // PNG: IHDR chunk at offset 16, width[4] height[4] big-endian
    static (int, int) ReadPng(Span<byte> h) => (ReadIntBE(h[16..20]), ReadIntBE(h[20..24]));

    // GIF: bytes 6-7=width, 8-9=height, little-endian
    static (int, int) ReadGif(Span<byte> h) => (ReadIntLE(h[6..8]), ReadIntLE(h[8..10]));

    // BMP: bytes 18-21=width, 22-25=height, little-endian
    static (int, int) ReadBmp(Span<byte> h) => (ReadIntLE(h[18..22]), Math.Abs(ReadIntLE(h[22..26])));

    // JPEG: scan for SOF marker (0xFF 0xC0..0xC3), read height[2] width[2] big-endian
    static (int, int) ReadJpeg(FileStream fs)
    {
        Span<byte> buf = stackalloc byte[2];
        while (true)
        {
            int n = fs.Read(buf);
            if (n < 2) throw new InvalidDataException("Invalid JPEG: unexpected end of file.");
            if (buf[0] != 0xFF) continue;
            byte marker = buf[1];
            if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3)
            {
                Span<byte> frame = stackalloc byte[7];
                if (fs.Read(frame) < 7) throw new InvalidDataException("Invalid JPEG: truncated SOF segment.");
                return (ReadIntBE(frame[5..7]), ReadIntBE(frame[3..5]));
            }
            if (marker is >= 0xD0 and <= 0xD7 or 0xD8 or 0x01) continue;
            if (fs.Read(buf) < 2) throw new InvalidDataException("Invalid JPEG: unexpected end of file.");
            int skipLen = (buf[0] << 8) | buf[1];
            fs.Seek(skipLen - 2, SeekOrigin.Current);
        }
    }

    // TIFF little-endian
    static (int, int) ReadTiffLE(FileStream fs, Span<byte> h)
    {
        int ifdOffset = ReadIntLE(h[4..8]);
        return ReadTiffEntries(fs, ifdOffset, littleEndian: true);
    }

    // TIFF big-endian
    static (int, int) ReadTiffBE(FileStream fs, Span<byte> h)
    {
        int ifdOffset = ReadIntBE(h[4..8]);
        return ReadTiffEntries(fs, ifdOffset, littleEndian: false);
    }

    static (int, int) ReadTiffEntries(FileStream fs, int ifdOffset, bool littleEndian)
    {
        fs.Seek(ifdOffset, SeekOrigin.Begin);
        Span<byte> buf = stackalloc byte[12];
        if (fs.Read(buf[..2]) < 2) throw new InvalidDataException("Invalid TIFF: unexpected end of file.");
        int numEntries = littleEndian ? ReadIntLE(buf[..2]) : ReadIntBE(buf[..2]);

        int? width = null, height = null;
        for (int i = 0; i < numEntries; i++)
        {
            if (fs.Read(buf) < 12) throw new InvalidDataException("Invalid TIFF: truncated IFD entry.");
            int tag = littleEndian ? ReadIntLE(buf[0..2]) : ReadIntBE(buf[0..2]);
            int value = littleEndian ? ReadIntLE(buf[8..12]) : ReadIntBE(buf[8..12]);
            if (tag == 0x0100) width = value;
            else if (tag == 0x0101) height = value;
            if (width.HasValue && height.HasValue) return (width.Value, height.Value);
        }
        throw new InvalidDataException("Invalid TIFF: ImageWidth or ImageLength tag not found.");
    }

    static int ReadIntBE(Span<byte> b) => b.Length switch
    {
        2 => (b[0] << 8) | b[1],
        4 => (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3],
        _ => throw new ArgumentException(),
    };

    static int ReadIntLE(Span<byte> b) => b.Length switch
    {
        2 => b[0] | (b[1] << 8),
        4 => b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24),
        _ => throw new ArgumentException(),
    };
}
