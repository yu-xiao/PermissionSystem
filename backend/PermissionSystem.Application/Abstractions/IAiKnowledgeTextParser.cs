namespace PermissionSystem.Application.Abstractions;

public sealed record AiKnowledgeParsedChunk(int Sequence, int StartLine, int EndLine, string Content, string ContentHash);

public interface IAiKnowledgeTextParser
{
    Task<IReadOnlyList<AiKnowledgeParsedChunk>> ParseAsync(Stream content, CancellationToken ct = default);
}
