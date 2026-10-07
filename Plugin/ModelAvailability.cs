namespace FortniteEmotes;

// Kept independent of native APIs so the safety policy can be regression-tested.
internal sealed class ModelAvailability
{
    private readonly HashSet<string> _precached = new(StringComparer.Ordinal);

    internal static bool IsValidPath(string? model) =>
        !string.IsNullOrWhiteSpace(model) &&
        !model.StartsWith('/') && !model.Contains('\\') && !model.Contains(':') &&
        !model.Any(char.IsControl) &&
        model.EndsWith(".vmdl", StringComparison.Ordinal) &&
        model.Split('/').All(segment => segment.Length > 0 && segment is not "." and not "..");

    internal bool Register(string model, Func<string, bool> isMounted, Action<string> addResource)
    {
        if (!IsValidPath(model) || !isMounted(model))
            return false;
        if (_precached.Contains(model))
            return true;
        addResource(model);
        _precached.Add(model); // Never publish readiness before registration succeeds.
        return true;
    }

    // Recheck visibility immediately before SetModel: an addon can be unmounted mid-map.
    internal bool CanUse(string model, Func<string, bool> isMounted) =>
        IsValidPath(model) && _precached.Contains(model) && isMounted(model);

    internal void Clear() => _precached.Clear();
}
