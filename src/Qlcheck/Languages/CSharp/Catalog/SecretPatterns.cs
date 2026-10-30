// Copyright (c) qlcheck contributors.
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qlcheck.Languages.CSharp.Catalog.Queries;
using Qlcheck.Secrets;

namespace Qlcheck.Languages.CSharp.Catalog;

// The C# side of secret scanning: collects every string constant in a file, works out its name
// and slot, and lets the shared classifier decide. Each value gets at most one kind, and each of
// the four Checks reports only its own. The Finding points at the constant and never repeats it.
internal static class SecretPatterns
{
    private const string SymmetricSecurityKeyType = "SymmetricSecurityKey";

    private const int MaxKeyDepth = 3;

    private static readonly HashSet<string> KeyByteMethods = new(StringComparer.Ordinal)
    {
        "GetBytes", "FromBase64String",
    };

    private static readonly ConditionalWeakTable<WalkContext, List<Hit>> Cache = new();

    public static void Credential(WalkContext ctx, string id) => Report(ctx, id, SecretKind.Password);

    public static void DatabasePassword(WalkContext ctx, string id) => Report(ctx, id, SecretKind.ConnectionPassword);

    public static void Secret(WalkContext ctx, string id) => Report(ctx, id, SecretKind.Format, SecretKind.NamedSecret);

    public static void JwtSecret(WalkContext ctx, string id) => Report(ctx, id, SecretKind.JwtKey);

    // Every string constant in the file, outermost expression only: a folded `"a" + "b"` is one
    // value. A non-constant interpolated string contributes its text with each hole as `{}`, which
    // reads as a placeholder, and has no name, so only the name-free kinds can match it.
    internal static IEnumerable<Constant> Constants(WalkContext ctx)
    {
        var seen = new HashSet<SyntaxNode>();
        foreach (var node in ctx.Tree.GetRoot().DescendantNodes())
        {
            if (node is not (LiteralExpressionSyntax or InterpolatedStringExpressionSyntax) || IsInsideInterpolation(node))
            {
                continue;
            }

            var expression = (ExpressionSyntax)node;
            if (node is LiteralExpressionSyntax literal && !IsString(literal))
            {
                continue;
            }

            var top = Outermost(ctx.Model, expression);
            if (!seen.Add(top))
            {
                continue;
            }

            if (ValueOf(ctx.Model, top) is { } value)
            {
                yield return new Constant(top, value, Named: true);
                continue;
            }

            if (top is InterpolatedStringExpressionSyntax interpolated)
            {
                yield return new Constant(top, TextWithHoles(interpolated), Named: false);
            }
        }
    }

    private static void Report(WalkContext ctx, string id, params SecretKind[] kinds)
    {
        foreach (var hit in Cache.GetValue(ctx, Classify))
        {
            if (!kinds.Contains(hit.Kind))
            {
                continue;
            }

            if (hit.Detail.Length == 0)
            {
                ctx.Report(id, hit.Node);
            }
            else
            {
                ctx.Report(id, hit.Node, hit.Detail);
            }
        }
    }

    private static List<Hit> Classify(WalkContext ctx)
    {
        var signing = SigningKeyValues(ctx);
        var hits = new List<Hit>();
        var classified = new HashSet<SyntaxNode>();
        foreach (var constant in Constants(ctx))
        {
            classified.Add(constant.Node);
            var (name, keyPath) = constant.Named ? SecretSlots.NameOf(ctx.Model, constant.Node) : (null, null);
            var context = new SecretContext(
                ctx.IsTestFile,
                constant.Named && SecretSlots.IsConnectionStringSlot(ctx.Model, constant.Node),
                signing.Contains(constant.Node));
            AddHit(hits, constant.Node, SecretRules.Classify(name, keyPath, constant.Value, context));
        }

        // A key read from a constant declared in another file, or given as constant bytes.
        var slot = new SecretContext(ctx.IsTestFile, IsConnectionStringSlot: false, IsSigningKeySlot: true);
        foreach (var node in signing.OfType<ExpressionSyntax>().Where(node => !classified.Contains(node)))
        {
            var hit = ValueOf(ctx.Model, node) is { } value
                ? SecretRules.Classify(null, null, value, slot)
                : new SecretHit(SecretKind.JwtKey, string.Empty);
            AddHit(hits, node, hit);
        }

        return hits;
    }

    private static void AddHit(List<Hit> hits, SyntaxNode node, SecretHit? hit)
    {
        if (hit is not null)
        {
            hits.Add(new Hit(node, hit.Kind, hit.Detail));
        }
    }

