using System;
using System.Collections.Generic;
using System.Linq;

namespace UltraDecompiler;

/// <summary>
/// Автоматически находит функции в дизассемблированном коде
/// </summary>
public static class FunctionDetector
{
    /// <summary>
    /// Находит все функции в списке инструкций
    /// </summary>
    public static List<Function> DetectFunctions(List<Instruction> instructions, ControlFlowGraph cfg)
    {
        var functions = new List<Function>();
        if (instructions == null || instructions.Count == 0)
            return functions;

        // 1. Находим все CALL инструкции и их цели
        var callTargets = new HashSet<int>();

        foreach (var instr in instructions)
        {
            if (instr.Mnemonic.Equals("CALL", StringComparison.OrdinalIgnoreCase))
            {
                if (TryGetCallTarget(instr, out int target))
                {
                    callTargets.Add(target);
                }
            }
        }

        // 2. Для каждой цели CALL создаём функцию
        foreach (int entryPoint in callTargets)
        {
            var func = new Function
            {
                EntryPoint = entryPoint,
                Name = $"sub_{entryPoint:X4}"
            };

            // Находим все блоки, принадлежащие этой функции
            func.Blocks = FindFunctionBlocks(cfg, entryPoint);

            functions.Add(func);
        }

        // 3. Если есть точка входа программы, добавляем её как главную функцию
        if (instructions.Count > 0)
        {
            int mainEntry = instructions[0].Offset;
            if (!functions.Any(f => f.EntryPoint == mainEntry))
            {
                var mainFunc = new Function
                {
                    EntryPoint = mainEntry,
                    Name = "main"
                };
                mainFunc.Blocks = FindFunctionBlocks(cfg, mainEntry);
                functions.Insert(0, mainFunc);
            }
        }

        return functions;
    }

    private static List<BasicBlock> FindFunctionBlocks(ControlFlowGraph cfg, int entryPoint)
    {
        var blocks = new List<BasicBlock>();
        var visited = new HashSet<int>();

        // Находим блок с точкой входа
        var entryBlock = cfg.Blocks.FirstOrDefault(b => b.StartOffset == entryPoint);
        if (entryBlock == null)
            return blocks;

        // Простой обход в глубину (DFS) для сбора всех блоков функции
        var stack = new Stack<BasicBlock>();
        stack.Push(entryBlock);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (visited.Contains(current.StartOffset))
                continue;

            visited.Add(current.StartOffset);
            blocks.Add(current);

            // Добавляем successors, которые ещё не посещены
            foreach (var succ in current.Successors)
            {
                if (!visited.Contains(succ.StartOffset))
                {
                    stack.Push(succ);
                }
            }
        }

        return blocks.OrderBy(b => b.StartOffset).ToList();
    }

    private static bool TryGetCallTarget(Instruction instr, out int target)
    {
        target = 0;
        if (string.IsNullOrEmpty(instr.Operands))
            return false;

        var parts = instr.Operands.Split(new[] { ' ', ',', ':' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith("0x") && int.TryParse(part[2..], System.Globalization.NumberStyles.HexNumber, null, out int addr))
            {
                target = addr;
                return true;
            }
        }
        return false;
    }

    public static void PrintFunctions(List<Function> functions)
    {
        Console.WriteLine("\n=== Detected Functions ===");
        foreach (var func in functions)
        {
            Console.WriteLine($"{func.Name} @ 0x{func.EntryPoint:X6} ({func.Blocks.Count} blocks)");
            foreach (var block in func.Blocks.Take(3))
            {
                Console.WriteLine($"   {block}");
            }
            if (func.Blocks.Count > 3)
                Console.WriteLine("   ...");
        }
    }
}