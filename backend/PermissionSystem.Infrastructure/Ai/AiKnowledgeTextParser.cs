using System.Text;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiKnowledge;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Infrastructure.Ai;

public sealed class AiKnowledgeTextParser : IAiKnowledgeTextParser
{
    public async Task<IReadOnlyList<AiKnowledgeParsedChunk>> ParseAsync(Stream content, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        var bytes = new byte[8192];
        int count;
        while ((count = await content.ReadAsync(bytes, ct)) > 0)
        {
            if (buffer.Length + count > AiKnowledgeContract.MaxFileBytes) throw Invalid();
            await buffer.WriteAsync(bytes.AsMemory(0, count), ct);
        }
        string text;
        try { text = new UTF8Encoding(false, true).GetString(buffer.ToArray()).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { throw Invalid(); }
        if (string.IsNullOrWhiteSpace(text) || text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'))) throw Invalid();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var chunks = new List<AiKnowledgeParsedChunk>();
        var pending = new StringBuilder();
        var startLine = 0;
        var endLine = 0;
        void Flush()
        {
            if (pending.Length == 0) return;
            var value = pending.ToString();
            chunks.Add(new(chunks.Count + 1, startLine, endLine, value, AiStructuredResults.Digest(value)));
            if (chunks.Count > AiKnowledgeContract.MaxChunks) throw Invalid();
            pending.Clear();
        }
        for (var i = 0; i < lines.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(lines[i])) { Flush(); continue; }
            var line = lines[i];
            for (var offset = 0; offset < line.Length;)
            {
                if (pending.Length > 0 && pending.Length + 1 + line.Length - offset > AiKnowledgeContract.MaxChunkCharacters) Flush();
                var length = Math.Min(AiKnowledgeContract.MaxChunkCharacters, line.Length - offset);
                if (offset + length < line.Length && char.IsHighSurrogate(line[offset + length - 1])) length--;
                if (pending.Length == 0) startLine = i + 1;
                else pending.Append('\n');
                pending.Append(line.AsSpan(offset, length));
                endLine = i + 1;
                offset += length;
                if (offset < line.Length) Flush();
            }
        }
        Flush();
        if (chunks.Count == 0) throw Invalid();
        return chunks;
    }

    private static BusinessException Invalid() => new(ErrorCode.ValidationFailed, "Knowledge text must be bounded, nonempty UTF-8 plain text.");
}
