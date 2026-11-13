using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Anjian.Core.Imaging;

public sealed record ImageResourceEntry(
    string Id, string Name, string RelativePath, string Sha256,
    int Width, int Height, long FileSize, DateTimeOffset AddedAtUtc);

public sealed record ImageResourceManifest(
    int SchemaVersion, string ScriptId, DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ImageResourceEntry> Resources);

public sealed record ImageResourceValidation(
    string ResourceId, string Name, string RelativePath,
    string Status, string Message);

/// <summary>
/// Script-local PNG catalog. Paths are relative to the script directory and are confined
/// to the matching .assets folder; every validation checks file size, PNG dimensions and SHA-256.
/// </summary>
public sealed class ImageResourceCatalog
{
    public const int CurrentSchemaVersion = 1;
    public const long MaximumResourceFileBytes = 128L * 1024 * 1024;
    private readonly string _scriptPath;
    private readonly string _manifestPath;
    private readonly string _assetDirectory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public ImageResourceCatalog(string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
            throw new ArgumentException("脚本路径不能为空。", nameof(scriptPath));
        _scriptPath = Path.GetFullPath(scriptPath);
        var directory = Path.GetDirectoryName(_scriptPath)!;
        var stem = Path.GetFileNameWithoutExtension(_scriptPath);
        _manifestPath = Path.Combine(directory, stem + ".images.json");
        _assetDirectory = Path.Combine(directory, stem + ".assets");
    }

    public string ManifestPath => _manifestPath;
    public string AssetDirectory => _assetDirectory;

    public ImageResourceManifest Load()
    {
        if (!File.Exists(_manifestPath))
            return NewManifest();
        var manifest = JsonSerializer.Deserialize<ImageResourceManifest>(File.ReadAllText(_manifestPath), JsonOptions)
            ?? throw new InvalidDataException("图像资源清单为空或格式错误。");
        ValidateManifestShape(manifest);
        return manifest;
    }

    public ImageResourceEntry ImportPng(string sourcePath, string name)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("待导入 PNG 文件不存在。", sourcePath);
        if (!string.Equals(Path.GetExtension(sourcePath), ".png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("模板资源只接受 PNG 文件。");
        var displayName = RequiredName(name);
        var sourceInfo = new FileInfo(sourcePath);
        if (sourceInfo.Length is < 1 or > MaximumResourceFileBytes)
            throw new InvalidDataException($"PNG 文件大小必须在 1 到 {MaximumResourceFileBytes / (1024 * 1024)} MiB 之间。");
        var bytes = File.ReadAllBytes(sourcePath);
        var dimensions = ReadPngDimensions(bytes);
        Directory.CreateDirectory(_assetDirectory);
        var manifest = Load();
        var id = Guid.NewGuid().ToString("N");
        var fileName = id + ".png";
        var destination = Path.Combine(_assetDirectory, fileName);
        var temporary = destination + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, destination);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }

