using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using CSharp = Microsoft.CodeAnalysis.CSharp;
using VB = Microsoft.CodeAnalysis.VisualBasic;

namespace Colsemroslyn
{

    [Export(typeof(ITaggerProvider))]
    [ContentType("CSharp")]
    [ContentType("Basic")]
    [TagType(typeof(IClassificationTag))]
    internal class SemanticColorizerProvider : ITaggerProvider
    {
#pragma warning disable CS0649
        [Import]
        internal IClassificationTypeRegistryService ClassificationRegistry; // Set via MEF
#pragma warning restore CS0649

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return (ITagger<T>)new SemanticColorizer(buffer, ClassificationRegistry);
        }
    }

    class SemanticColorizer : ITagger<IClassificationTag>
    {
        private static readonly HashSet<string> SupportedClassificationTypeNames;
        private readonly ITextBuffer _theBuffer;
        private readonly IClassificationType _fieldType;
        private readonly IClassificationType _enumFieldType;
        private readonly IClassificationType _extensionMethodType;
        private readonly IClassificationType _staticMethodType;
        private readonly IClassificationType _normalMethodType;
        private readonly IClassificationType _localFunctionType;
        private readonly IClassificationType _constructorType;
        private readonly IClassificationType _parameterType;
        private readonly IClassificationType _namespaceType;
        private readonly IClassificationType _propertyType;
        private readonly IClassificationType _localType;
        private readonly IClassificationType _typeSpecialType;
        private readonly IClassificationType _eventType;
        private readonly IClassificationType _builtInMethodType;
        private readonly IClassificationType _declarationMethodType;
        private readonly IClassificationType _callMethodType;

        // Built in VS by default
        private readonly IClassificationType _builtInClassType;
        private readonly IClassificationType _builtInStructType;
        
        private Cache _cache;

        // Reusable caches to reduce allocations and repeated work
        private TextSpan _lastClassificationSpan;
        private List<ClassifiedSpan> _lastClassifications;
        private readonly Dictionary<TextSpan, SyntaxNode> _nodeCache = new Dictionary<TextSpan, SyntaxNode>();
        private readonly Dictionary<TextSpan, ISymbol> _symbolCache = new Dictionary<TextSpan, ISymbol>();

#pragma warning disable CS0067
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
#pragma warning restore CS0067

        static class NewClassificationTypeNames
        {
            public const string PropertyName = "property name";
            public const string EventName = "event name";
            public const string ExtensionMethodName = "extension method name";
            public const string MethodName = "method name";
            public const string ParameterName = "parameter name";
            public const string LocalName = "local name";
            public const string FieldName = "field name";
            public const string EnumMemberName = "enum member name";
            public const string ConstantName = "constant name";
        }
        public const MethodKind LocalMethodKind = (MethodKind)17;

        static SemanticColorizer()
        {
            SupportedClassificationTypeNames = new HashSet<string>
            {
                NewClassificationTypeNames.FieldName,
                NewClassificationTypeNames.PropertyName,
                NewClassificationTypeNames.EnumMemberName,
                ClassificationTypeNames.Identifier,
                ClassificationTypeNames.Keyword,
                NewClassificationTypeNames.EventName,
                NewClassificationTypeNames.LocalName,
                NewClassificationTypeNames.ParameterName,
                NewClassificationTypeNames.ExtensionMethodName,
                NewClassificationTypeNames.ConstantName,
                NewClassificationTypeNames.MethodName
            };
        }

        internal SemanticColorizer(ITextBuffer buffer, IClassificationTypeRegistryService registry)
        {
            _theBuffer = buffer;
            _fieldType = registry.GetClassificationType(Constants.FieldFormat);
            _enumFieldType = registry.GetClassificationType(Constants.EnumFieldFormat);
            _extensionMethodType = registry.GetClassificationType(Constants.ExtensionMethodFormat);
            _staticMethodType = registry.GetClassificationType(Constants.StaticMethodFormat);
            _normalMethodType = registry.GetClassificationType(Constants.NormalMethodFormat);
            _localFunctionType = registry.GetClassificationType(Constants.LocalFunctionFormat);
            _constructorType = registry.GetClassificationType(Constants.ConstructorFormat);
            _parameterType = registry.GetClassificationType(Constants.ParameterFormat);
            _namespaceType = registry.GetClassificationType(Constants.NamespaceFormat);
            _propertyType = registry.GetClassificationType(Constants.PropertyFormat);
            _localType = registry.GetClassificationType(Constants.LocalFormat);
            _typeSpecialType = registry.GetClassificationType(Constants.TypeSpecialFormat);
            _eventType = registry.GetClassificationType(Constants.EventFormat);
            _builtInMethodType = registry.GetClassificationType(Constants.BuiltInMethodFormat);
            _declarationMethodType = registry.GetClassificationType(Constants.DeclarationMethodFormat);
            _callMethodType = registry.GetClassificationType(Constants.CallMethodFormat);

            // Built in VS by default
            _builtInClassType = registry.GetClassificationType(Constants.BuiltInClassTypeFormat);
            _builtInStructType = registry.GetClassificationType(Constants.BuildInStructTypeFormat);

            _lastClassificationSpan = default(TextSpan);
            _lastClassifications = null;
        }

        public IEnumerable<ITagSpan<IClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
            {
                return Enumerable.Empty<ITagSpan<IClassificationTag>>();
            }
            if (_cache == null || _cache.Snapshot != spans[0].Snapshot)
            {
                Cache cache = null;
                try
                {
                    cache = Cache.Resolve(_theBuffer, spans[0].Snapshot);
                }
                catch (Exception)
                {
                    return Enumerable.Empty<ITagSpan<IClassificationTag>>();
                }
                _cache = cache;
                if (_cache == null)
                {
                    return Enumerable.Empty<ITagSpan<IClassificationTag>>();
                }

                // snapshot changed, clear reusable caches to avoid holding references
                _lastClassifications = null;
                _lastClassificationSpan = default(TextSpan);
                _nodeCache.Clear();
                _symbolCache.Clear();
            }
            return GetTagsImpl(_cache, spans);
        }

        private IEnumerable<ITagSpan<IClassificationTag>> GetTagsImpl(
              Cache doc,
              NormalizedSnapshotSpanCollection spans)
        {
            var snapshot = spans[0].Snapshot;

            var classifiedSpans = GetClassifiedSpans(doc.Document, doc.SemanticModel, spans);

            // local references to caches to avoid repeated field access
            var nodeCache = _nodeCache;
            var symbolCache = _symbolCache;
            var syntaxRoot = doc.SyntaxRoot;
            var model = doc.SemanticModel;

            foreach (var span in classifiedSpans)
            {
                SyntaxNode node;
                if (!nodeCache.TryGetValue(span.TextSpan, out node))
                {
                    node = GetNodeForSpan(syntaxRoot, span.TextSpan);
                    nodeCache[span.TextSpan] = node;
                }

                ISymbol symbol;
                if (!symbolCache.TryGetValue(span.TextSpan, out symbol))
                {
                    symbol = model.GetSymbolInfo(node).Symbol;
                    if (symbol == null) symbol = model.GetDeclaredSymbol(node);
                    // store even if null to avoid repeated lookups
                    symbolCache[span.TextSpan] = symbol;
                }

                if (symbol == null)
                {
                    continue;
                }

                switch (symbol.Kind)
                {
                    case SymbolKind.Field:
                        switch (span.ClassificationType)
                        {
                            case NewClassificationTypeNames.ConstantName:
                            case NewClassificationTypeNames.FieldName:
                                yield return span.TextSpan.ToTagSpan(snapshot, _fieldType);
                                break;
                            case NewClassificationTypeNames.EnumMemberName:
                                yield return span.TextSpan.ToTagSpan(snapshot, _enumFieldType);
                                break;
                        }
                        break;
                    case SymbolKind.Method:
                        var methodSymbol = (IMethodSymbol)symbol;
                        switch (span.ClassificationType)
                        {
                            case ClassificationTypeNames.Identifier:
                                if (IsConstructor(methodSymbol))
                                {
                                    yield return span.TextSpan.ToTagSpan(snapshot, _constructorType);
                                }
                                //local function definition
                                else if (methodSymbol.MethodKind == LocalMethodKind)
                                {
                                    yield return span.TextSpan.ToTagSpan(snapshot, _localFunctionType);
                                }
                                break;
                            case NewClassificationTypeNames.MethodName:
                            case NewClassificationTypeNames.ExtensionMethodName:
                                //declaration method
                                if (IsDeclarationMethod(node))
                                {
                                    yield return span.TextSpan.ToTagSpan(snapshot, _declarationMethodType);
                                }
                                //built-in method call
                                else if (IsBuiltInMethod(methodSymbol))
                                {
                                    yield return span.TextSpan.ToTagSpan(snapshot, _builtInMethodType);
                                }
                                //method call
                                else if (IsCallMethod(node))
                                {
                                    yield return span.TextSpan.ToTagSpan(snapshot, _callMethodType);
                                }
                                //local function call
                                else if (methodSymbol.MethodKind == LocalMethodKind)
                                {
                                    yield return span.TextSpan.ToTagSpan(snapshot, _callMethodType);
                                }
                                //static method call
                                else if (methodSymbol.MethodKind == MethodKind.Ordinary && methodSymbol.IsStatic)
                                {
                                    yield return span.TextSpan.ToTagSpan(snapshot, _callMethodType);
                                }
                                //other method call
                                else
                                {
                                    yield return span.TextSpan.ToTagSpan(snapshot, _callMethodType);
                                }
                                break;
                        }
                        break;
                    case SymbolKind.Parameter:
                        yield return span.TextSpan.ToTagSpan(snapshot, _parameterType);
                        break;
                    case SymbolKind.Namespace:
                        yield return span.TextSpan.ToTagSpan(snapshot, _namespaceType);
                        break;
                    case SymbolKind.Property:
                        yield return span.TextSpan.ToTagSpan(snapshot, _propertyType);
                        break;
                    case SymbolKind.Local:
                        yield return span.TextSpan.ToTagSpan(snapshot, _localType);
                        break;
                    case SymbolKind.Event:
                        yield return span.TextSpan.ToTagSpan(snapshot, _eventType);
                        break;
                    case SymbolKind.NamedType:
                        switch (span.ClassificationType)
                        {
                            case ClassificationTypeNames.Keyword:
                                if (node.IsCSharpPredefinedTypeSyntax())
                                {
                                    var type = (INamedTypeSymbol)symbol;
                                    if (type.SpecialType == SpecialType.System_Void)
                                        continue;
                                    if (type.TypeKind == TypeKind.Struct)
                                    {
                                        yield return span.TextSpan.ToTagSpan(snapshot, _builtInStructType);
                                    }
                                    if (type.TypeKind == TypeKind.Class)
                                    {
                                        yield return span.TextSpan.ToTagSpan(snapshot, _builtInClassType);
                                    }
                                }
                                break;
                            default:
                                if (IsSpecialType(symbol))
                                {
                                    yield return span.TextSpan.ToTagSpan(snapshot, _typeSpecialType);
                                }
                                break;

                        }
                        break;
                }
            }
        }

        private SyntaxNode GetNodeForSpan(SyntaxNode syntaxRoot, TextSpan textSpan)
        {
            if (syntaxRoot == null) return null;
            // find token at start; faster than FindNode for many cases
            var token = syntaxRoot.FindToken(textSpan.Start);
            var node = token.Parent;
            while (node != null)
            {
                var span = node.Span;
                if (span.Start <= textSpan.Start && span.End >= textSpan.End)
                    break;
                node = node.Parent;
            }
            if (node == null)
            {
                node = syntaxRoot;
            }
            return GetExpression(node);
        }

        private static bool IsBuiltInMethod(IMethodSymbol symbol)
        {
            if (symbol == null)
                return false;

            var assemblyName = symbol.ContainingAssembly?.Name ?? string.Empty;
            var ns = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

            // Caso speciale: extension methods LINQ (First, Where, Select, ecc.)
            if (symbol.IsExtensionMethod && symbol.ContainingType?.ToDisplayString() == "System.Linq.Enumerable")
                return true;

            if (assemblyName == "mscorlib" ||
                assemblyName == "System.Runtime" ||
                assemblyName == "System.Private.CoreLib" ||
                assemblyName == "System.Core")
            {
                return true;
            }

            if (ns.StartsWith("System", StringComparison.Ordinal))
                return true;

            if (ns.StartsWith("Microsoft", StringComparison.Ordinal))
                return true;

            return false;
        }

        private bool IsDeclarationMethod(SyntaxNode node)
        {
            if (node.Language == LanguageNames.CSharp)
                return node is Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax;

            if (node.Language == LanguageNames.VisualBasic)
                return node is Microsoft.CodeAnalysis.VisualBasic.Syntax.MethodStatementSyntax ||
                       node is Microsoft.CodeAnalysis.VisualBasic.Syntax.MethodBlockSyntax;

            return false;
        }

        private bool IsCallMethod(SyntaxNode node)
        {
            if (node.Language == LanguageNames.CSharp)
                return node is CSharp.Syntax.InvocationExpressionSyntax;

            if (node.Language == LanguageNames.VisualBasic)
            {
                if (node is VB.Syntax.IdentifierNameSyntax)
                    return true;
                if (node.Parent is VB.Syntax.InvocationExpressionSyntax)
                    return true;
            }

            return false;
        }

        private bool IsSpecialType(ISymbol symbol)
        {
            var type = (INamedTypeSymbol)symbol;
            return type.SpecialType != SpecialType.None;
        }

        private SyntaxNode GetExpression(SyntaxNode node)
        {
            if (node == null) return null;
            if (node.CSharpKind() == CSharp.SyntaxKind.Argument)
            {
                return ((CSharp.Syntax.ArgumentSyntax)node).Expression;
            }
            else if (node.CSharpKind() == CSharp.SyntaxKind.AttributeArgument)
            {
                return ((CSharp.Syntax.AttributeArgumentSyntax)node).Expression;
            }
            else if (node.VbKind() == VB.SyntaxKind.SimpleArgument)
            {
                return ((VB.Syntax.SimpleArgumentSyntax)node).Expression;
            }
            return node;
        }

        private bool IsConstructor(IMethodSymbol methodSymbol)
        {
            return methodSymbol.MethodKind == MethodKind.Constructor ||
                   methodSymbol.MethodKind == MethodKind.StaticConstructor ||
                   methodSymbol.MethodKind == MethodKind.SharedConstructor;
        }

        private IEnumerable<ClassifiedSpan> GetClassifiedSpans(
              Document document, SemanticModel model,
              NormalizedSnapshotSpanCollection spans)
        {
            var comparer = StringComparer.InvariantCultureIgnoreCase;

            if (spans.Count == 0)
            {
                return Enumerable.Empty<ClassifiedSpan>();
            }

            var min = spans.Min(s => s.Start);
            var max = spans.Max(s => s.End);
            var aggregateTextSpan = TextSpan.FromBounds(min, max);

            // reuse last fetched classifications for the same aggregate span
            if (_lastClassifications != null && _lastClassificationSpan == aggregateTextSpan)
            {
                // filter results to requested spans
                var list = new List<ClassifiedSpan>();
                var classifications = _lastClassifications;
                for (int i = 0; i < classifications.Count; i++)
                {
                    var c = classifications[i];
                    if (!SupportedClassificationTypeNames.Contains(c.ClassificationType, comparer)) continue;
                    for (int j = 0; j < spans.Count; j++)
                    {
                        var s = spans[j];
                        if (c.TextSpan.Start < s.End && c.TextSpan.End > s.Start)
                        {
                            list.Add(c);
                            break;
                        }
                    }
                }
                return list;
            }

            List<ClassifiedSpan> classificationsResult = null;
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                var list = await Classifier.GetClassifiedSpansAsync(document, aggregateTextSpan).ConfigureAwait(false);
                // copy to list to decouple from underlying collection
                classificationsResult = (list ?? Enumerable.Empty<ClassifiedSpan>()).ToList();
            });

            if (classificationsResult == null || classificationsResult.Count == 0)
            {
                _lastClassifications = classificationsResult;
                _lastClassificationSpan = aggregateTextSpan;
                return Enumerable.Empty<ClassifiedSpan>();
            }

            // cache the fetched classifications for reuse
            _lastClassifications = classificationsResult;
            _lastClassificationSpan = aggregateTextSpan;

            var filteredList = new List<ClassifiedSpan>();
            for (int i = 0; i < classificationsResult.Count; i++)
            {
                var c = classificationsResult[i];
                if (!SupportedClassificationTypeNames.Contains(c.ClassificationType, comparer)) continue;
                for (int j = 0; j < spans.Count; j++)
                {
                    var s = spans[j];
                    if (c.TextSpan.Start < s.End && c.TextSpan.End > s.Start)
                    {
                        filteredList.Add(c);
                        break;
                    }
                }
            }

            return filteredList;
        }

        private class Cache
        {
            public Workspace Workspace { get; private set; }
            public Document Document { get; private set; }
            public SemanticModel SemanticModel { get; private set; }
            public SyntaxNode SyntaxRoot { get; private set; }
            public ITextSnapshot Snapshot { get; private set; }

            private Cache() { }

            public static Cache Resolve(ITextBuffer buffer, ITextSnapshot snapshot)
            {
                var workspace = buffer.GetWorkspace();
                var document = snapshot.GetOpenDocumentInCurrentContextWithChanges();
                if (document == null)
                {
                    // Razor cshtml returns a null document for some reason.
                    return null;
                }

                // the ConfigureAwait() calls are important,
                // otherwise we'll deadlock VS
                SemanticModel semanticModel = null;
                SyntaxNode syntaxRoot = null;
                ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    semanticModel = await document.GetSemanticModelAsync().ConfigureAwait(false);
                    syntaxRoot = await document.GetSyntaxRootAsync().ConfigureAwait(false);
                });
                return new Cache
                {
                    Workspace = workspace,
                    Document = document,
                    SemanticModel = semanticModel,
                    SyntaxRoot = syntaxRoot,
                    Snapshot = snapshot
                };
            }
        }
    }
}
