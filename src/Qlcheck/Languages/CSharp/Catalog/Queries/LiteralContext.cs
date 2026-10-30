// Copyright (c) qlcheck contributors.
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qlcheck.Languages.CSharp.Catalog.Queries;

// Decides which literals are magic-literal candidates and which target ("context") a literal feeds,
// so the same value is matched across files only when it is used for the same thing.
internal static class LiteralContext
{
    private const string ExceptionSuffix = "Exception";

    private const int TruncateLimit = 40;

    private const int TruncateKeep = 37;

    private const string Ellipsis = "...";

    public static bool IsCandidate(LiteralExpressionSyntax literal) =>
        literal.Kind() switch
        {
            SyntaxKind.NumericLiteralExpression => IsNumberCandidate(literal),
            SyntaxKind.StringLiteralExpression => IsStringCandidate(literal),
            _ => false,
        };

    // Widened to a wrapping unary minus so `-5` is one value.
    public static ExpressionSyntax ValueExpression(LiteralExpressionSyntax literal) =>
        literal.Parent is PrefixUnaryExpressionSyntax unary && unary.IsKind(SyntaxKind.UnaryMinusExpression)
            ? unary
            : literal;

    public static string Label(LiteralExpressionSyntax literal)
    {
        if (literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return $"Magic string \"{Truncate(literal.Token.ValueText)}\"";
        }

        return $"Magic number {ValueExpression(literal)}";
    }

    public static string Truncate(string value) =>
        value.Length <= TruncateLimit ? value : $"{value[..TruncateKeep]}{Ellipsis}";