        var relative = Path.GetFileName(_assetDirectory) + "/" + fileName;
        var entry = new ImageResourceEntry(id, displayName, relative,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            dimensions.Width, dimensions.Height, bytes.LongLength, DateTimeOffset.UtcNow);
        Save(manifest with
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Resources = manifest.Resources.Append(entry).ToArray()
        });
        return entry;
    }

    public ImageResourceManifest Rename(string resourceId, string newName)
    {
        var manifest = Load();
        var name = RequiredName(newName);
        var entry = manifest.Resources.FirstOrDefault(x => x.Id == resourceId)
            ?? throw new KeyNotFoundException("图像资源不存在。");
        var updated = manifest with
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Resources = manifest.Resources.Select(x => x.Id == resourceId ? x with { Name = name } : x).ToArray()
        };
        Save(updated);
        return updated;
    }

    public ImageResourceManifest Remove(string resourceId)
    {
        var manifest = Load();
        if (!manifest.Resources.Any(x => x.Id == resourceId))
            return manifest;
        var updated = manifest with
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Resources = manifest.Resources.Where(x => x.Id != resourceId).ToArray()
        };
        Save(updated);
        return updated;
    }

    public IReadOnlyList<ImageResourceValidation> ValidateAll()
    {
        var manifest = Load();
        return manifest.Resources.Select(entry =>
        {
            try
            {
                var path = ResolveEntryPath(entry.RelativePath);
                if (!File.Exists(path))
                    return Result(entry, "missing", "文件不存在。");
                var info = new FileInfo(path);
                if (info.Length != entry.FileSize)
                    return Result(entry, "size-mismatch", $"文件大小已变化：清单 {entry.FileSize}，实际 {info.Length}。");
                var bytes = File.ReadAllBytes(path);
                var dimensions = ReadPngDimensions(bytes);
                if (dimensions.Width != entry.Width || dimensions.Height != entry.Height)
                    return Result(entry, "dimension-mismatch", $"PNG 尺寸已变化：清单 {entry.Width}×{entry.Height}，实际 {dimensions.Width}×{dimensions.Height}。");
                var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                return string.Equals(hash, entry.Sha256, StringComparison.OrdinalIgnoreCase)
                    ? Result(entry, "valid", "路径、PNG 尺寸、文件大小与 SHA-256 均匹配。")
                    : Result(entry, "hash-mismatch", "SHA-256 与清单不一致，资源可能被替换。");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
            {
                return Result(entry, "invalid-path", ex.Message);
            }
        }).ToArray();
    }

    public string ResolveEntryPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new InvalidDataException("资源相对路径不能为空。");
        var root = Path.GetFullPath(_assetDirectory) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(_scriptPath)!, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("资源路径必须是当前脚本 .assets 目录内的 PNG，禁止目录穿越。");
        return path;
    }

    private void Save(ImageResourceManifest manifest)
    {
        ValidateManifestShape(manifest);
        Directory.CreateDirectory(Path.GetDirectoryName(_manifestPath)!);
        var temporary = _manifestPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(manifest, JsonOptions));
            if (File.Exists(_manifestPath))
                File.Copy(_manifestPath, _manifestPath + ".bak", overwrite: true);
            File.Move(temporary, _manifestPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private ImageResourceManifest NewManifest() =>
        new(CurrentSchemaVersion, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, Array.Empty<ImageResourceEntry>());

    private static ImageResourceValidation Result(ImageResourceEntry entry, string status, string message) =>
        new(entry.Id, entry.Name, entry.RelativePath, status, message);

    private static string RequiredName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 80)
            throw new ArgumentException("资源名称不能为空，且最多 80 个字符。", nameof(value));
        return value.Trim();
    }

    private static void ValidateManifestShape(ImageResourceManifest manifest)
    {
        if (manifest.SchemaVersion != CurrentSchemaVersion)
            throw new NotSupportedException($"图像资源清单 schemaVersion={manifest.SchemaVersion} 不受支持；原文件不会被覆盖。");
        if (string.IsNullOrWhiteSpace(manifest.ScriptId) || manifest.Resources is null)
            throw new InvalidDataException("图像资源清单缺少 ScriptId 或 Resources。");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in manifest.Resources)
            if (string.IsNullOrWhiteSpace(entry.Id) || !ids.Add(entry.Id)
                || string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.RelativePath)
                || entry.Sha256?.Length != 64 || entry.Width < 1 || entry.Height < 1
                || entry.FileSize < 1)
                throw new InvalidDataException("图像资源清单包含无效或重复条目。");
    }

    private static (int Width, int Height) ReadPngDimensions(byte[] bytes)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(signature)
            || !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8))
            throw new InvalidDataException("文件不是有效 PNG（缺失 PNG 签名或 IHDR）。");
        var width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
        var height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
        if (width is < 1 or > 32768 || height is < 1 or > 32768 || (long)width * height > PixelImage.MaximumPixelCount)
            throw new InvalidDataException("PNG 尺寸超出图像资源限制。");
        return (width, height);
    }
}

public sealed record ImageResourceDependencyReport(
    IReadOnlyList<string> LiteralPaths, int DynamicLoadImageCalls);

