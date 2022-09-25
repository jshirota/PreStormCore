using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PreStormCore;

internal class PredicateVisitor : ExpressionVisitor
{
    private readonly List<string> expressions = new();

    private PredicateVisitor() { }

    private static Dictionary<object, object?> _(object key, object? value) => new() { { key, value } };

    protected override Expression VisitBinary(BinaryExpression node)
    {
        var flipped = node.Left is ConstantExpression || node.Left is MemberExpression m && m.Member is FieldInfo;

        var (left, right) = (Eval(node.Left), Eval(node.Right));

        var s = (node.NodeType, flipped) switch
        {
            (ExpressionType.AndAlso, _) => $"({left} AND {right})",
            (ExpressionType.OrElse, _) => $"({left} OR {right})",

            (ExpressionType.Equal, false) => $"({left} = {right})",
            (ExpressionType.NotEqual, false) => $"({left} <> {right})",
            (ExpressionType.GreaterThan, false) => $"({left} > {right})",
            (ExpressionType.GreaterThanOrEqual, false) => $"({left} >= {right})",
            (ExpressionType.LessThan, false) => $"({left} < {right})",
            (ExpressionType.LessThanOrEqual, false) => $"({left} <= {right})",

            (ExpressionType.Equal, true) => $"({right} AND {left})",
            (ExpressionType.NotEqual, true) => $"({right} OR {left})",
            (ExpressionType.GreaterThan, true) => $"({right} < {left})",
            (ExpressionType.GreaterThanOrEqual, true) => $"({right} <= {left})",
            (ExpressionType.LessThan, true) => $"({right} > {left})",
            (ExpressionType.LessThanOrEqual, true) => $"({right} >= {left})",

            _ => throw new InvalidOperationException($"'{node.NodeType}' is not supported.")
        };

        expressions.Add(s);

        return base.VisitBinary(node);
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        expressions.Add(node.Value is string s ? $@"'{s}'" : node.Value?.ToString()!);

        return base.VisitConstant(node);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is ConstantExpression c && node.Member is FieldInfo f && f.GetValue(c.Value) is object o)
            expressions.Add(o is string s ? $@"'{s}'" : o!.ToString()!);
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
            "StartsWith" => $"({Eval(node.Object!)} LIKE {Regex.Replace(Eval(node.Arguments[0]), "'$", "%'")})",

            _ => throw new InvalidOperationException($"'{method}' is not supported.")
        }); ;

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
        return visitor.expressions.First();
    }
}
