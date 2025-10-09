namespace Seq.Extensions.Logging;

/// <summary>
/// A wrapper type that marks a string as containing valid JSON that's safe to include as-is in log events.
/// </summary>
public readonly struct JsonSafeString
{
    string? Json { get; }

    /// <summary>
    /// Construct a <see cref="JsonSafeString"/> with well-formed JSON. The JSON is not validated: it must be
    /// externally validated or proven to be valid JSON before calling this constructor.
    /// </summary>
    /// <param name="json">A well-formed JSON string that can be included directly in a log event.</param>
    public JsonSafeString(string json)
    {
        Json = json;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        // As this type is a struct, it's impossible to enforce that `Json` is non-null. We use "<null>" instead
        // of a literal JSON null, so that valid null values can be distinguished from invalid values.
        return Json ?? "\"<null>\"";
    }
}