public static class ImageResourceDependencyScanner
{
    public static ImageResourceDependencyReport Scan(string? luaSource)
    {
        var source = luaSource ?? string.Empty;
        var literals = new List<string>();
        var dynamicCalls = 0;
        for (var i = 0; i < source.Length;)
        {
            if (source[i] == '-' && i + 1 < source.Length && source[i + 1] == '-')
            {
                i += 2;
                if (TryReadLongBracket(source, i, out _, out var commentEnd))
                    i = commentEnd;
                else
                    while (i < source.Length && source[i] != '\n') i++;
                continue;
            }
            if (char.IsWhiteSpace(source[i])) { i++; continue; }
            if (source[i] is '\'' or '"')
            {
                TryReadQuoted(source, i, out _, out var stringEnd);
                i = stringEnd;
                continue;
            }
            if (TryReadLongBracket(source, i, out _, out var longStringEnd))
            {
                i = longStringEnd;
                continue;
            }
            if (IsIdentifierStart(source[i]))
            {
                var identifierStart = i++;
                while (i < source.Length && IsIdentifierPart(source[i])) i++;
                if (!source.AsSpan(identifierStart, i - identifierStart).SequenceEqual("LoadImage"))
                    continue;
                var argument = i;
                SkipIgnorable(source, ref argument);
                if (argument >= source.Length || source[argument] != '(') continue;
                argument++;
                SkipIgnorable(source, ref argument);
                if (argument < source.Length && (source[argument] is '\'' or '"'))
                {
                    if (TryReadQuoted(source, argument, out var literal, out var literalEnd))
                    {
                        literals.Add(literal);
                        i = literalEnd;
                    }
                }
                else if (TryReadLongBracket(source, argument, out var longLiteral, out var longLiteralEnd))
                {
                    literals.Add(longLiteral);
                    i = longLiteralEnd;
                }
                else
                {
                    dynamicCalls++;
                }
                continue;
            }
            i++;
        }
        return new ImageResourceDependencyReport(
            literals.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), dynamicCalls);
    }

    private static bool IsIdentifierStart(char value) => value == '_' || char.IsLetter(value);
    private static bool IsIdentifierPart(char value) => value == '_' || char.IsLetterOrDigit(value);

    private static void SkipIgnorable(string source, ref int index)
    {
        while (index < source.Length)
        {
            if (char.IsWhiteSpace(source[index])) { index++; continue; }
            if (source[index] == '-' && index + 1 < source.Length && source[index + 1] == '-')
            {
                index += 2;
                if (TryReadLongBracket(source, index, out _, out var end))
                    index = end;
                else
                    while (index < source.Length && source[index] != '\n') index++;
                continue;
            }
            break;
        }
    }

    private static bool TryReadQuoted(string source, int start, out string value, out int end)
    {
        value = string.Empty;
        end = start;
        if (start >= source.Length || (source[start] is not ('\'' or '"'))) return false;
        var quote = source[start];
        var builder = new StringBuilder();
        var index = start + 1;
        while (index < source.Length)
        {
            var current = source[index++];
            if (current == quote)
            {
                value = builder.ToString();
                end = index;
                return true;
            }
            if (current != '\\')
            {
                builder.Append(current);
                continue;
            }
            if (index >= source.Length) break;
            var escaped = source[index++];
            switch (escaped)
            {
                case 'a': builder.Append('\a'); break;
                case 'b': builder.Append('\b'); break;
                case 'f': builder.Append('\f'); break;
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 't': builder.Append('\t'); break;
                case 'v': builder.Append('\v'); break;
                case '\\': builder.Append('\\'); break;
                case '"': builder.Append('"'); break;
                case '\'': builder.Append('\''); break;
                case '\n': builder.Append('\n'); break;
                case '\r':
                    if (index < source.Length && source[index] == '\n') index++;
                    builder.Append('\n');
                    break;
                case 'z':
                    while (index < source.Length && char.IsWhiteSpace(source[index])) index++;
                    break;
                case 'x':
                    if (index + 1 < source.Length
                        && byte.TryParse(source.AsSpan(index, 2), System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture, out var hex))
                    {
                        builder.Append((char)hex);
                        index += 2;
                    }
                    else builder.Append('x');
                    break;
                default:
                    if (char.IsDigit(escaped))
                    {
                        var number = escaped - '0';
                        var count = 1;
                        while (count < 3 && index < source.Length && char.IsDigit(source[index]))
                        {
                            number = number * 10 + source[index++] - '0';
                            count++;
                        }
                        if (number > 255) return false;
                        builder.Append((char)number);
                    }
                    else builder.Append(escaped);
                    break;
            }
        }
        end = source.Length;
        return false;
    }

    private static bool TryReadLongBracket(string source, int start, out string value, out int end)
    {
        value = string.Empty;
        end = start;
        if (start >= source.Length || source[start] != '[') return false;
        var open = start + 1;
        while (open < source.Length && source[open] == '=') open++;
        if (open >= source.Length || source[open] != '[') return false;
        var level = open - start - 1;
        var contentStart = open + 1;
        var close = contentStart;
        while (close < source.Length)
        {
            if (source[close] != ']') { close++; continue; }
            var p = close + 1;
            var count = 0;
            while (p < source.Length && source[p] == '=' && count < level) { count++; p++; }
            if (count == level && p < source.Length && source[p] == ']')
            {
                value = source[contentStart..close];
                end = p + 1;
                return true;
            }
            close++;
        }
        end = source.Length;
        return true;
    }
}
