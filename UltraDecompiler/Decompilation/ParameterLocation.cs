namespace UltraDecompiler.Decompilation;

/// <summary>Место передачи параметра (соглашение QuickC cdecl, near).</summary>
public abstract record ParameterLocation;

/// <summary>Параметр на стеке (порядок в сигнатуре — слева направо, как в C).</summary>
public sealed record StackParameter(int StackOffsetFromBp = 0) : ParameterLocation;

/// <summary>Параметр в регистре (редко для пользовательского кода, но встречается в обёртках).</summary>
public sealed record RegisterParameter(GpRegister16 Register) : ParameterLocation;
