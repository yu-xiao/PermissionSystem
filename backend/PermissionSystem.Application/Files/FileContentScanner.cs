using System.Text;
using PermissionSystem.Application.Abstractions;

namespace PermissionSystem.Application.Files;

public sealed class FileContentScanner : IFileContentScanner
{
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
    private static readonly byte[] JpegSignature = [0xff, 0xd8, 0xff];
    private static readonly byte[] GifSignature = "GIF8"u8.ToArray();
    private static readonly byte[] ZipSignature = [0x50, 0x4b, 0x03, 0x04];
    private static readonly byte[] OleSignature = [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1];
    private static readonly byte[] RarSignature = "Rar!"u8.ToArray();
    private static readonly byte[] EicarSignature =
        Encoding.ASCII.GetBytes(@"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");

    public async Task<FileScanResult> ScanAsync(
        FileScanRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Content == Stream.Null || !request.Content.CanRead)
        {
            return Infected("File content is not readable.");
        }

        if (request.Content.CanSeek)
        {
            request.Content.Position = 0;
        }

        var header = new byte[16];
        var headerLength = await ReadAtMostAsync(request.Content, header, cancellationToken);
        if (request.Content.CanSeek)
        {
            request.Content.Position = 0;
        }

        var detectedContentType = DetectContentType(
            request.Extension,
            header.AsSpan(0, headerLength));
        if (detectedContentType is null)
        {
            return Infected("File content does not match an allowed file type.");
        }

        if (!string.IsNullOrWhiteSpace(request.ClientContentType) &&
            !IsCompatibleContentType(request.ClientContentType, detectedContentType))
        {
            return Infected("Client content type does not match the detected file type.");
        }

        if (request.Content.CanSeek)
        {
            request.Content.Position = 0;
        }

        if (await ContainsSignatureAsync(request.Content, EicarSignature, cancellationToken))
        {
            return Infected("Malware signature detected.");
        }

        return new FileScanResult
        {
            IsClean = true,
            DetectedContentType = detectedContentType
        };
    }

    private static string? DetectContentType(string extension, ReadOnlySpan<byte> header)
    {
        var normalizedExtension = extension.Trim().ToLowerInvariant();
        if (header.StartsWith(PdfSignature))
        {
            return normalizedExtension == ".pdf" ? "application/pdf" : null;
        }

        if (header.StartsWith(PngSignature))
        {
            return normalizedExtension is ".png" ? "image/png" : null;
        }

        if (header.StartsWith(JpegSignature))
        {
            return normalizedExtension is ".jpg" or ".jpeg" ? "image/jpeg" : null;
        }

        if (header.StartsWith(GifSignature))
        {
            return normalizedExtension == ".gif" ? "image/gif" : null;
        }

        if (header.Length >= 12 &&
            header[..4].SequenceEqual("RIFF"u8) &&
            header[8..12].SequenceEqual("WEBP"u8))
        {
            return normalizedExtension == ".webp" ? "image/webp" : null;
        }

        if (header.StartsWith(RarSignature))
        {
            return normalizedExtension == ".rar" ? "application/vnd.rar" : null;
        }

        if (header.StartsWith(OleSignature))
        {
            return normalizedExtension switch
            {
                ".doc" => "application/msword",
                ".xls" => "application/vnd.ms-excel",
                ".ppt" => "application/vnd.ms-powerpoint",
                _ => null
            };
        }

        if (header.StartsWith(ZipSignature))
        {
            return normalizedExtension switch
            {
                ".zip" => "application/zip",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                _ => null
            };
        }

        if (normalizedExtension is ".txt" or ".csv")
        {
            return header.IndexOf((byte)0) < 0 ? normalizedExtension == ".csv" ? "text/csv" : "text/plain" : null;
        }

        return null;
    }

    private static bool IsCompatibleContentType(string clientContentType, string detectedContentType)
    {
        var normalized = clientContentType.Split(';')[0].Trim();
        return string.Equals(normalized, detectedContentType, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "application/octet-stream", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<int> ReadAtMostAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        return offset;
    }

    private static async Task<bool> ContainsSignatureAsync(
        Stream stream,
        byte[] signature,
        CancellationToken cancellationToken)
    {
        var matched = 0;
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            for (var index = 0; index < read; index++)
            {
                matched = buffer[index] == signature[matched] ? matched + 1 : buffer[index] == signature[0] ? 1 : 0;
                if (matched == signature.Length)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static FileScanResult Infected(string message)
    {
        return new FileScanResult
        {
            IsClean = false,
            Message = message
        };
    }
}
