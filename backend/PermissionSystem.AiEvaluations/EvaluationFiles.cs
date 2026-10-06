using System.Text.Json;

namespace PermissionSystem.AiEvaluations;

public static class EvaluationFiles
{
    public static T Read<T>(string path)
    {
        if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new EvaluationInputException("Input file is too large.");
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), EvaluationJson.Options)
            ?? throw new EvaluationInputException("Input document is empty.");
    }
}
