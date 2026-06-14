namespace UltraDecompiler.Disassembly.Disassembler;

/// <summary>
/// Перечисление всех поддерживаемых мнемоник 8086/80286
/// </summary>
public enum Mnemonic
{
    // Основные
    MOV,
    PUSH,
    POP,
    XCHG,
    IN,
    OUT,
    LEA,
    LDS,
    LES,

    // Арифметика
    ADD,
    ADC,
    SUB,
    SBB,
    CMP,
    AND,
    OR,
    XOR,
    NOT,
    NEG,
    INC,
    DEC,
    MUL,
    IMUL,
    DIV,
    IDIV,

    // Логика и сдвиги
    TEST,
    SAL,
    SHR,
    SAR,
    ROL,
    ROR,
    RCL,
    RCR,

    // Переходы
    JMP,
    JMP_FAR,
    CALL,
    CALL_FAR,
    RET,
    RET_IMM,
    RETF,
    RETF_IMM,
    IRET,

    // Условные переходы
    JO, JNO, JB, JAE, JE, JNE, JBE, JA,
    JS, JNS, JP, JNP, JL, JGE, JLE, JG,
    JCXZ,

    // Циклы
    LOOP, LOOPE, LOOPNE,

    // Строковые
    MOVSB, MOVSW,
    CMPSB, CMPSW,
    STOSB, STOSW,
    LODSB, LODSW,
    SCASB, SCASW,

    // Флаги
    PUSHF, POPF, SAHF, LAHF,
    STI, CLI, STD, CLD, CLC, CMC, STC,

    // Прочие
    NOP,
    HLT,
    INT,
    INTO,
    ENTER,
    LEAVE,
    BOUND,
    ARPL,

    // FPU / 8087
    FWAIT,
    FPU,

    // Специальные
    DAA, DAS, AAA, AAS,
    AAM, AAD,
    CBW, CWD,
    XLAT,

    // Неизвестная / сырая
    DB
}