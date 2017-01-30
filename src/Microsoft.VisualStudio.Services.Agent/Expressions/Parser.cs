using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Services.Agent.Expressions
{
    internal sealed class Parser
    {
        private readonly IDictionary<string, object> _extensionObjects;
        private readonly LexicalAnalyzer _lexer;
        private readonly string _raw; // Raw expression string.
        private readonly ITraceWriter _trace;
        private readonly Stack<ContainerInfo> _containers = new Stack<ContainerInfo>();
        private Token _token;
        private Token _lastToken;

        public Parser(string expression, ITraceWriter trace, IDictionary<string, object> extensionObjects)
        {
            ArgUtil.NotNull(trace, nameof(trace));
            ArgUtil.NotNull(extensionObjects, nameof(extensionObjects));
            _raw = expression;
            _trace = trace;
            _extensionObjects = extensionObjects;
            _lexer = new LexicalAnalyzer(expression, trace, _extensionObjects);
            CreateTree();
        }

        public Node Root { get; private set; }

        private void CreateTree()
        {
            _trace.Verbose($"Entering {nameof(CreateTree)}");
            while (TryGetNextToken())
            {
                switch (_token.Kind)
                {
                    // Punctuation
                    case TokenKind.StartIndex:
                        HandleStartIndex();
                        break;
                    case TokenKind.EndIndex:
                        HandleEndIndex();
                        break;
                    case TokenKind.EndParameter:
                        HandleEndParameter();
                        break;
                    case TokenKind.Separator:
                        HandleSeparator();
                        break;
                    case TokenKind.Dereference:
                        HandleDereference();
                        break;

                    // Functions
                    case TokenKind.And:
                    case TokenKind.Equal:
                    case TokenKind.GreaterThan:
                    case TokenKind.GreaterThanOrEqual:
                    case TokenKind.LessThan:
                    case TokenKind.LessThanOrEqual:
                    case TokenKind.Not:
                    case TokenKind.NotEqual:
                    case TokenKind.Or:
                    case TokenKind.Xor:
                        HandleFunction();
                        break;

                    // Leaf values
                    case TokenKind.Boolean:
                    case TokenKind.Number:
                    case TokenKind.Version:
                    case TokenKind.String:
                    case TokenKind.ExtensionObject:
                        HandleValue();
                        break;

                    // Malformed
                    case TokenKind.Unrecognized:
                        throw new ParseException(ParseExceptionKind.UnrecognizedValue, _token, _raw);

                    // Unexpected
                    case TokenKind.PropertyName:    // PropertyName should never reach here.
                    case TokenKind.StartParameter:  // StartParameter is only expected by HandleFunction.
                    default:
                        throw new ParseException(ParseExceptionKind.UnexpectedSymbol, _token, _raw);
                }
            }

            // Validate all containers were closed.
            if (_containers.Count > 0)
            {
                ContainerInfo container = _containers.Peek();
                if (container.Node is FunctionNode)
                {
                    throw new ParseException(ParseExceptionKind.UnclosedFunction, container.Token, _raw);
                }
                else
                {
                    throw new ParseException(ParseExceptionKind.UnclosedIndexer, container.Token, _raw);
                }
            }
        }

        private bool TryGetNextToken()
        {
            _lastToken = _token;
            if (_lexer.TryGetNextToken(ref _token))
            {
                string indent = string.Empty.PadRight(_containers.Count * 2, '.');
                switch (_token.Kind)
                {
                    // Literal values
                    case TokenKind.Boolean:
                    case TokenKind.Number:
                    case TokenKind.Version:
                    case TokenKind.String:
                        _trace.Verbose($"{indent}{_token.Kind} '{_token.ParsedValue}'");
                        break;
                    // Named or unrecognized
                    case TokenKind.ExtensionObject:
                    case TokenKind.PropertyName:
                    case TokenKind.Unrecognized:
                        _trace.Verbose($"{indent}{_token.Kind} '{_raw.Substring(_token.Index, _token.Length)}'");
                        break;
                    // Punctuation
                    case TokenKind.StartIndex:
                    case TokenKind.StartParameter:
                    case TokenKind.EndIndex:
                    case TokenKind.EndParameter:
                    case TokenKind.Separator:
                    case TokenKind.Dereference:
                        _trace.Verbose($"{indent}{_raw.Substring(_token.Index, 1)}");
                        break;
                    // Functions
                    default:
                        _trace.Verbose($"{indent}{_token.Kind}");
                        break;
                }

                return true;
            }

            return false;
        }

        private void HandleStartIndex()
        {
            // Validate follows an extension dictionary object, a property name, or "]".
            bool valid = false;
            if (_lastToken != null)
            {
                switch (_lastToken.Kind)
                {
                    case TokenKind.ExtensionObject:
                        string extensionName = _raw.Substring(_lastToken.Index, _lastToken.Length);
                        valid = _extensionObjects[extensionName] is IDictionary<string, object>;
                        break;
                    case TokenKind.PropertyName:
                    case TokenKind.EndIndex:
                        valid = true;
                        break;
                }
            }

            if (!valid)
            {
                throw new ParseException(ParseExceptionKind.UnexpectedSymbol, _token, _raw);
            }

            // Wrap the object being indexed into.
            var indexer = new IndexerNode(_trace);
            Node obj = null;
            if (_containers.Count > 0)
            {
                ContainerNode container = _containers.Peek().Node;
                int objIndex = container.Parameters.Count;
                obj = container.Parameters[container.Parameters.Count - 1];
                container.ReplaceParameter(objIndex, indexer);
            }
            else
            {
                obj = Root;
                Root = indexer;
            }

            indexer.AddParameter(obj);

            // Update the container stack.
            _containers.Push(new ContainerInfo() { Node = indexer, Token = _token });
        }

        private void HandleDereference()
        {
            // Validate follows an extension dictionary object, a property name, or "]".
            bool valid = false;
            if (_lastToken != null)
            {
                switch (_lastToken.Kind)
                {
                    case TokenKind.ExtensionObject:
                        string extensionName = _raw.Substring(_lastToken.Index, _lastToken.Length);
                        valid = _extensionObjects[extensionName] is IDictionary<string, object>;
                        break;
                    case TokenKind.PropertyName:
                    case TokenKind.EndIndex:
                        valid = true;
                        break;
                }
            }

            if (!valid)
            {
                throw new ParseException(ParseExceptionKind.UnexpectedSymbol, _token, _raw);
            }

            // Wrap the object being indexed into.
            var indexer = new IndexerNode(_trace);
            Node obj = null;
            if (_containers.Count > 0)
            {
                ContainerNode container = _containers.Peek().Node;
                int objIndex = container.Parameters.Count;
                obj = container.Parameters[container.Parameters.Count - 1];
                container.ReplaceParameter(objIndex, indexer);
            }
            else
            {
                obj = Root;
                Root = indexer;
            }

            indexer.AddParameter(obj);

            // Validate a property name follows.
            if (!TryGetNextToken())
            {
                throw new ParseException(ParseExceptionKind.ExpectedPropertyName, _lastToken, _raw);
            }

            if (_token.Kind != TokenKind.PropertyName)
            {
                throw new ParseException(ParseExceptionKind.UnexpectedSymbol, _token, _raw);
            }

            // Add the property name to the indexer, as a string.
            string propertyName = _raw.Substring(_token.Index, _token.Length);
            indexer.AddParameter(new LeafNode(val: propertyName, extensionName: string.Empty, trace: _trace));
        }

        private void HandleEndParameter()
        {
            ContainerInfo container = _containers.Count > 0 ? _containers.Peek() : null;    // Validate:
            if (container == null ||                                                        // 1) Container is not null
                !(container.Node is FunctionNode) ||                                        // 2) Container is a function
                container.Node.Parameters.Count < GetMinParamCount(container.Token.Kind) || // 3) Not below min param threshold
                _lastToken.Kind == TokenKind.Separator)                                     // 4) Last token is not a separator
            {
                throw new ParseException(ParseExceptionKind.UnexpectedSymbol, _token, _raw);
            }

            _containers.Pop();
        }

        private void HandleEndIndex()
        {
            IndexerNode indexer = _containers.Count > 0 ? _containers.Peek().Node as IndexerNode : null;
            //                                  // Validate:
            if (indexer == null ||              // 1) Container is an indexer
                indexer.Parameters.Count != 2)  // 2) Exactly 2 parameters
            {
                throw new ParseException(ParseExceptionKind.UnexpectedSymbol, _token, _raw);
            }

            _containers.Pop();
        }

        private void HandleValue()
        {
            // Validate either A) is the first token OR B) follows "[" "(" or ",".
            if (_lastToken != null &&
                _lastToken.Kind != TokenKind.StartIndex &&
                _lastToken.Kind != TokenKind.StartParameter &&
                _lastToken.Kind != TokenKind.Separator)
            {
                throw new ParseException(ParseExceptionKind.UnexpectedSymbol, _token, _raw);
            }

            // Create the node.
            object val;
            string extensionName;
            if (_token.Kind == TokenKind.ExtensionObject)
            {
                extensionName = _raw.Substring(_token.Index, _token.Length);
                val = _extensionObjects[extensionName];
            }
            else
            {
                extensionName = string.Empty;
                val = _token.ParsedValue;
            }

            var node = new LeafNode(val: val, extensionName: extensionName, trace: _trace);

            // Update the tree.
            if (Root == null)
            {
                Root = node;
            }
            else
            {
                _containers.Peek().Node.AddParameter(node);
            }
        }

        private void HandleSeparator()
        {
            ContainerInfo container = _containers.Count > 0 ? _containers.Peek() : null;        // Validate:
            if (container == null ||                                                            // 1) Container is not null
                !(container.Node is FunctionNode) ||                                            // 2) Container is a function
                container.Node.Parameters.Count < 1 ||                                          // 3) At least one parameter
                container.Node.Parameters.Count >= GetMaxParamCount(container.Token.Kind) ||    // 4) Under max parameters threshold
                _lastToken.Kind == TokenKind.Separator)                                         // 5) Last token is not a separator
            {
                throw new ParseException(ParseExceptionKind.UnexpectedSymbol, _token, _raw);
            }
        }

        private void HandleFunction()
        {
            // Validate either A) is first token OR B) follows "," or "[" or "(".
            if (_lastToken != null &&
                (_lastToken.Kind != TokenKind.Separator &&
                _lastToken.Kind != TokenKind.StartIndex &&
                _lastToken.Kind != TokenKind.StartParameter))
            {
                throw new ParseException(ParseExceptionKind.UnexpectedSymbol, _token, _raw);
            }

            // Create the node.
            FunctionNode node;
            switch (_token.Kind)
            {
                case TokenKind.And:
                    node = new AndNode(_trace);
                    break;
                case TokenKind.Equal:
                    node = new EqualNode(_trace);
                    break;
                case TokenKind.GreaterThan:
                    node = new GreaterThanNode(_trace);
                    break;
                case TokenKind.GreaterThanOrEqual:
                    node = new GreaterThanOrEqualNode(_trace);
                    break;
                case TokenKind.LessThan:
                    node = new LessThanNode(_trace);
                    break;
                case TokenKind.LessThanOrEqual:
                    node = new LessThanOrEqualNode(_trace);
                    break;
                case TokenKind.Not:
                    node = new NotNode(_trace);
                    break;
                case TokenKind.NotEqual:
                    node = new NotEqualNode(_trace);
                    break;
                case TokenKind.Or:
                    node = new OrNode(_trace);
                    break;
                case TokenKind.Xor:
                    node = new XorNode(_trace);
                    break;
                default:
                    // Should never reach here.
                    throw new NotSupportedException($"Unexpected function token name: '{_token.Kind}'");
            }

            // Update the tree.
            if (Root == null)
            {
                Root = node;
            }
            else
            {
                _containers.Peek().Node.AddParameter(node);
            }

            // Update the container stack.
            _containers.Push(new ContainerInfo() { Node = node, Token = _token });

            // Validate '(' follows.
            if (!TryGetNextToken() || _token.Kind != TokenKind.StartParameter)
            {
                throw new ParseException(ParseExceptionKind.ExpectedStartParameter, _lastToken, _raw);
            }
        }

        private static int GetMinParamCount(TokenKind kind)
        {
            switch (kind)
            {
                case TokenKind.Not:
                    return 1;
                case TokenKind.And:
                case TokenKind.Equal:
                case TokenKind.GreaterThan:
                case TokenKind.GreaterThanOrEqual:
                case TokenKind.LessThan:
                case TokenKind.LessThanOrEqual:
                case TokenKind.NotEqual:
                case TokenKind.Or:
                case TokenKind.Xor:
                    return 2;
                default: // Should never reach here.
                    throw new NotSupportedException($"Unexpected token kind '{kind}'. Unable to determine min param count.");
            }
        }

        private static int GetMaxParamCount(TokenKind kind)
        {
            switch (kind)
            {
                case TokenKind.Not:
                    return 1;
                case TokenKind.Equal:
                case TokenKind.GreaterThan:
                case TokenKind.GreaterThanOrEqual:
                case TokenKind.LessThan:
                case TokenKind.LessThanOrEqual:
                case TokenKind.NotEqual:
                case TokenKind.Xor:
                    return 2;
                case TokenKind.And:
                case TokenKind.Or:
                    return int.MaxValue;
                default: // Should never reach here.
                    throw new NotSupportedException($"Unexpected token kind '{kind}'. Unable to determine max param count.");
            }
        }

        private sealed class ContainerInfo
        {
            public ContainerNode Node { get; set; }

            public Token Token { get; set; }
        }
    }

    // todo: make internal
    public sealed class ParseException : Exception
    {
        private readonly string _message;

        public ParseException(ParseExceptionKind kind, Token token, string condition)
        {
            Condition = condition;
            Kind = kind;
            TokenIndex = token.Index;
            TokenLength = token.Length;
            string description;
            // TODO: LOC
            switch (kind)
            {
                case ParseExceptionKind.ExpectedPropertyName:
                    description = "Expected property name to follow deference operator";
                    break;
                case ParseExceptionKind.ExpectedStartParameter:
                    description = "Expected '(' to follow function";
                    break;
                case ParseExceptionKind.UnclosedFunction:
                    description = "Unclosed function";
                    break;
                case ParseExceptionKind.UnclosedIndexer:
                    description = "Unclosed indexer";
                    break;
                case ParseExceptionKind.UnexpectedSymbol:
                    description = "Unexpected symbol";
                    break;
                case ParseExceptionKind.UnrecognizedValue:
                    description = "Unrecognized value";
                    break;
                default: // Should never reach here.
                    throw new Exception($"Unexpected parse exception kind '{kind}'.");
            }

            RawToken = condition.Substring(token.Index, token.Length);
            int position = token.Index + 1;
            // TODO: loc
            _message = $"{description}: '{RawToken}'. Located at position {position} within condition expression: {Condition}";
        }

        public string Condition { get; private set; }

        public ParseExceptionKind Kind { get; private set; }

        public string RawToken { get; private set; }

        public int TokenIndex { get; private set; }

        public int TokenLength { get; private set; }

        public sealed override string Message => _message;
    }

    // todo: make internal
    public enum ParseExceptionKind
    {
        ExpectedPropertyName,
        ExpectedStartParameter,
        UnclosedFunction,
        UnclosedIndexer,
        UnexpectedSymbol,
        UnrecognizedValue,
    }
}