    // The resolved parameter, field, property or indexer the value feeds, or null when there is none.
    public static LiteralTarget? TargetOf(SemanticModel model, ExpressionSyntax value)
    {
        SyntaxNode node = value;
        while (node.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            node = parenthesized;
        }

        return node.Parent switch
        {
            ArgumentSyntax argument => ArgumentTarget(model, argument),
            BinaryExpressionSyntax binary when IsEquality(binary) =>
                MemberTarget(model, binary.Left == node ? binary.Right : binary.Left),
            AssignmentExpressionSyntax assignment
                when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) && assignment.Right == node =>
                MemberTarget(model, assignment.Left),
            ConstantPatternSyntax pattern => MemberTarget(model, PatternSubject(pattern)),
            CaseSwitchLabelSyntax label => MemberTarget(model, (label.Parent?.Parent as SwitchStatementSyntax)?.Expression),
            EqualsValueClauseSyntax equals => DeclarationTarget(model, equals),
            _ => null,
        };
    }

    private static bool IsNumberCandidate(LiteralExpressionSyntax literal) =>
        !IsAllowedNumber(literal.Token.Value) && !IsIgnoredContext(literal);

    private static bool IsStringCandidate(LiteralExpressionSyntax literal)
    {
        var value = literal.Token.ValueText;
        if (value.Length == 0 || value.Length > Thresholds.MagicStringLength)
        {
            return false;
        }

        if (IsMessage(value, literal) || IsDottedName(value) || SqlText.LooksLikeSql(value))
        {
            return false;
        }

        return !IsIgnoredContext(literal) && !IsCollectionElement(literal);
    }

    private static bool IsEquality(BinaryExpressionSyntax binary) =>
        binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression);

    // Falls back to a lone candidate so a call still resolves when overload resolution fails
    // on incomplete references.
    public static ISymbol? SymbolOf(SemanticModel model, SyntaxNode node)
    {
        var info = model.GetSymbolInfo(node);
        return info.Symbol ?? (info.CandidateSymbols.Length == 1 ? info.CandidateSymbols[0] : null);
    }

    private static LiteralTarget? ArgumentTarget(SemanticModel model, ArgumentSyntax argument)
    {
        if (argument.Parent is not BaseArgumentListSyntax list || list.Parent is null)
        {
            return null;
        }

        return SymbolOf(model, list.Parent) switch
        {
            IMethodSymbol method => ParameterTarget(method, ParameterOf(method, list, argument)),
            IPropertySymbol { IsIndexer: true } indexer => LiteralTarget.Of(indexer),
            _ => null,
        };
    }

    internal static IParameterSymbol? ParameterOf(IMethodSymbol method, BaseArgumentListSyntax list, ArgumentSyntax argument)
    {
        var parameters = method.Parameters;
        if (argument.NameColon is { } name)
        {
            return parameters.FirstOrDefault(parameter => parameter.Name == name.Name.Identifier.ValueText);
        }

        var index = list.Arguments.IndexOf(argument);
        if (index < parameters.Length)
        {
            return parameters[index];
        }

        return parameters.Length > 0 && parameters[^1].IsParams ? parameters[^1] : null;
    }

    private static LiteralTarget? ParameterTarget(IMethodSymbol method, IParameterSymbol? parameter) =>
        parameter is null ? null : LiteralTarget.Of(method.OriginalDefinition, parameter.Name);

    private static LiteralTarget? MemberTarget(SemanticModel model, ExpressionSyntax? expression)
    {
        if (expression is null)
        {
            return null;
        }

        return SymbolOf(model, expression) switch
        {
            IFieldSymbol member => LiteralTarget.Of(member.OriginalDefinition),
            IPropertySymbol { IsIndexer: false } property => LiteralTarget.Of(property.OriginalDefinition),
            _ => null,
        };
    }

    private static ExpressionSyntax? PatternSubject(ConstantPatternSyntax pattern)
    {
        var node = pattern.Parent;
        while (node is PatternSyntax)
        {
            node = node.Parent;
        }

        return node switch
        {
            IsPatternExpressionSyntax isPattern => isPattern.Expression,
            SwitchExpressionArmSyntax arm => (arm.Parent as SwitchExpressionSyntax)?.GoverningExpression,
            CasePatternSwitchLabelSyntax label => (label.Parent?.Parent as SwitchStatementSyntax)?.Expression,
            _ => null,
        };
    }

    private static LiteralTarget? DeclarationTarget(SemanticModel model, EqualsValueClauseSyntax equals) =>
        equals.Parent switch
        {
            ParameterSyntax parameter =>
                model.GetDeclaredSymbol(parameter) is { ContainingSymbol: IMethodSymbol method } symbol
                    ? ParameterTarget(method, symbol)
                    : null,
            PropertyDeclarationSyntax property => DeclaredTarget(model.GetDeclaredSymbol(property)),
            VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax } variable =>
                DeclaredTarget(model.GetDeclaredSymbol(variable)),
            _ => null,
        };

    private static LiteralTarget? DeclaredTarget(ISymbol? declared) =>
        declared is null ? null : LiteralTarget.Of(declared.OriginalDefinition);

    private static bool IsDottedName(string value)
    {
        var segments = value.Split('.');
        return segments.Length > 1 && segments.All(IsIdentifierSegment);
    }

    private static bool IsIdentifierSegment(string segment) =>
        segment.Length > 0 && IsIdentifierStart(segment[0]) && segment.Skip(1).All(IsIdentifierPart);

    private static bool IsIdentifierStart(char ch) => ch is '_' or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');

    private static bool IsIdentifierPart(char ch) =>
        ch is '_' or (>= '0' and <= '9') or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');

    private static bool IsMessage(string value, LiteralExpressionSyntax literal)
    {
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                return true;
            }
        }

        for (var node = literal.Parent; node is not null; node = node.Parent)
        {
            if (node is ThrowStatementSyntax or ThrowExpressionSyntax)
            {
                return true;
            }

            if (node is ObjectCreationExpressionSyntax creation
                && GetTypeName(creation.Type).EndsWith(ExceptionSuffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetTypeName(TypeSyntax type)
    {
        return type switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            QualifiedNameSyntax q => q.Right.Identifier.Text,
            _ => type.ToString(),
        };
    }

    private static bool IsCollectionElement(LiteralExpressionSyntax literal)
    {
        for (var node = literal.Parent; node is not null; node = node.Parent)
        {
            if (node is CollectionExpressionSyntax)
            {
                return true;
            }

            if (node is InitializerExpressionSyntax init
                && (init.IsKind(SyntaxKind.ArrayInitializerExpression)
                    || init.IsKind(SyntaxKind.CollectionInitializerExpression)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsIgnoredContext(LiteralExpressionSyntax literal)
    {
        for (var node = literal.Parent; node is not null; node = node.Parent)
        {
            if (node is AttributeSyntax)
            {
                return true;
            }
        }

        var value = ValueExpression(literal);
        if (value.Parent is not EqualsValueClauseSyntax equals)
        {
            return false;
        }

        if (equals.Parent is EnumMemberDeclarationSyntax)
        {
            return true;
        }

        if (equals.Parent is not VariableDeclaratorSyntax)
        {
            return false;
        }

        var declaration = equals.Parent.Parent?.Parent;
        return declaration switch
        {
            LocalDeclarationStatementSyntax local => local.Modifiers.Any(SyntaxKind.ConstKeyword),
            FieldDeclarationSyntax fieldDeclaration => fieldDeclaration.Modifiers.Any(SyntaxKind.ConstKeyword),
            _ => false,
        };
    }

    private static bool IsAllowedNumber(object? value) =>
        value switch
        {
            decimal m => m is 0 or 1,
            IConvertible number => number.ToDouble(CultureInfo.InvariantCulture) is 0 or 1,
            _ => false,
        };

    // The key is fully qualified so unrelated types never match, and the display is the short form for messages.
    internal readonly record struct LiteralTarget(string Key, string Display)
    {
        private const string ParameterJoin = " of ";

        public static LiteralTarget Of(ISymbol symbol, string? parameter = null)
        {
            var key = symbol.ToDisplayString();
            var display = symbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
            return parameter is null
                ? new LiteralTarget(key, display)
                : new LiteralTarget($"{parameter}{ParameterJoin}{key}", $"{parameter}{ParameterJoin}{display}");
        }
    }
}
