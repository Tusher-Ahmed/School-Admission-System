namespace SchoolAdmission.Api.Infrastructure;

/// <summary>
/// Minimal .env loader for local development. Real environment variables always win,
/// so reviewers can ignore .env and just set variables in their shell.
/// </summary>
public static class DotEnv
{
    public static void Load()
    {
        var path = Find(Directory.GetCurrentDirectory()) ?? Find(AppContext.BaseDirectory);
        if (path is null) return;

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim().Trim('"');
            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    // Walks up from the start directory so it works from backend/, the project folder and test bin folders,
    // but never above backend/ (the folder with the .sln): an unrelated .env higher up must not be loaded.
    private static string? Find(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate)) return candidate;
            if (File.Exists(Path.Combine(dir.FullName, "SchoolAdmission.sln"))) return null;
        }
        return null;
    }
}