    // The values that become JWT signing key bytes: the argument of `new SymmetricSecurityKey(...)`,
    // followed through `Encoding.*.GetBytes`/`Convert.FromBase64String` and fields or locals
    // initialised in this file, down to a string constant or a constant byte array.
    private static HashSet<SyntaxNode> SigningKeyValues(WalkContext ctx)
    {
        var values = new HashSet<SyntaxNode>();
        foreach (var creation in ctx.Tree.GetRoot().DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
        {
            if (creation.ArgumentList?.Arguments.Count > 0
                && Symbols.TypeOf(ctx, creation)?.Name == SymmetricSecurityKeyType)
            {
                AddKeyValues(ctx, creation.ArgumentList.Arguments[0].Expression, values, MaxKeyDepth);
            }
        }

        return values;
    }

    private static void AddKeyValues(WalkContext ctx, ExpressionSyntax expression, HashSet<SyntaxNode> values, int depth)
    {
        if (depth == 0)
        {
            return;
        }

        // A field or local declared here is followed to its initializer first, so the Finding
        // lands on the literal rather than on the reference.
        if (expression is IdentifierNameSyntax or MemberAccessExpressionSyntax && InitializerOf(ctx, expression) is { } initializer)
        {
            AddKeyValues(ctx, initializer, values, depth - 1);
            return;
        }

        if (ValueOf(ctx.Model, expression) is not null)
        {
            values.Add(Outermost(ctx.Model, expression));
            return;
        }

        if (expression is InvocationExpressionSyntax invocation
            && KeyByteMethods.Contains(Names.Invocation(invocation))
            && invocation.ArgumentList.Arguments.Count > 0)
        {
            AddKeyValues(ctx, invocation.ArgumentList.Arguments[0].Expression, values, depth - 1);
            return;
        }

        if (IsConstantByteArray(expression))
        {
            values.Add(expression);
        }
    }

    private static bool IsConstantByteArray(ExpressionSyntax expression) =>
        expression switch
        {
            ArrayCreationExpressionSyntax { Initializer: { } init } => IsConstantBytes(init.Expressions),
            ImplicitArrayCreationExpressionSyntax implicitArray => IsConstantBytes(implicitArray.Initializer.Expressions),
            CollectionExpressionSyntax collection => collection.Elements.All(element => element is ExpressionElementSyntax)
                && IsConstantBytes(collection.Elements.OfType<ExpressionElementSyntax>().Select(element => element.Expression).ToList()),
            _ => false,
        };

    // The initializer of a field or local declared in this file.
    private static ExpressionSyntax? InitializerOf(WalkContext ctx, ExpressionSyntax reference)
    {
        var symbol = Symbols.SymbolOf(ctx, reference);
        if (symbol is not (IFieldSymbol or ILocalSymbol))
        {
            return null;
        }

        return symbol.DeclaringSyntaxReferences
            .Where(syntax => syntax.SyntaxTree == ctx.Tree)
            .Select(syntax => syntax.GetSyntax())
            .OfType<VariableDeclaratorSyntax>()
            .Select(declarator => declarator.Initializer?.Value)
            .FirstOrDefault(value => value is not null);
    }

    private static bool IsConstantBytes(IReadOnlyCollection<ExpressionSyntax> elements) =>
        elements.Count > 0 && elements.All(element => element is LiteralExpressionSyntax);

    private static ExpressionSyntax Outermost(SemanticModel model, ExpressionSyntax expression)
    {
        var top = expression;
        while (top.Parent is ParenthesizedExpressionSyntax
            || (top.Parent is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression) && ValueOf(model, binary) is not null))
        {
            top = (ExpressionSyntax)top.Parent;
        }

        return top;
    }

    private static string? ValueOf(SemanticModel model, ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.Utf8StringLiteralExpression)
            ? literal.Token.ValueText
            : SecretSlots.ConstantString(model, expression);

    private static string TextWithHoles(InterpolatedStringExpressionSyntax interpolated) =>
        string.Concat(interpolated.Contents.Select(content => content is InterpolatedStringTextSyntax text ? text.TextToken.ValueText : "{}"));

    private static bool IsString(LiteralExpressionSyntax literal) =>
        literal.IsKind(SyntaxKind.StringLiteralExpression) || literal.IsKind(SyntaxKind.Utf8StringLiteralExpression);

    private static bool IsInsideInterpolation(SyntaxNode node) => node.Ancestors().Any(ancestor => ancestor is InterpolationSyntax);

    internal sealed record Constant(ExpressionSyntax Node, string Value, bool Named);

    private sealed record Hit(SyntaxNode Node, SecretKind Kind, string Detail);
}
