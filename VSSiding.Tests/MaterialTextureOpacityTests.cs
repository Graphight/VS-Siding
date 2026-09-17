using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Xunit;

namespace VSSiding.Tests;

public class MaterialTextureOpacityTests
{
    private static readonly string[] AssetDomainFolders = ["survival", "game", "creative"];

    private static string GetAssemblyMetadata(string key)
    {
        var value = typeof(MaterialTextureOpacityTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value;
        Assert.False(string.IsNullOrEmpty(value), $"AssemblyMetadata '{key}' was not set at build time");
        return value!;
    }

    private static IEnumerable<string> CollectTextureCodes(JObject wallJson, string dictName)
    {
        var dict = (JObject)wallJson["attributes"]![dictName]!;
        foreach (var entry in dict.Properties())
            yield return (string)entry.Value["Texture"]!;
    }

    private static string ResolveTextureFile(string vintageStoryPath, string textureCode)
    {
        var parts = textureCode.Split(':', 2);
        var relativePath = parts[1] + ".png";
        foreach (var domainFolder in AssetDomainFolders)
        {
            var candidate = Path.Combine(vintageStoryPath, "assets", domainFolder, "textures", relativePath);
            if (File.Exists(candidate))
                return candidate;
        }
        throw new FileNotFoundException($"Could not find texture file for '{textureCode}' under any of: {string.Join(", ", AssetDomainFolders)}");
    }

    [Fact]
    public void NoMaterialTextureHasPartialAlpha()
    {
        var vintageStoryPath = GetAssemblyMetadata("VintageStoryPath");
        var repoRoot = GetAssemblyMetadata("RepoRoot");
        var wallJsonPath = Path.Combine(repoRoot, "VSSiding", "assets", "vssiding", "blocktypes", "wall.json");
        var wallJson = (JObject)JToken.Parse(File.ReadAllText(wallJsonPath));

        var textureCodes = CollectTextureCodes(wallJson, "Framings")
            .Concat(CollectTextureCodes(wallJson, "Infills"))
            .Concat(CollectTextureCodes(wallJson, "Finishes"))
            .Distinct();

        var offenders = new List<string>();
        foreach (var textureCode in textureCodes)
        {
            var file = ResolveTextureFile(vintageStoryPath, textureCode);
            if (!PngOpacity.HasNoPartialAlpha(file, out var reason))
                offenders.Add($"{textureCode} ({reason})");
        }

        Assert.Equal(Array.Empty<string>(), offenders);
    }
}

internal static class PngOpacity
{
    public static bool HasNoPartialAlpha(string path, out string reason)
    {
        var bytes = File.ReadAllBytes(path);
        var (width, height, colorType, bitDepth, idat) = ReadChunks(bytes);

        if (colorType == 2 || colorType == 0)
        {
            reason = "";
            return true;
        }
        if (colorType != 6 || bitDepth != 8)
        {
            reason = $"unsupported PNG color type {colorType} at bit depth {bitDepth}";
            return false;
        }

        var raw = Inflate(idat);
        var pixels = Unfilter(raw, width, height, bytesPerPixel: 4);

        int partial = 0;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            var alpha = pixels[i + 3];
            if (alpha != 0 && alpha != 255)
                partial++;
        }

        if (partial > 0)
        {
            reason = $"{partial} pixels with partial alpha";
            return false;
        }
        reason = "";
        return true;
    }

    private static (int width, int height, int colorType, int bitDepth, byte[] idat) ReadChunks(byte[] bytes)
    {
        const int signatureLength = 8;
        int offset = signatureLength;
        int width = 0, height = 0, colorType = 0, bitDepth = 0;
        using var idatStream = new MemoryStream();

        while (offset < bytes.Length)
        {
            int length = ReadInt32BigEndian(bytes, offset);
            var type = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, 4);
            int dataOffset = offset + 8;

            if (type == "IHDR")
            {
                width = ReadInt32BigEndian(bytes, dataOffset);
                height = ReadInt32BigEndian(bytes, dataOffset + 4);
                bitDepth = bytes[dataOffset + 8];
                colorType = bytes[dataOffset + 9];
            }
            else if (type == "IDAT")
            {
                idatStream.Write(bytes, dataOffset, length);
            }
            else if (type == "IEND")
            {
                break;
            }

            offset = dataOffset + length + 4;
        }

        return (width, height, colorType, bitDepth, idatStream.ToArray());
    }

    private static int ReadInt32BigEndian(byte[] bytes, int offset) =>
        (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

    private static byte[] Inflate(byte[] zlibData)
    {
        using var input = new MemoryStream(zlibData);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] Unfilter(byte[] raw, int width, int height, int bytesPerPixel)
    {
        int stride = width * bytesPerPixel;
        var pixels = new byte[stride * height];
        int rawOffset = 0;

        for (int y = 0; y < height; y++)
        {
            var filterType = raw[rawOffset];
            rawOffset++;
            int rowOffset = y * stride;
            int priorRowOffset = (y - 1) * stride;

            for (int x = 0; x < stride; x++)
            {
                byte value = raw[rawOffset + x];
                byte a = x >= bytesPerPixel ? pixels[rowOffset + x - bytesPerPixel] : (byte)0;
                byte b = y > 0 ? pixels[priorRowOffset + x] : (byte)0;
                byte c = (y > 0 && x >= bytesPerPixel) ? pixels[priorRowOffset + x - bytesPerPixel] : (byte)0;

                byte recon = filterType switch
                {
                    0 => value,
                    1 => (byte)(value + a),
                    2 => (byte)(value + b),
                    3 => (byte)(value + (a + b) / 2),
                    4 => (byte)(value + Paeth(a, b, c)),
                    _ => throw new NotSupportedException($"Unsupported PNG filter type {filterType}")
                };
                pixels[rowOffset + x] = recon;
            }

            rawOffset += stride;
        }

        return pixels;
    }

    private static byte Paeth(byte a, byte b, byte c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return a;
        if (pb <= pc) return b;
        return c;
    }
}
