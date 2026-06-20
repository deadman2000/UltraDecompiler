namespace UltraDecompiler.Ir.InstructionHandlers;

public static class Handlers
{
    private static Dictionary<Mnemonic, IInstructionHandler> _dictionary = new()
    {
        [Mnemonic.RET] = new RetHandler(),
        [Mnemonic.RET_IMM] = new RetHandler(),
        [Mnemonic.RETF] = new RetHandler(),
        [Mnemonic.RETF_IMM] = new RetHandler(),

        [Mnemonic.PUSH] = new PushHandler(),
        [Mnemonic.POP] = new PopHandler(),

        [Mnemonic.MOV] = new MovHandler(),

        [Mnemonic.ADD] = new ArithmeticHandler(Math2Operation.Add, useCarryFlag: false),
        [Mnemonic.SUB] = new ArithmeticHandler(Math2Operation.Sub, useCarryFlag: false),
        [Mnemonic.ADC] = new ArithmeticHandler(Math2Operation.Add, useCarryFlag: true),
        [Mnemonic.SBB] = new ArithmeticHandler(Math2Operation.Sub, useCarryFlag: true),

        [Mnemonic.CMP] = new CmpHandler(),
        [Mnemonic.TEST] = new TestHandler(),

        [Mnemonic.CALL] = new CallHandler(),
        [Mnemonic.CALL_FAR] = new CallHandler(),

        [Mnemonic.CLI] = new CliHandler(),
        [Mnemonic.STI] = new StiHandler(),

        [Mnemonic.NOP] = new NopHandler(),

        [Mnemonic.JO] = new JoHandler(),
        [Mnemonic.JNO] = new JnoHandler(),
        [Mnemonic.JB] = new JbHandler(),
        [Mnemonic.JAE] = new JaeHandler(),
        [Mnemonic.JE] = new JeHandler(),
        [Mnemonic.JNE] = new JneHandler(),
        [Mnemonic.JBE] = new JbeHandler(),
        [Mnemonic.JA] = new JaHandler(),
        [Mnemonic.JS] = new JsHandler(),
        [Mnemonic.JNS] = new JnsHandler(),
        [Mnemonic.JP] = new JpHandler(),
        [Mnemonic.JNP] = new JnpHandler(),
        [Mnemonic.JL] = new JlHandler(),
        [Mnemonic.JGE] = new JgeHandler(),
        [Mnemonic.JLE] = new JleHandler(),
        [Mnemonic.JG] = new JgHandler(),
        [Mnemonic.JCXZ] = new JcxzHandler(),

        [Mnemonic.ROL] = new RotateHandler(isLeft: true),
        [Mnemonic.ROR] = new RotateHandler(isLeft: false),
    };

    public static IInstructionHandler Get(Mnemonic mnemonic) => _dictionary.GetValueOrDefault(mnemonic) ?? throw new NotImplementedException($"Instruction {mnemonic} is not yet supported");
}
