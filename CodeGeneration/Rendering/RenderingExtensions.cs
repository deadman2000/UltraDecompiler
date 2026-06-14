using System.Text;

namespace UltraDecompiler.CodeGeneration.Rendering;

/// <summary>Extension-методы рендеринга IR в синтаксис C.</summary>
public static class RenderingExtensions
{
    extension(Expr expr)
    {
        /// <summary>Рендерит выражение IR в синтаксис C с учётом приоритетов операторов.</summary>
        public string RenderExpr(int parentPrec = 0) =>
            CExprRenderer.Render(expr, parentPrec);
    }

    extension(Operation op)
    {
        public string ToCString(int indent = 0, bool asStatement = false) =>
            COperationRenderer.Render(op, indent, asStatement);

        public void AppendToCString(StringBuilder sb, int indent = 0, bool asStatement = false) =>
            COperationRenderer.Append(sb, op, indent, asStatement);
    }
}
