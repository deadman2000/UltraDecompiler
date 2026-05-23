# Summary

|||
|:---|:---|
| Generated on: | 23.05.2026 - 23:19:25 |
| Parser: | DynamicCodeCoverage |
| Assemblies: | 1 |
| Classes: | 1 |
| Files: | 1 |
| **Line coverage:** | 73% (501 of 686) |
| Covered lines: | 501 |
| Uncovered lines: | 185 |
| Coverable lines: | 686 |
| Total lines: | 1052 |
| Covered branches: | 0 |
| Total branches: | 0 |
| **Method coverage:** | [Feature is only available for sponsors](https://reportgenerator.io/pro) |

# Metrics

| **Method** | **Blocks covered** | **Blocks not covered** |
|:---|---:|---:|
| **Total** | 575 | 374 |

# Coverage

| **Name** | **Covered** | **Uncovered** | **Coverable** | **Total** | **Line coverage** |
|:---|---:|---:|---:|---:|---:|
| **UltraDecompiler.dll** | **501** | **185** | **686** | **1052** | **73%** |
| [UltraDecompiler.Disassembler.X86Disassembler](#ultradecompilerdisassemblerx86disassembler) | 501 | 185 | 686 | 1052 | 73% |

# UltraDecompiler.Disassembler.X86Disassembler

## Summary

|||
|:---|:---|
| Class: | UltraDecompiler.Disassembler.X86Disassembler |
| Assembly: | UltraDecompiler.dll |
| **File(s):** | D:\Projects\Decompiler\UltraDecompiler\UltraDecompiler\Disassembler\X86Disassembler.cs |
| **Line coverage:** | 73% (501 of 686) |
| Covered lines: | 501 |
| Uncovered lines: | 185 |
| Coverable lines: | 686 |
| Total lines: | 1052 |
| Covered branches: | 0 |
| Total branches: | 0 |
| **Method coverage:** | [Feature is only available for sponsors](https://reportgenerator.io/pro) |

## Metrics

| **Method** | **Blocks covered** | **Blocks not covered** |
|:---|---:|---:|
| **X86Disassembler(...)** | 5 | 2 |
| **Disassemble(...)** | 24 | 1 |
| **DisassembleBlock(...)** | 45 | 1 |
| **GetEffectiveJumpTarget(...)** | 21 | 4 |
| **DecodeOneInstruction()** | 207 | 108 |
| **DecodeLds()** | 11 | 3 |
| **DecodeLes()** | 11 | 3 |
| **DecodeAam()** | 7 | 4 |
| **DecodeAad()** | 7 | 4 |
| **DecodeEnter()** | 10 | 0 |
| **DecodeModRmAlu(...)** | 18 | 21 |
| **DecodeAluImmAx(...)** | 19 | 0 |
| **DecodeGroup80(...)** | 0 | 38 |
| **DecodeGroupF6(...)** | 18 | 16 |
| **DecodeGroupFEFF(...)** | 18 | 11 |
| **DecodeMovRegMem(...)** | 15 | 23 |
| **DecodeMovRegImm(...)** | 16 | 5 |
| **DecodeMovMemImm(...)** | 0 | 24 |
| **DecodeMovAxMem(...)** | 0 | 29 |
| **DecodeMovSreg(...)** | 0 | 14 |
| **DecodeShortJump(...)** | 14 | 13 |
| **DecodeNearJump()** | 7 | 0 |
| **DecodeLea()** | 11 | 3 |
| **DecodeXchg(...)** | 13 | 7 |
| **DecodeTestModRm(...)** | 0 | 20 |
| **DecodeTestAxImm(...)** | 15 | 3 |
| **DecodeShift(...)** | 23 | 9 |
| **DecodeLoop(...)** | 9 | 3 |
| **ParseMemoryOperand(...)** | 18 | 4 |
| **GetAluMnemonicEnum(...)** | 10 | 1 |
| **ReadByte()** | 1 | 0 |
| **ReadUInt16()** | 2 | 0 |

## File(s)

### D:\Projects\Decompiler\UltraDecompiler\UltraDecompiler\Disassembler\X86Disassembler.cs
```
   1           using UltraDecompiler.Extensions;
   2           
   3           namespace UltraDecompiler.Disassembler;
   4           
   5           public class X86Disassembler
   6           {
   7               private readonly byte[] _image;
   8               private int _pos;
   9               private Segment _segmentOverride;
  10  ✔  1         private readonly HashSet<int> _visited = [];
  11           
  12               public int DataSegmentBase { get; set; }
  13           
  14  ✔  1         public X86Disassembler(byte[] image)
  15  ✔  1         {
  16  ✓  1             _image = image ?? throw new ArgumentNullException(nameof(image));
  17  ✔  1         }
  18           
  19  ✔  1         public List<Instruction> Instructions { get; private set; } = [];
  20           
  21               public void Disassemble(int startOffset)
  22  ✔  1         {
  23  ✔  1             _visited.Clear();
  24  ✔  1             Instructions.Clear();
  25           
  26  ✔  1             var queue = new Queue<int>();
  27  ✔  1             queue.Enqueue(startOffset);
  28           
  29  ✔  1             while (queue.Count > 0)
  30  ✔  1             {
  31  ✔  1                 int offset = queue.Dequeue();
  32           
  33  ✓  1                 if (_visited.Contains(offset) || offset >= _image.Length)
  34  ✔  1                     continue;
  35           
  36  ✔  1                 DisassembleBlock(offset, queue);
  37  ✔  1             }
  38           
  39  ✔  1             Instructions = Instructions.OrderBy(i => i.Offset).ToList();
  40  ✔  1         }
  41           
  42               private void DisassembleBlock(int startOffset, Queue<int> queue)
  43  ✔  1         {
  44  ✔  1             _pos = startOffset;
  45  ✔  1             _segmentOverride = Segment.None;
  46           
  47  ✔  1             while (_pos < _image.Length)
  48  ✔  1             {
  49  ✔  1                 if (_visited.Contains(_pos))
  50  ❌ 0                     break;
  51           
  52  ✔  1                 _visited.Add(_pos);
  53           
  54  ✔  1                 int instrStart = _pos;
  55  ✔  1                 var instr = DecodeOneInstruction();
  56  ✔  1                 instr.Offset = instrStart;
  57  ✔  1                 instr.Bytes = _image[instrStart.._pos].ToArray();
  58  ✔  1                 instr.Segment = _segmentOverride;
  59  ✔  1                 Instructions.Add(instr);
  60  ✔  1                 _segmentOverride = Segment.None;
  61           
  62  ✔  1                 if (instr.Mnemonic is Mnemonic.RET or Mnemonic.RETF or Mnemonic.IRET)
  63  ✔  1                     break;
  64           
  65  ✔  1                 if (instr.Mnemonic == Mnemonic.JMP)
  66  ✔  1                 {
  67  ✔  1                     int target = GetEffectiveJumpTarget(instr);
  68  ✔  1                     if (target != -1)
  69  ✔  1                         queue.Enqueue(target);
  70  ✔  1                     break;
  71                       }
  72  ✔  1                 else if (instr.IsJump || instr.Mnemonic == Mnemonic.CALL)
  73  ✔  1                 {
  74  ✔  1                     int target = GetEffectiveJumpTarget(instr);
  75  ✔  1                     if (target != -1)
  76  ✔  1                         queue.Enqueue(target);
  77  ✔  1                 }
  78  ✔  1             }
  79  ✔  1         }
  80           
  81               private int GetEffectiveJumpTarget(Instruction instr)
  82  ✔  1         {
  83  ✔  1             int direct = instr.GetJumpTarget();
  84  ✔  1             if (direct != -1)
  85  ✔  1                 return direct;
  86           
  87  ✓  1             var op = instr.Operand1.IsSet ? instr.Operand1 : instr.Operand2;
  88  ✓  1             if ((instr.Mnemonic == Mnemonic.CALL || instr.Mnemonic == Mnemonic.JMP) && op.Type == OperandType.Memory)
  89  ✔  1             {
  90  ✔  1                 int realAddr = DataSegmentBase + op.Value;
  91  ✓  1                 if (realAddr >= 0 && realAddr + 2 <= _image.Length)
  92  ✔  1                 {
  93  ✔  1                     return (ushort)(_image[realAddr] | (_image[realAddr + 1] << 8));
  94                       }
  95  ✔  1             }
  96           
  97  ✔  1             return -1;
  98  ✔  1         }
  99           
 100               private Instruction DecodeOneInstruction()
 101  ✔  1         {
 102  ✔  1             byte opcode = ReadByte();
 103           
 104  ✔  1             switch (opcode)
 105                   {
 106                       // Префиксы
 107                       case 0xF0:
 108  ✔  1                     {
 109  ✔  1                         var instr = DecodeOneInstruction();
 110  ✔  1                         instr.Prefix |= InstructionPrefix.LOCK;
 111  ✔  1                         return instr;
 112                           }
 113           
 114                       case 0xF2:
 115  ✔  1                     {
 116  ✔  1                         var instr = DecodeOneInstruction();
 117  ✔  1                         instr.Prefix |= InstructionPrefix.REPNZ;
 118  ✔  1                         return instr;
 119                           }
 120                       case 0xF3:
 121  ✔  1                     {
 122  ✔  1                         var instr = DecodeOneInstruction();
 123  ✔  1                         instr.Prefix |= InstructionPrefix.REPZ;
 124  ✔  1                         return instr;
 125                           }
 126           
 127  ✔  1                 case 0x26: _segmentOverride = Segment.ES; return DecodeOneInstruction();
 128  ❌ 0                 case 0x2E: _segmentOverride = Segment.CS; return DecodeOneInstruction();
 129  ✔  1                 case 0x36: _segmentOverride = Segment.SS; return DecodeOneInstruction();
 130  ❌ 0                 case 0x3E: _segmentOverride = Segment.DS; return DecodeOneInstruction();
 131           
 132                       case 0x00:
 133                       case 0x01:
 134                       case 0x02:
 135                       case 0x03:
 136                       case 0x08:
 137                       case 0x09:
 138                       case 0x0A:
 139                       case 0x0B:
 140                       case 0x18:
 141                       case 0x19:
 142                       case 0x1A:
 143                       case 0x1B:
 144                       case 0x20:
 145                       case 0x21:
 146                       case 0x22:
 147                       case 0x23:
 148                       case 0x28:
 149                       case 0x29:
 150                       case 0x2A:
 151                       case 0x2B:
 152                       case 0x30:
 153                       case 0x31:
 154                       case 0x32:
 155                       case 0x33:
 156                       case 0x38:
 157                       case 0x39:
 158                       case 0x3A:
 159                       case 0x3B:
 160  ✔  1                     return DecodeModRmAlu(opcode);
 161           
 162                       case 0x04:
 163                       case 0x05:
 164                       case 0x0C:
 165                       case 0x0D:
 166                       case 0x14:
 167                       case 0x15:
 168                       case 0x1C:
 169                       case 0x1D:
 170                       case 0x24:
 171                       case 0x25:
 172                       case 0x2C:
 173                       case 0x2D:
 174                       case 0x34:
 175                       case 0x35:
 176                       case 0x3C:
 177                       case 0x3D:
 178  ✔  1                     return DecodeAluImmAx(opcode);
 179           
 180                       case 0x80:
 181                       case 0x81:
 182                       case 0x82:
 183                       case 0x83:
 184  ❌ 0                     return DecodeGroup80(opcode);
 185           
 186                       case 0xF6:
 187                       case 0xF7:
 188  ✔  1                     return DecodeGroupF6(opcode);
 189           
 190                       case 0xFE:
 191                       case 0xFF:
 192  ✔  1                     return DecodeGroupFEFF(opcode);
 193           
 194                       case 0x88:
 195                       case 0x89:
 196                       case 0x8A:
 197                       case 0x8B:
 198  ✔  1                     return DecodeMovRegMem(opcode);
 199           
 200                       case 0x8C:
 201                       case 0x8E:
 202  ❌ 0                     return DecodeMovSreg(opcode);
 203           
 204                       case 0xA0:
 205                       case 0xA1:
 206                       case 0xA2:
 207                       case 0xA3:
 208  ❌ 0                     return DecodeMovAxMem(opcode);
 209           
 210                       case 0xB0:
 211                       case 0xB1:
 212                       case 0xB2:
 213                       case 0xB3:
 214                       case 0xB4:
 215                       case 0xB5:
 216                       case 0xB6:
 217                       case 0xB7:
 218                       case 0xB8:
 219                       case 0xB9:
 220                       case 0xBA:
 221                       case 0xBB:
 222                       case 0xBC:
 223                       case 0xBD:
 224                       case 0xBE:
 225                       case 0xBF:
 226  ✔  1                     return DecodeMovRegImm(opcode);
 227           
 228                       case 0xC6:
 229                       case 0xC7:
 230  ❌ 0                     return DecodeMovMemImm(opcode);
 231           
 232                       case 0x50:
 233                       case 0x51:
 234                       case 0x52:
 235                       case 0x53:
 236                       case 0x54:
 237                       case 0x55:
 238                       case 0x56:
 239                       case 0x57:
 240  ✔  1                 {
 241  ✔  1                     int reg = opcode - 0x50;
 242  ✔  1                     return new Instruction
 243  ✔  1                     {
 244  ✔  1                         Mnemonic = Mnemonic.PUSH,
 245  ✔  1                         Operand1 = new Operand(OperandType.Register16, reg)
 246  ✔  1                     };
 247                       }
 248                       case 0x58:
 249                       case 0x59:
 250                       case 0x5A:
 251                       case 0x5B:
 252                       case 0x5C:
 253                       case 0x5D:
 254                       case 0x5E:
 255                       case 0x5F:
 256  ✔  1                 {
 257  ✔  1                     int reg = opcode - 0x58;
 258  ✔  1                     return new Instruction
 259  ✔  1                     {
 260  ✔  1                         Mnemonic = Mnemonic.POP,
 261  ✔  1                         Operand1 = new Operand(OperandType.Register16, reg)
 262  ✔  1                     };
 263                       }
 264           
 265  ✔  1                 case 0x06: return new Instruction { Mnemonic = Mnemonic.PUSH, Operand1 = new Operand(OperandType.SegmentRegi
 266  ❌ 0                 case 0x0E: return new Instruction { Mnemonic = Mnemonic.PUSH, Operand1 = new Operand(OperandType.SegmentRegi
 267  ❌ 0                 case 0x16: return new Instruction { Mnemonic = Mnemonic.PUSH, Operand1 = new Operand(OperandType.SegmentRegi
 268  ❌ 0                 case 0x1E: return new Instruction { Mnemonic = Mnemonic.PUSH, Operand1 = new Operand(OperandType.SegmentRegi
 269  ✔  1                 case 0x07: return new Instruction { Mnemonic = Mnemonic.POP, Operand1 = new Operand(OperandType.SegmentRegis
 270  ❌ 0                 case 0x17: return new Instruction { Mnemonic = Mnemonic.POP, Operand1 = new Operand(OperandType.SegmentRegis
 271  ❌ 0                 case 0x1F: return new Instruction { Mnemonic = Mnemonic.POP, Operand1 = new Operand(OperandType.SegmentRegis
 272           
 273                       case 0x70:
 274                       case 0x71:
 275                       case 0x72:
 276                       case 0x73:
 277                       case 0x74:
 278                       case 0x75:
 279                       case 0x76:
 280                       case 0x77:
 281                       case 0x78:
 282                       case 0x79:
 283                       case 0x7A:
 284                       case 0x7B:
 285                       case 0x7C:
 286                       case 0x7D:
 287                       case 0x7E:
 288                       case 0x7F:
 289                       case 0xEB:
 290                       case 0xE3:
 291  ✔  1                     return DecodeShortJump(opcode);
 292           
 293  ✔  1                 case 0xE9: return DecodeNearJump();
 294           
 295                       case 0xE8:
 296  ❌ 0                     short rel = (short)ReadUInt16();
 297  ❌ 0                     return new Instruction
 298  ❌ 0                     {
 299  ❌ 0                         Mnemonic = Mnemonic.CALL,
 300  ❌ 0                         Operand1 = new Operand(OperandType.Relative16, _pos + rel)
 301  ❌ 0                     };
 302           
 303  ✔  1                 case 0xC3: return new Instruction { Mnemonic = Mnemonic.RET };
 304  ❌ 0                 case 0xCA: return new Instruction { Mnemonic = Mnemonic.RETF_FAR };
 305  ✔  1                 case 0xCB: return new Instruction { Mnemonic = Mnemonic.RETF };
 306  ✔  1                 case 0xCE: return new Instruction { Mnemonic = Mnemonic.INTO };
 307  ✔  1                 case 0xCF: return new Instruction { Mnemonic = Mnemonic.IRET };
 308           
 309                       case 0xCD:
 310  ✔  1                 {
 311  ✔  1                     byte intNum = ReadByte();
 312  ✔  1                     return new Instruction
 313  ✔  1                     {
 314  ✔  1                         Mnemonic = Mnemonic.INT,
 315  ✔  1                         Operand1 = new Operand(OperandType.Immediate8, intNum)
 316  ✔  1                     };
 317                       }
 318           
 319  ✔  1                 case 0x90: return new Instruction { Mnemonic = Mnemonic.NOP };
 320           
 321                       case 0x86:
 322                       case 0x87:
 323  ✔  1                     return DecodeXchg(opcode);
 324                       case 0x91:
 325                       case 0x92:
 326                       case 0x93:
 327                       case 0x94:
 328                       case 0x95:
 329                       case 0x96:
 330                       case 0x97:
 331  ✔  1                 {
 332  ✔  1                     int reg = opcode - 0x90;
 333  ✔  1                     return new Instruction
 334  ✔  1                     {
 335  ✔  1                         Mnemonic = Mnemonic.XCHG,
 336  ✔  1                         Operand1 = new Operand(OperandType.Register16, 0),
 337  ✔  1                         Operand2 = new Operand(OperandType.Register16, reg)
 338  ✔  1                     };
 339                       }
 340           
 341                       case 0x40:
 342                       case 0x41:
 343                       case 0x42:
 344                       case 0x43:
 345                       case 0x44:
 346                       case 0x45:
 347                       case 0x46:
 348                       case 0x47:
 349  ✔  1                 {
 350  ✔  1                     int reg = opcode - 0x40;
 351  ✔  1                     return new Instruction
 352  ✔  1                     {
 353  ✔  1                         Mnemonic = Mnemonic.INC,
 354  ✔  1                         Operand1 = new Operand(OperandType.Register16, reg)
 355  ✔  1                     };
 356                       }
 357                       case 0x48:
 358                       case 0x49:
 359                       case 0x4A:
 360                       case 0x4B:
 361                       case 0x4C:
 362                       case 0x4D:
 363                       case 0x4E:
 364                       case 0x4F:
 365  ✔  1                 {
 366  ✔  1                     int reg = opcode - 0x48;
 367  ✔  1                     return new Instruction
 368  ✔  1                     {
 369  ✔  1                         Mnemonic = Mnemonic.DEC,
 370  ✔  1                         Operand1 = new Operand(OperandType.Register16, reg)
 371  ✔  1                     };
 372                       }
 373           
 374  ✔  1                 case 0x8D: return DecodeLea();
 375           
 376  ❌ 0                 case 0x84: case 0x85: return DecodeTestModRm(opcode);
 377  ✔  1                 case 0xA8: case 0xA9: return DecodeTestAxImm(opcode);
 378           
 379  ✔  1                 case 0xA4: return new Instruction { Mnemonic = Mnemonic.MOVSB };
 380  ✔  1                 case 0xA5: return new Instruction { Mnemonic = Mnemonic.MOVSW };
 381  ✔  1                 case 0xA6: return new Instruction { Mnemonic = Mnemonic.CMPSB };
 382  ✔  1                 case 0xA7: return new Instruction { Mnemonic = Mnemonic.CMPSW };
 383  ✔  1                 case 0xAA: return new Instruction { Mnemonic = Mnemonic.STOSB };
 384  ❌ 0                 case 0xAB: return new Instruction { Mnemonic = Mnemonic.STOSW };
 385  ✔  1                 case 0xAC: return new Instruction { Mnemonic = Mnemonic.LODSB };
 386  ❌ 0                 case 0xAD: return new Instruction { Mnemonic = Mnemonic.LODSW };
 387  ✔  1                 case 0xAE: return new Instruction { Mnemonic = Mnemonic.SCASB };
 388  ✔  1                 case 0xAF: return new Instruction { Mnemonic = Mnemonic.SCASW };
 389           
 390                       case 0xD0:
 391                       case 0xD1:
 392                       case 0xD2:
 393                       case 0xD3:
 394  ✔  1                     return DecodeShift(opcode);
 395           
 396  ✔  1                 case 0x98: return new Instruction { Mnemonic = Mnemonic.CBW };
 397  ✔  1                 case 0x99: return new Instruction { Mnemonic = Mnemonic.CWD };
 398           
 399                       case 0xE0:
 400                       case 0xE1:
 401                       case 0xE2:
 402  ✔  1                     return DecodeLoop(opcode);
 403           
 404  ✔  1                 case 0x27: return new Instruction { Mnemonic = Mnemonic.DAA };
 405  ✔  1                 case 0x2F: return new Instruction { Mnemonic = Mnemonic.DAS };
 406  ✔  1                 case 0x37: return new Instruction { Mnemonic = Mnemonic.AAA };
 407  ✔  1                 case 0x3F: return new Instruction { Mnemonic = Mnemonic.AAS };
 408           
 409  ✔  1                 case 0xD4: return DecodeAam();
 410  ✔  1                 case 0xD5: return DecodeAad();
 411           
 412  ✔  1                 case 0x9C: return new Instruction { Mnemonic = Mnemonic.PUSHF };
 413  ❌ 0                 case 0x9D: return new Instruction { Mnemonic = Mnemonic.POPF };
 414  ✔  1                 case 0x9E: return new Instruction { Mnemonic = Mnemonic.SAHF };
 415  ✔  1                 case 0x9F: return new Instruction { Mnemonic = Mnemonic.LAHF };
 416           
 417  ✔  1                 case 0xFA: return new Instruction { Mnemonic = Mnemonic.CLI };
 418  ✔  1                 case 0xFB: return new Instruction { Mnemonic = Mnemonic.STI };
 419  ✔  1                 case 0xFC: return new Instruction { Mnemonic = Mnemonic.CLD };
 420  ✔  1                 case 0xFD: return new Instruction { Mnemonic = Mnemonic.STD };
 421  ✔  1                 case 0xF5: return new Instruction { Mnemonic = Mnemonic.CMC };
 422  ✔  1                 case 0xF8: return new Instruction { Mnemonic = Mnemonic.CLC };
 423  ✔  1                 case 0xF9: return new Instruction { Mnemonic = Mnemonic.STC };
 424           
 425  ✔  1                 case 0xD7: return new Instruction { Mnemonic = Mnemonic.XLAT };
 426           
 427  ✔  1                 case 0xF4: return new Instruction { Mnemonic = Mnemonic.HLT };
 428           
 429  ✔  1                 case 0xC4: return DecodeLes();
 430  ✔  1                 case 0xC5: return DecodeLds();
 431           
 432  ✔  1                 case 0xC8: return DecodeEnter();
 433  ❌ 0                 case 0xC9: return new Instruction { Mnemonic = Mnemonic.LEAVE };
 434           
 435                       // IN/OUT support added
 436                       case 0xE4: // IN AL, imm8
 437  ✔  1                 {
 438  ✔  1                     byte port = ReadByte();
 439  ✔  1                     return new Instruction
 440  ✔  1                     {
 441  ✔  1                         Mnemonic = Mnemonic.IN,
 442  ✔  1                         Operand1 = new Operand(OperandType.Register8, 0), // AL
 443  ✔  1                         Operand2 = new Operand(OperandType.Immediate8, port)
 444  ✔  1                     };
 445                       }
 446                       case 0xE5: // IN AX, imm8
 447  ❌ 0                 {
 448  ❌ 0                     byte port = ReadByte();
 449  ❌ 0                     return new Instruction
 450  ❌ 0                     {
 451  ❌ 0                         Mnemonic = Mnemonic.IN,
 452  ❌ 0                         Operand1 = new Operand(OperandType.Register16, 0), // AX
 453  ❌ 0                         Operand2 = new Operand(OperandType.Immediate8, port)
 454  ❌ 0                     };
 455                       }
 456                       case 0xE6: // OUT imm8, AL
 457  ✔  1                 {
 458  ✔  1                     byte port = ReadByte();
 459  ✔  1                     return new Instruction
 460  ✔  1                     {
 461  ✔  1                         Mnemonic = Mnemonic.OUT,
 462  ✔  1                         Operand1 = new Operand(OperandType.Immediate8, port),
 463  ✔  1                         Operand2 = new Operand(OperandType.Register8, 0) // AL
 464  ✔  1                     };
 465                       }
 466                       case 0xE7: // OUT imm8, AX
 467  ❌ 0                 {
 468  ❌ 0                     byte port = ReadByte();
 469  ❌ 0                     return new Instruction
 470  ❌ 0                     {
 471  ❌ 0                         Mnemonic = Mnemonic.OUT,
 472  ❌ 0                         Operand1 = new Operand(OperandType.Immediate8, port),
 473  ❌ 0                         Operand2 = new Operand(OperandType.Register16, 0) // AX
 474  ❌ 0                     };
 475                       }
 476                       case 0xEC: // IN AL, DX
 477  ❌ 0                     return new Instruction
 478  ❌ 0                     {
 479  ❌ 0                         Mnemonic = Mnemonic.IN,
 480  ❌ 0                         Operand1 = new Operand(OperandType.Register8, 0),
 481  ❌ 0                         Operand2 = new Operand(OperandType.Register16, 2) // DX
 482  ❌ 0                     };
 483                       case 0xED: // IN AX, DX
 484  ❌ 0                     return new Instruction
 485  ❌ 0                     {
 486  ❌ 0                         Mnemonic = Mnemonic.IN,
 487  ❌ 0                         Operand1 = new Operand(OperandType.Register16, 0),
 488  ❌ 0                         Operand2 = new Operand(OperandType.Register16, 2) // DX
 489  ❌ 0                     };
 490                       case 0xEE: // OUT DX, AL
 491  ❌ 0                     return new Instruction
 492  ❌ 0                     {
 493  ❌ 0                         Mnemonic = Mnemonic.OUT,
 494  ❌ 0                         Operand1 = new Operand(OperandType.Register16, 2), // DX
 495  ❌ 0                         Operand2 = new Operand(OperandType.Register8, 0) // AL
 496  ❌ 0                     };
 497                       case 0xEF: // OUT DX, AX
 498  ❌ 0                     return new Instruction
 499  ❌ 0                     {
 500  ❌ 0                         Mnemonic = Mnemonic.OUT,
 501  ❌ 0                         Operand1 = new Operand(OperandType.Register16, 2), // DX
 502  ❌ 0                         Operand2 = new Operand(OperandType.Register16, 0) // AX
 503  ❌ 0                     };
 504           
 505                       // SBB already supported via DecodeGroup80 / GetAluMnemonicEnum, but ensuring in DecodeOneInstruction path
 506                       // (SBB uses 0x18-0x1B, 0x80/83 with reg=3 etc. - handled)
 507           
 508                       default:
 509  ❌ 0                     return new Instruction { Mnemonic = Mnemonic.DB, Operands = Instruction.UnknownOperand };
 510                   }
 511  ✔  1         }
 512           
 513               private Instruction DecodeLds()
 514  ✔  1         {
 515  ✔  1             byte modrm = ReadByte();
 516  ✔  1             int mod = (modrm >> 6) & 3;
 517  ✔  1             int reg = (modrm >> 3) & 7;
 518  ✔  1             int rm = modrm & 7;
 519           
 520  ✔  1             var instr = new Instruction
 521  ✔  1             {
 522  ✔  1                 Mnemonic = Mnemonic.LDS,
 523  ✔  1                 Operand1 = new Operand(OperandType.Register16, reg)
 524  ✔  1             };
 525  ✔  1             if (mod != 3)
 526  ✔  1                 instr.Operand2 = ParseMemoryOperand(rm, mod);
 527                   else
 528  ❌ 0                 instr.Operand2 = new Operand(OperandType.Register16, rm);
 529  ✔  1             return instr;
 530  ✔  1         }
 531           
 532               private Instruction DecodeLes()
 533  ✔  1         {
 534  ✔  1             byte modrm = ReadByte();
 535  ✔  1             int mod = (modrm >> 6) & 3;
 536  ✔  1             int reg = (modrm >> 3) & 7;
 537  ✔  1             int rm = modrm & 7;
 538           
 539  ✔  1             var instr = new Instruction
 540  ✔  1             {
 541  ✔  1                 Mnemonic = Mnemonic.LES,
 542  ✔  1                 Operand1 = new Operand(OperandType.Register16, reg)
 543  ✔  1             };
 544  ✔  1             if (mod != 3)
 545  ✔  1                 instr.Operand2 = ParseMemoryOperand(rm, mod);
 546                   else
 547  ❌ 0                 instr.Operand2 = new Operand(OperandType.Register16, rm);
 548  ✔  1             return instr;
 549  ✔  1         }
 550           
 551               private Instruction DecodeAam()
 552  ✔  1         {
 553  ✔  1             byte baseVal = ReadByte();
 554  ✓  1             return baseVal == 0x0A ? new Instruction { Mnemonic = Mnemonic.AAM } : new Instruction { Mnemonic = Mnemonic.AAM
 555  ✔  1         }
 556           
 557               private Instruction DecodeAad()
 558  ✔  1         {
 559  ✔  1             byte baseVal = ReadByte();
 560  ✓  1             return baseVal == 0x0A ? new Instruction { Mnemonic = Mnemonic.AAD } : new Instruction { Mnemonic = Mnemonic.AAD
 561  ✔  1         }
 562           
 563               private Instruction DecodeEnter()
 564  ✔  1         {
 565  ✔  1             ushort alloc = ReadUInt16();
 566  ✔  1             byte level = ReadByte();
 567  ✔  1             return new Instruction
 568  ✔  1             {
 569  ✔  1                 Mnemonic = Mnemonic.ENTER,
 570  ✔  1                 Operand1 = new Operand(OperandType.Immediate16, alloc),
 571  ✔  1                 Operand2 = new Operand(OperandType.Immediate8, level)
 572  ✔  1             };
 573  ✔  1         }
 574           
 575               private Instruction DecodeModRmAlu(byte opcode)
 576  ✔  1         {
 577  ✔  1             byte modrm = ReadByte();
 578  ✔  1             Mnemonic mnem = GetAluMnemonicEnum(opcode);
 579  ✔  1             int mod = (modrm >> 6) & 3;
 580  ✔  1             int reg = (modrm >> 3) & 7;
 581  ✔  1             int rm = modrm & 7;
 582  ✔  1             bool word = (opcode & 1) == 1;
 583           
 584  ✔  1             var instr = new Instruction { Mnemonic = mnem };
 585           
 586  ✔  1             if ((opcode & 2) != 0)
 587  ❌ 0             {
 588  ❌ 0                 instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, reg);
 589  ❌ 0                 if (mod == 3)
 590  ❌ 0                     instr.Operand2 = new Operand(word ? OperandType.Register16 : OperandType.Register8, rm);
 591                       else
 592  ❌ 0                     instr.Operand2 = ParseMemoryOperand(rm, mod);
 593  ❌ 0             }
 594                   else
 595  ✔  1             {
 596  ✔  1                 if (mod == 3)
 597  ✓  1                     instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, rm);
 598                       else
 599  ❌ 0                     instr.Operand1 = ParseMemoryOperand(rm, mod);
 600  ✓  1                 instr.Operand2 = new Operand(word ? OperandType.Register16 : OperandType.Register8, reg);
 601  ✔  1             }
 602           
 603  ✔  1             return instr;
 604  ✔  1         }
 605           
 606               private Instruction DecodeAluImmAx(byte opcode)
 607  ✔  1         {
 608  ✔  1             Mnemonic mnem = GetAluMnemonicEnum(opcode);
 609  ✔  1             bool word = (opcode & 1) == 1;
 610  ✔  1             ushort imm = word ? ReadUInt16() : ReadByte();
 611  ✔  1             var instr = new Instruction
 612  ✔  1             {
 613  ✔  1                 Mnemonic = mnem,
 614  ✔  1                 Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, 0),
 615  ✔  1                 Operand2 = new Operand(word ? OperandType.Immediate16 : OperandType.Immediate8, imm)
 616  ✔  1             };
 617  ✔  1             return instr;
 618  ✔  1         }
 619           
 620               private Instruction DecodeGroup80(byte opcode)
 621  ❌ 0         {
 622  ❌ 0             byte modrm = ReadByte();
 623  ❌ 0             int mod = (modrm >> 6) & 3;
 624  ❌ 0             int regField = (modrm >> 3) & 7;
 625  ❌ 0             bool word = (opcode & 1) == 1;
 626  ❌ 0             bool signExtend = opcode == 0x83;
 627           
 628  ❌ 0             Mnemonic mnem = regField switch
 629  ❌ 0             {
 630  ❌ 0                 0 => Mnemonic.ADD,
 631  ❌ 0                 1 => Mnemonic.OR,
 632  ❌ 0                 2 => Mnemonic.ADC,
 633  ❌ 0                 3 => Mnemonic.SBB,
 634  ❌ 0                 4 => Mnemonic.AND,
 635  ❌ 0                 5 => Mnemonic.SUB,
 636  ❌ 0                 6 => Mnemonic.XOR,
 637  ❌ 0                 7 => Mnemonic.CMP,
 638  ❌ 0                 _ => Mnemonic.DB
 639  ❌ 0             };
 640           
 641  ❌ 0             var instr = new Instruction { Mnemonic = mnem };
 642           
 643  ❌ 0             if (mod == 3)
 644  ❌ 0                 instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, modrm & 7);
 645                   else
 646  ❌ 0                 instr.Operand1 = ParseMemoryOperand(modrm & 7, mod);
 647  ❌ 0             instr.Operand2 = new Operand(word ? OperandType.Immediate16 : OperandType.Immediate8, signExtend ? (ushort)(sbyt
 648           
 649  ❌ 0             return instr;
 650  ❌ 0         }
 651           
 652               private Instruction DecodeGroupF6(byte opcode)
 653  ✔  1         {
 654  ✔  1             byte modrm = ReadByte();
 655  ✔  1             int mod = (modrm >> 6) & 3;
 656  ✔  1             int regField = (modrm >> 3) & 7;
 657  ✔  1             bool word = (opcode & 1) == 1;
 658           
 659  ✔  1             var instr = new Instruction
 660  ✔  1             {
 661  ✔  1                 Mnemonic = regField switch
 662  ✔  1                 {
 663  ✔  1                     0 => Mnemonic.TEST,
 664  ✔  1                     2 => Mnemonic.NOT,
 665  ✔  1                     3 => Mnemonic.NEG,
 666  ✔  1                     4 => Mnemonic.MUL,
 667  ✔  1                     5 => Mnemonic.IMUL,
 668  ✔  1                     6 => Mnemonic.DIV,
 669  ✔  1                     7 => Mnemonic.IDIV,
 670  ✔  1                     _ => Mnemonic.DB
 671  ✔  1                 }
 672  ✔  1             };
 673           
 674  ✔  1             if (mod == 3)
 675  ✓  1                 instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, modrm & 7);
 676                   else
 677  ❌ 0                 instr.Operand1 = ParseMemoryOperand(modrm & 7, mod);
 678           
 679  ✔  1             if (regField == 0)
 680  ❌ 0             {
 681  ❌ 0                 ushort imm = word ? ReadUInt16() : ReadByte();
 682  ❌ 0                 instr.Operand2 = new Operand(word ? OperandType.Immediate16 : OperandType.Immediate8, imm);
 683  ❌ 0             }
 684           
 685  ✔  1             return instr;
 686  ✔  1         }
 687           
 688               private Instruction DecodeGroupFEFF(byte opcode)
 689  ✔  1         {
 690  ✔  1             byte modrm = ReadByte();
 691  ✔  1             int mod = (modrm >> 6) & 3;
 692  ✔  1             int regField = (modrm >> 3) & 7;
 693  ✔  1             bool word = (opcode & 1) == 1;
 694           
 695  ✔  1             var instr = new Instruction();
 696           
 697  ✔  1             if (mod == 3)
 698  ❌ 0                 instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, modrm & 7);
 699                   else
 700  ✔  1                 instr.Operand1 = ParseMemoryOperand(modrm & 7, mod);
 701           
 702  ✔  1             if (opcode == 0xFE)
 703  ✔  1             {
 704  ✔  1                 instr.Mnemonic = regField switch { 0 => Mnemonic.INC, 1 => Mnemonic.DEC, _ => Mnemonic.DB };
 705  ✔  1                 return instr;
 706                   }
 707           
 708  ✔  1             instr.Mnemonic = regField switch
 709  ✔  1             {
 710  ✔  1                 0 => Mnemonic.INC,
 711  ✔  1                 1 => Mnemonic.DEC,
 712  ✔  1                 2 => Mnemonic.CALL,
 713  ✔  1                 3 => Mnemonic.CALL_FAR,
 714  ✔  1                 4 => Mnemonic.JMP,
 715  ✔  1                 5 => Mnemonic.JMP_FAR,
 716  ✔  1                 6 => Mnemonic.PUSH,
 717  ✔  1                 _ => Mnemonic.DB
 718  ✔  1             };
 719  ✔  1             return instr;
 720  ✔  1         }
 721           
 722               private Instruction DecodeMovRegMem(byte opcode)
 723  ✔  1         {
 724  ✔  1             byte modrm = ReadByte();
 725  ✔  1             int mod = (modrm >> 6) & 3;
 726  ✔  1             int reg = (modrm >> 3) & 7;
 727  ✔  1             int rm = modrm & 7;
 728  ✔  1             bool word = (opcode & 1) == 1;
 729           
 730  ✔  1             var instr = new Instruction() { Mnemonic = Mnemonic.MOV };
 731           
 732  ✔  1             if ((opcode & 2) != 0)
 733  ✔  1             {
 734  ✓  1                 instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, reg);
 735  ✔  1                 if (mod == 3)
 736  ❌ 0                     instr.Operand2 = new Operand(word ? OperandType.Register16 : OperandType.Register8, rm);
 737                       else
 738  ✔  1                     instr.Operand2 = ParseMemoryOperand(rm, mod);
 739  ✔  1             }
 740                   else
 741  ❌ 0             {
 742  ❌ 0                 if (mod == 3)
 743  ❌ 0                     instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, rm);
 744                       else
 745  ❌ 0                     instr.Operand1 = ParseMemoryOperand(rm, mod);
 746  ❌ 0                 instr.Operand2 = new Operand(word ? OperandType.Register16 : OperandType.Register8, reg);
 747  ❌ 0             }
 748           
 749  ✔  1             return instr;
 750  ✔  1         }
 751           
 752               private Instruction DecodeMovRegImm(byte opcode)
 753  ✔  1         {
 754  ✔  1             bool word = opcode >= 0xB8;
 755  ✓  1             int regIndex = opcode - (word ? 0xB8 : 0xB0);
 756  ✓  1             ushort imm = word ? ReadUInt16() : ReadByte();
 757  ✓  1             return new Instruction
 758  ✓  1             {
 759  ✓  1                 Mnemonic = Mnemonic.MOV,
 760  ✓  1                 Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, regIndex),
 761  ✓  1                 Operand2 = new Operand(word ? OperandType.Immediate16 : OperandType.Immediate8, imm)
 762  ✓  1             };
 763  ✔  1         }
 764           
 765               private Instruction DecodeMovMemImm(byte opcode)
 766  ❌ 0         {
 767  ❌ 0             byte modrm = ReadByte();
 768  ❌ 0             int mod = (modrm >> 6) & 3;
 769  ❌ 0             int rm = modrm & 7;
 770  ❌ 0             bool word = (opcode & 1) == 1;
 771           
 772  ❌ 0             var instr = new Instruction { Mnemonic = Mnemonic.MOV };
 773  ❌ 0             if (mod == 3)
 774  ❌ 0                 instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, rm);
 775                   else
 776  ❌ 0                 instr.Operand1 = ParseMemoryOperand(rm, mod);
 777  ❌ 0             instr.Operand2 = new Operand(word ? OperandType.Immediate16 : OperandType.Immediate8, word ? ReadUInt16() : Read
 778  ❌ 0             return instr;
 779  ❌ 0         }
 780           
 781               private Instruction DecodeMovAxMem(byte opcode)
 782  ❌ 0         {
 783  ❌ 0             ushort disp = ReadUInt16();
 784           
 785  ❌ 0             var instr = new Instruction { Mnemonic = Mnemonic.MOV };
 786  ❌ 0             if (opcode == 0xA0)
 787  ❌ 0             {
 788  ❌ 0                 instr.Operand1 = new Operand(OperandType.Register8, 0);
 789  ❌ 0                 instr.Operand2 = new Operand(OperandType.Memory, disp);
 790  ❌ 0             }
 791  ❌ 0             if (opcode == 0xA1)
 792  ❌ 0             {
 793  ❌ 0                 instr.Operand1 = new Operand(OperandType.Register16, 0);
 794  ❌ 0                 instr.Operand2 = new Operand(OperandType.Memory, disp);
 795  ❌ 0             }
 796  ❌ 0             if (opcode == 0xA2)
 797  ❌ 0             {
 798  ❌ 0                 instr.Operand1 = new Operand(OperandType.Memory, disp);
 799  ❌ 0                 instr.Operand2 = new Operand(OperandType.Register8, 0);
 800  ❌ 0             }
 801  ❌ 0             if (opcode == 0xA3)
 802  ❌ 0             {
 803  ❌ 0                 instr.Operand1 = new Operand(OperandType.Memory, disp);
 804  ❌ 0                 instr.Operand2 = new Operand(OperandType.Register16, 0);
 805  ❌ 0             }
 806  ❌ 0             return instr;
 807  ❌ 0         }
 808           
 809               private Instruction DecodeMovSreg(byte opcode)
 810  ❌ 0         {
 811  ❌ 0             byte modrm = ReadByte();
 812  ❌ 0             int mod = (modrm >> 6) & 3;
 813  ❌ 0             int sreg = (modrm >> 3) & 7;
 814  ❌ 0             int rm = modrm & 7;
 815           
 816  ❌ 0             var instr = new Instruction
 817  ❌ 0             {
 818  ❌ 0                 Mnemonic = Mnemonic.MOV,
 819  ❌ 0                 Operand2 = new Operand(OperandType.SegmentRegister, sreg)
 820  ❌ 0             };
 821  ❌ 0             if (mod == 3)
 822  ❌ 0                 instr.Operand1 = new Operand(OperandType.Register16, rm);
 823                   else
 824  ❌ 0                 instr.Operand1 = ParseMemoryOperand(rm, mod);
 825  ❌ 0             return instr;
 826  ❌ 0         }
 827           
 828               private Instruction DecodeShortJump(byte opcode)
 829  ✔  1         {
 830  ✔  1             sbyte rel = (sbyte)ReadByte();
 831  ✔  1             int target = _pos + rel;
 832           
 833  ✔  1             Mnemonic mnem = opcode switch
 834  ✔  1             {
 835  ✔  1                 0x70 => Mnemonic.JO,
 836  ✔  1                 0x71 => Mnemonic.JNO,
 837  ✔  1                 0x72 => Mnemonic.JB,
 838  ✔  1                 0x73 => Mnemonic.JAE,
 839  ✔  1                 0x74 => Mnemonic.JE,
 840  ✔  1                 0x75 => Mnemonic.JNE,
 841  ✔  1                 0x76 => Mnemonic.JBE,
 842  ✔  1                 0x77 => Mnemonic.JA,
 843  ✔  1                 0x78 => Mnemonic.JS,
 844  ✔  1                 0x79 => Mnemonic.JNS,
 845  ✔  1                 0x7A => Mnemonic.JP,
 846  ✔  1                 0x7B => Mnemonic.JNP,
 847  ✔  1                 0x7C => Mnemonic.JL,
 848  ✔  1                 0x7D => Mnemonic.JGE,
 849  ✔  1                 0x7E => Mnemonic.JLE,
 850  ✔  1                 0x7F => Mnemonic.JG,
 851  ✔  1                 0xE3 => Mnemonic.JCXZ,
 852  ✔  1                 0xEB => Mnemonic.JMP,
 853  ✔  1                 _ => Mnemonic.DB
 854  ✔  1             };
 855           
 856  ✔  1             var instr = new Instruction
 857  ✔  1             {
 858  ✔  1                 Mnemonic = mnem,
 859  ✔  1                 Operand1 = new Operand(OperandType.Relative8, target)
 860  ✔  1             };
 861  ✔  1             return instr;
 862  ✔  1         }
 863           
 864               private Instruction DecodeNearJump()
 865  ✔  1         {
 866  ✔  1             short rel = (short)ReadUInt16();
 867  ✔  1             int target = _pos + rel;
 868           
 869  ✔  1             var instr = new Instruction
 870  ✔  1             {
 871  ✔  1                 Mnemonic = Mnemonic.JMP,
 872  ✔  1                 Operand1 = new Operand(OperandType.Relative16, target)
 873  ✔  1             };
 874  ✔  1             return instr;
 875  ✔  1         }
 876           
 877               private Instruction DecodeLea()
 878  ✔  1         {
 879  ✔  1             byte modrm = ReadByte();
 880  ✔  1             int mod = (modrm >> 6) & 3;
 881  ✔  1             int reg = (modrm >> 3) & 7;
 882  ✔  1             int rm = modrm & 7;
 883           
 884  ✔  1             var instr = new Instruction
 885  ✔  1             {
 886  ✔  1                 Mnemonic = Mnemonic.LEA,
 887  ✔  1                 Operand1 = new Operand(OperandType.Register16, reg)
 888  ✔  1             };
 889  ✔  1             if (mod == 3)
 890  ❌ 0                 instr.Operand2 = new Operand(OperandType.Register16, rm);
 891                   else
 892  ✔  1                 instr.Operand2 = ParseMemoryOperand(rm, mod);
 893  ✔  1             return instr;
 894  ✔  1         }
 895           
 896               private Instruction DecodeXchg(byte opcode)
 897  ✔  1         {
 898  ✔  1             byte modrm = ReadByte();
 899  ✔  1             int mod = (modrm >> 6) & 3;
 900  ✔  1             int reg = (modrm >> 3) & 7;
 901  ✔  1             int rm = modrm & 7;
 902  ✔  1             bool word = (opcode & 1) == 1;
 903           
 904  ✔  1             var instr = new Instruction { Mnemonic = Mnemonic.XCHG };
 905  ✓  1             instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, reg);
 906  ✔  1             if (mod == 3)
 907  ❌ 0                 instr.Operand2 = new Operand(word ? OperandType.Register16 : OperandType.Register8, rm);
 908                   else
 909  ✔  1                 instr.Operand2 = ParseMemoryOperand(rm, mod);
 910  ✔  1             return instr;
 911  ✔  1         }
 912           
 913               private Instruction DecodeTestModRm(byte opcode)
 914  ❌ 0         {
 915  ❌ 0             byte modrm = ReadByte();
 916  ❌ 0             int mod = (modrm >> 6) & 3;
 917  ❌ 0             int reg = (modrm >> 3) & 7;
 918  ❌ 0             int rm = modrm & 7;
 919  ❌ 0             bool word = (opcode & 1) == 1;
 920           
 921  ❌ 0             var instr = new Instruction { Mnemonic = Mnemonic.TEST };
 922  ❌ 0             instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, reg);
 923  ❌ 0             if (mod == 3)
 924  ❌ 0                 instr.Operand2 = new Operand(word ? OperandType.Register16 : OperandType.Register8, rm);
 925                   else
 926  ❌ 0                 instr.Operand2 = ParseMemoryOperand(rm, mod);
 927  ❌ 0             return instr;
 928  ❌ 0         }
 929           
 930               private Instruction DecodeTestAxImm(byte opcode)
 931  ✔  1         {
 932  ✔  1             bool word = (opcode & 1) == 1;
 933  ✓  1             ushort imm = word ? ReadUInt16() : ReadByte();
 934  ✓  1             var instr = new Instruction
 935  ✓  1             {
 936  ✓  1                 Mnemonic = Mnemonic.TEST,
 937  ✓  1                 Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, 0),
 938  ✓  1                 Operand2 = new Operand(word ? OperandType.Immediate16 : OperandType.Immediate8, imm)
 939  ✓  1             };
 940  ✔  1             return instr;
 941  ✔  1         }
 942           
 943               private Instruction DecodeShift(byte opcode)
 944  ✔  1         {
 945  ✔  1             byte modrm = ReadByte();
 946  ✔  1             int mod = (modrm >> 6) & 3;
 947  ✔  1             int regField = (modrm >> 3) & 7;
 948  ✔  1             bool word = (opcode & 1) == 1;
 949           
 950  ✔  1             Mnemonic mnem = regField switch
 951  ✔  1             {
 952  ✔  1                 0 => Mnemonic.ROL,
 953  ✔  1                 1 => Mnemonic.ROR,
 954  ✔  1                 2 => Mnemonic.RCL,
 955  ✔  1                 3 => Mnemonic.RCR,
 956  ✔  1                 4 => Mnemonic.SAL,
 957  ✔  1                 5 => Mnemonic.SHR,
 958  ✔  1                 6 => Mnemonic.SAL,
 959  ✔  1                 7 => Mnemonic.SAR,
 960  ✔  1                 _ => Mnemonic.DB
 961  ✔  1             };
 962           
 963  ✔  1             var instr = new Instruction { Mnemonic = mnem };
 964  ✔  1             if (mod == 3)
 965  ✓  1                 instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, modrm & 7);
 966                   else
 967  ❌ 0                 instr.Operand1 = ParseMemoryOperand(modrm & 7, mod);
 968                   // CL or 1
 969  ✔  1             if ((opcode & 2) != 0)   // D2 и D3
 970  ✔  1                 instr.Operand2 = new Operand(OperandType.Register8, 1); // CL (регистр CL = 1)
 971                   else                     // D0 и D1
 972  ❌ 0                 instr.Operand2 = new Operand(OperandType.Immediate8, 1);
 973  ✔  1             return instr;
 974  ✔  1         }
 975               private Instruction DecodeLoop(byte opcode)
 976  ✔  1         {
 977  ✔  1             sbyte rel = (sbyte)ReadByte();
 978  ✔  1             int target = _pos + rel;
 979  ✔  1             Mnemonic mnem = opcode switch
 980  ✔  1             {
 981  ✔  1                 0xE0 => Mnemonic.LOOPNE,
 982  ✔  1                 0xE1 => Mnemonic.LOOPE,
 983  ✔  1                 0xE2 => Mnemonic.LOOP,
 984  ✔  1                 _ => Mnemonic.LOOP
 985  ✔  1             };
 986           
 987  ✔  1             return new Instruction
 988  ✔  1             {
 989  ✔  1                 Mnemonic = mnem,
 990  ✔  1                 Operand1 = new Operand(OperandType.Relative8, target)
 991  ✔  1             };
 992  ✔  1         }
 993           
 994               private Operand ParseMemoryOperand(int rm, int mod)
 995  ✔  1         {
 996  ✔  1             int disp = 0;
 997  ✔  1             if (mod == 1)
 998  ✔  1                 disp = (sbyte)ReadByte();
 999  ✔  1             else if (mod == 2)
1000  ✔  1                 disp = (short)ReadUInt16();
1001           
1002  ✔  1             AddressRegister baseReg = AddressRegister.None;
1003  ✔  1             AddressRegister indexReg = AddressRegister.None;
1004           
1005  ✔  1             switch (rm)
1006                   {
1007  ✔  1                 case 0: baseReg = AddressRegister.BX; indexReg = AddressRegister.SI; break; // [BX+SI]
1008  ❌ 0                 case 1: baseReg = AddressRegister.BX; indexReg = AddressRegister.DI; break; // [BX+DI]
1009  ❌ 0                 case 2: baseReg = AddressRegister.BP; indexReg = AddressRegister.SI; break; // [BP+SI]
1010  ✔  1                 case 3: baseReg = AddressRegister.BP; indexReg = AddressRegister.DI; break; // [BP+DI]
1011  ✔  1                 case 4: baseReg = AddressRegister.SI; break; // [SI]
1012  ❌ 0                 case 5: baseReg = AddressRegister.DI; break; // [DI]
1013                       case 6:
1014  ✔  1                     if (mod == 0)
1015  ✔  1                     {
1016  ✔  1                         disp = ReadUInt16();
1017  ✔  1                         baseReg = AddressRegister.None; // direct
1018  ✔  1                     }
1019                           else
1020  ❌ 0                         baseReg = AddressRegister.BP; // [BP+disp]
1021  ✔  1                     break;
1022  ✔  1                 case 7: baseReg = AddressRegister.BX; break; // [BX]
1023                   }
1024           
1025  ✔  1             return new Operand(OperandType.Memory, disp, baseReg, indexReg);
1026  ✔  1         }
1027           
1028               private static Mnemonic GetAluMnemonicEnum(byte opcode)
1029  ✔  1         {
1030  ✔  1             return ((opcode >> 3) & 7) switch
1031  ✔  1             {
1032  ✔  1                 0 => Mnemonic.ADD,
1033  ✔  1                 1 => Mnemonic.OR,
1034  ✔  1                 2 => Mnemonic.ADC,
1035  ✔  1                 3 => Mnemonic.SBB,
1036  ✔  1                 4 => Mnemonic.AND,
1037  ✔  1                 5 => Mnemonic.SUB,
1038  ✔  1                 6 => Mnemonic.XOR,
1039  ✔  1                 7 => Mnemonic.CMP,
1040  ✔  1                 _ => Mnemonic.DB
1041  ✔  1             };
1042  ✔  1         }
1043           
1044  ✔  1         private byte ReadByte() => _image[_pos++];
1045           
1046               private ushort ReadUInt16()
1047  ✔  1         {
1048  ✔  1             ushort val = (ushort)(_image[_pos] | (_image[_pos + 1] << 8));
1049  ✔  1             _pos += 2;
1050  ✔  1             return val;
1051  ✔  1         }
1052           }
```
