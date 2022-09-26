using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PreStormCore;

public static class ExpressionExt
{
    public static string ToWhereClause<T>(this Expression<Func<T, bool>> expression)
        where T : Feature
    {
        return PredicateVisitor.Eval(expression);
    }

    private static string? GetValue(this object? obj)
    {
        return obj switch
        {
            string s => $@"'{s}'",
            DateTime t => $"TIMESTAMP '{t:yyyy-MM-dd HH:mm:ss}'",
            Enum e => e.GetType().GetMembers().Single(x => x.Name == e.ToString()).GetCustomAttribute<Domain>()!.Code.GetValue(),
            null => null,
            _ => obj.ToString()
        };
    }

    private class PredicateVisitor : ExpressionVisitor
    {
        private readonly List<string?> expressions = new();

        protected override Expression VisitBinary(BinaryExpression node)
        {
            var flipped = node.Left is ConstantExpression || node.Left is MemberExpression m && m.Member is FieldInfo;

            var (left, right) = (Eval(node.Left), Eval(node.Right));

            expressions.Add((node.NodeType, flipped) switch
            {
                (ExpressionType.AndAlso, _) => $"({left} AND {right})",
                (ExpressionType.OrElse, _) => $"({left} OR {right})",

                (ExpressionType.Equal, _) => right is null ? $"({left} IS NULL)" : left is null ? $"({right} IS NULL)" : $"({left} = {right})",
                (ExpressionType.NotEqual, _) => right is null ? $"({left} IS NOT NULL)" : left is null ? $"({right} IS NOT NULL)" : $"({left} <> {right})",

                (ExpressionType.GreaterThan, false) => $"({left} > {right})",
                (ExpressionType.GreaterThanOrEqual, false) => $"({left} >= {right})",
                (ExpressionType.LessThan, false) => $"({left} < {right})",
                (ExpressionType.LessThanOrEqual, false) => $"({left} <= {right})",

                (ExpressionType.GreaterThan, true) => $"({right} < {left})",
                (ExpressionType.GreaterThanOrEqual, true) => $"({right} <= {left})",
                (ExpressionType.LessThan, true) => $"({right} > {left})",
                (ExpressionType.LessThanOrEqual, true) => $"({right} >= {left})",

                _ => throw new InvalidOperationException($"'{node.NodeType}' is not supported.")
            });

            return base.VisitBinary(node);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            expressions.Add(node.Value.GetValue());

            return base.VisitConstant(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ConstantExpression c && node.Member is FieldInfo f && f.GetValue(c.Value) is object o)
                expressions.Add(o.GetValue());
            else
                expressions.Add(node.Member.GetCustomAttribute<Mapped>()!.FieldName);

            return base.VisitMember(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var method = node.Method.Name;

            expressions.Add(method switch
            {
                "Contains" => $"({Eval(node.Object!)} LIKE {Regex.Replace(Regex.Replace(Eval(node.Arguments[0]), "'$", "%'"), "^'", "'%")})",
                "EndsWith" => $"({Eval(node.Object!)} LIKE {Regex.Replace(Eval(node.Arguments[0]), "^'", "'%")})",
                "StartsWith" => $"({Eval(node.Object!)} LIKE {Regex.Replace(Eval(node.Arguments[0]), "'$", "%'")})",

                _ => throw new InvalidOperationException($"'{method}' is not supported.")
            });

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType == ExpressionType.Convert)
                expressions.Add(Eval(node.Operand));
            else if (node.NodeType == ExpressionType.Not)
                expressions.Add($"NOT ({Eval(node.Operand)})");
            else
                throw new InvalidOperationException($"'{node.NodeType}' is not supported.");

            return base.VisitUnary(node);
        }

        public static string Eval(Expression e)
        {
            var visitor = new PredicateVisitor();
            visitor.Visit(e);
            return visitor.expressions.First()!;
        }
    }
}
