// Copyright (c) qlcheck contributors.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Secrets;

namespace Qlcheck.Languages.CSharp.Catalog.Queries;

// Where a constant sits, for the secret classifier: the name it is stored under, and whether it is
// a connection-string builder password. Without a semantic model only the syntax is read, so a
// positional argument has no name.
internal static class SecretSlots
{
    private const string ConnectionStringBuilderSuffix = "ConnectionStringBuilder";

    private const string KeyValuePairType = "KeyValuePair";

    private static readonly HashSet<string> DictionaryAdds = new(StringComparer.Ordinal)
    {
        "Add", "TryAdd", "Set", "Create",
    };

    private static readonly HashSet<string> ConnectionPasswordProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password", "Pwd",
    };

    // The name and configuration key path the value is stored under, from the ten places a
    // hard-coded value can be named: a declarator, a property or method body, an assignment or
    // initializer member, a named or resolved positional argument, a dictionary entry, an anonymous
    // member, a default parameter value, or a comparison.
    public static (string? Name, string? KeyPath) NameOf(SemanticModel? model, ExpressionSyntax value)
    {
        var node = Outer(value);
        return node.Parent switch
        {
            EqualsValueClauseSyntax equals => (DeclaredName(equals.Parent), null),
            ArrowExpressionClauseSyntax arrow => (MemberName(arrow.Parent), null),
            ReturnStatementSyntax ret => (EnclosingMemberName(ret), null),
            AssignmentExpressionSyntax assignment when assignment.Right == node => AssignedName(model, assignment.Left),
            AnonymousObjectMemberDeclaratorSyntax { NameEquals: { } nameEquals } => (nameEquals.Name.Identifier.ValueText, null),
            ArgumentSyntax argument => ArgumentName(model, argument),
            BinaryExpressionSyntax binary when IsEquality(binary) => (ExpressionName(binary.Left == node ? binary.Right : binary.Left), null),
            InitializerExpressionSyntax { Expressions: [var key, var entry] } init
                when init.IsKind(SyntaxKind.ComplexElementInitializerExpression) && entry == node =>
                Keyed(ConstantKey(model, key)),
            _ => (null, null),
        };
    }

    // `builder.Password = "..."` or `new SqlConnectionStringBuilder { Password = "..." }`.
    public static bool IsConnectionStringSlot(SemanticModel? model, ExpressionSyntax value)
    {
        var node = Outer(value);
        if (node.Parent is not AssignmentExpressionSyntax assignment || assignment.Right != node)
        {
            return false;
        }

        return assignment.Left switch
        {
            MemberAccessExpressionSyntax member when ConnectionPasswordProperties.Contains(member.Name.Identifier.ValueText) =>
                IsBuilder(model, member.Expression, null),
            IdentifierNameSyntax name when ConnectionPasswordProperties.Contains(name.Identifier.ValueText)
                && assignment.Parent is InitializerExpressionSyntax { Parent: BaseObjectCreationExpressionSyntax creation } =>
                IsBuilder(model, creation, (creation as ObjectCreationExpressionSyntax)?.Type),
            _ => false,
        };
    }

    // The constant string a node evaluates to, from a literal or (with a model) any constant expression.
    public static string? ConstantString(SemanticModel? model, ExpressionSyntax expression)
    {
        if (model?.GetConstantValue(expression) is { HasValue: true, Value: string constant })
        {
            return constant;
        }

        return expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal.Token.ValueText
            : null;
    }

    public static ExpressionSyntax Outer(ExpressionSyntax value)
    {
        var node = value;
        while (node.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax or PostfixUnaryExpressionSyntax
            && node.Parent is ExpressionSyntax parent)
        {
            node = parent;
        }

        return node;
    }

    private static (string? Name, string? KeyPath) Keyed(string? key) => (SecretNames.LastSegment(key), key);

    private static string? DeclaredName(SyntaxNode? declaration) =>
        declaration switch
        {
            VariableDeclaratorSyntax variable => variable.Identifier.ValueText,
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            ParameterSyntax parameter => parameter.Identifier.ValueText,
            _ => null,
        };

    private static string? MemberName(SyntaxNode? member) =>
        member switch
        {
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            LocalFunctionStatementSyntax function => function.Identifier.ValueText,
            AccessorDeclarationSyntax { Parent.Parent: PropertyDeclarationSyntax property } => property.Identifier.ValueText,
            _ => null,
        };

    // A return inside a lambda belongs to no named member.
    private static string? EnclosingMemberName(ReturnStatementSyntax ret)
    {
        foreach (var ancestor in ret.Ancestors())
        {
            if (ancestor is AnonymousFunctionExpressionSyntax)
            {
                return null;
            }

            if (MemberName(ancestor) is { } name)
            {
                return name;
            }
        }

        return null;
    }

    private static (string? Name, string? KeyPath) AssignedName(SemanticModel? model, ExpressionSyntax left) =>
        left switch
        {
            ElementAccessExpressionSyntax element when element.ArgumentList.Arguments.Count == 1 =>
                Keyed(ConstantKey(model, element.ArgumentList.Arguments[0].Expression)),
            ImplicitElementAccessSyntax element when element.ArgumentList.Arguments.Count == 1 =>
                Keyed(ConstantKey(model, element.ArgumentList.Arguments[0].Expression)),
            _ => (ExpressionName(left), null),
        };

    private static (string? Name, string? KeyPath) ArgumentName(SemanticModel? model, ArgumentSyntax argument)
    {
        if (argument.NameColon is { } nameColon)
        {
            return (nameColon.Name.Identifier.ValueText, null);
        }

        if (argument.Parent is not ArgumentListSyntax list || list.Parent is null)
        {
            return (null, null);
        }

        if (list.Arguments is [var key, var entry] && entry == argument && IsKeyValueCall(list.Parent))
        {
            return Keyed(ConstantKey(model, key.Expression));
        }

        if (model is null || LiteralContext.SymbolOf(model, list.Parent) is not IMethodSymbol method)
        {
            return (null, null);
        }

        return (LiteralContext.ParameterOf(method, list, argument)?.Name, null);
    }

    // `dict.Add("password", "...")`, `new KeyValuePair<string, string>("password", "...")`,
    // `KeyValuePair.Create("password", "...")`.
    private static bool IsKeyValueCall(SyntaxNode call) =>
        call switch
        {
            InvocationExpressionSyntax invocation => DictionaryAdds.Contains(Names.Invocation(invocation)),
            ObjectCreationExpressionSyntax creation => TypeName(creation.Type) == KeyValuePairType,
            _ => false,
        };

    private static string? ConstantKey(SemanticModel? model, ExpressionSyntax expression) => ConstantString(model, expression);

    private static string? ExpressionName(ExpressionSyntax expression) =>
        expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            _ => null,
        };

    private static bool IsBuilder(SemanticModel? model, ExpressionSyntax receiver, TypeSyntax? declared)
    {
        var name = model?.GetTypeInfo(receiver).Type?.Name;
        if (string.IsNullOrEmpty(name) && declared is not null)
        {
            name = TypeName(declared);
        }

        return name?.EndsWith(ConnectionStringBuilderSuffix, StringComparison.Ordinal) == true;
    }

    private static string TypeName(TypeSyntax type) =>
        type switch
        {
            GenericNameSyntax generic => generic.Identifier.ValueText,
            QualifiedNameSyntax qualified => TypeName(qualified.Right),
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            _ => type.ToString(),
        };

    private static bool IsEquality(BinaryExpressionSyntax binary) =>
        binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression);
}
