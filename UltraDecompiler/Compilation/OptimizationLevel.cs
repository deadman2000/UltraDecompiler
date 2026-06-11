namespace UltraDecompiler.Compilation;

public enum OptimizationLevel
{
    /// <summary>
    /// /Od
    /// </summary>
    Disabled,

    /// <summary>
    /// /Ot
    /// </summary>
    Enabled,

    /// <summary>
    /// /Ol
    /// </summary>
    EnableLoop,

    /// <summary>
    /// /Ox
    /// </summary>
    EnabledFull,
}
