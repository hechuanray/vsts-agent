using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Services.Agent.Expressions
{
    internal sealed class Parser
    {
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
            _lexer = new LexicalAnalyzer(expression, trace, extensionObjects);
            CreateTree();
        }

        public Node Root { get; private set; }

        private bool TryGetNextToken()
        {
            _lastToken = _token;
            if (_lexer.TryGetNextToken(ref _token))
            {
                string indent = string.Empty.PadRight(_containers.Count * 2, '.');
                switch (_token.Kind)
                {
                    case TokenKind.Boolean:
                    case TokenKind.Number:
                    case TokenKind.Version:
                    case TokenKind.String:
                    case TokenKind.Object:
                        _trace.Verbose($"{indent}{_token.Kind} '{_token.ParsedValue}'");
                        break;
                    case TokenKind.Unrecognized:
                        _trace.Verbose($"{indent}{_token.Kind} '{_raw.Substring(_token.Index, _token.Length)}'");
                        break;
                    case TokenKind.StartIndex:
                    case TokenKind.StartParameter:
                    case TokenKind.EndIndex:
                    case TokenKind.EndParameter:
                    case TokenKind.Separator:
                    case TokenKind.Dereference:
                        _trace.Verbose($"{indent}{_raw.Substring(_token.Index, 1)}");
                        break;
                    default:
                        _trace.Verbose($"{indent}{_token.Kind}");
                        break;
                }

                return true;
            }

            return false;
        }

        private void CreateTree()
        {
            _trace.Verbose($"Entering {nameof(CreateTree)}");
            while (TryGetNextToken())
            {
                switch (_token.Kind)
                {
                    case TokenKind.Unrecognized:
                        throw new ParseException(ParseExceptionKind.UnrecognizedValue, _token, _raw);

                    // Punctuation
                    case TokenKind.StartIndex:
                        HandleStartIndex();
                        break;
                    case TokenKind.StartParameter:
                        HandleStartParameter();
                        // throw new ParseException(ParseExceptionKind.UnexpectedSymbol, token, _raw);
                        // ValidateStartParameter();
                        break;
                    case TokenKind.EndIndex:
                        HandleEndIndex();
                        // ValidateEndIndex();
                        // containers.Pop();
                        break;
                    case TokenKind.EndParameter:
                        HandleEndParameter();
                        // ValidateEndParameter();
                        // containers.Pop();
                        break;
                    case TokenKind.Separator:
                        HandleSeparator();
                        // ValidateSeparator();
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

                    // Objects
                    case TokenKind.Object:
                        HandleObject();
                        // // Update the tree.
                        // newNode = CreateExtensionObject(token, containers.Count);
                        // if (Root == null)
                        // {
                        //     Root = newNode;
                        // }
                        // else
                        // {
                        //     containers.Peek().Node.AddParameter(newNode);
                        // }

                        // // Push the container.
                        // containers.Push(new ContainerInfo() { Node = newNode as ContainerNode, Token = token });

                        // // StartIndex or Dereference should follow.
                        // lastToken = token;
                        // if (_lexer.TryGetNextToken(ref token))
                        // {
                        //     TraceToken(token, containers.Count);
                        // }

                        // if (token == null || token.Kind != TokenKind.OpenHashtable)
                        // {
                        //     throw new ParseException(ParseExceptionKind.ExpectedOpenHashtable, lastToken, _raw);
                        // }

                        break;

                    // Literal values
                    case TokenKind.Boolean:
                    case TokenKind.Number:
                    case TokenKind.Version:
                    case TokenKind.String:
                        HandleLiteral();
                        // ValidateLiteral(token, lastToken);

                        // // Update the tree.
                        // newNode = new LiteralValueNode(token.ParsedValue, _trace, containers.Count);
                        // if (Root == null)
                        // {
                        //     Root = newNode;
                        // }
                        // else
                        // {
                        //     containers.Peek().Node.AddParameter(newNode);
                        // }

                        break;
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

        private void HandleStartIndex()
        {
            // Validate follows an object, property name, or "]".
            if (_lastToken == null ||
                (_lastToken.Kind != TokenKind.Object && _lastToken.Kind != TokenKind.PropertyName && _lastToken.Kind != TokenKind.EndIndex))
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
            // Validate follows an object, property name, or "]".
            if (_lastToken == null ||
                (_lastToken.Kind != TokenKind.Object && _lastToken.Kind != TokenKind.PropertyName && _lastToken.Kind != TokenKind.EndIndex))
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

            // Add the property name as the index.
            string propertyName = _raw.Substring(_token.Index, _token.Length);
            indexer.AddParameter(new LiteralValueNode(propertyName, _trace));
        }

        // private void ValidateStartParameter(Stack<ContainerInfo> containers, Token token, Token lastToken)
        // {
        //     ContainerInfo container = containers.Count > 0 ? containers.Peek() : null;
        //     //                                          // Validate:
        //     if (container == null ||                    // 1) Container is not null
        //         !(container.Node is FunctionNode) ||    // 2) Container is a function
        //         container.Token != lastToken)           // 3) Container is the last token
        //     {
        //         throw new ParseException(ParseExceptionKind.UnexpectedSymbol, token, _raw);
        //     }
        // }

        private void ValidateEndParameter(Stack<ContainerInfo> containers, Token token, Token lastToken)
        {
            ContainerInfo container = containers.Count > 0 ? containers.Peek() : null;      // Validate:
            if (container == null ||                                                        // 1) Container is not null
                !(container.Node is FunctionNode) ||                                        // 2) Container is a function
                container.Node.Parameters.Count < GetMinParamCount(container.Token.Kind) || // 3) At or above min parameters threshold
                lastToken.Kind == TokenKind.Separator)                                      // 4) Last token is not a separator
            {
                throw new ParseException(ParseExceptionKind.UnexpectedSymbol, token, _raw);
            }
        }

        private void ValidateEndIndex(Stack<ContainerInfo> containers, Token token, Token lastToken)
        {
            ContainerInfo container = containers.Count > 0 ? containers.Peek() : null;
            //                                          // Validate:
            if (container == null ||                    // 1) Container is not null
                !(container.Node is HashtableNode) ||   // 2) Container is a hashtable
                container.Node.Parameters.Count != 1)   // 3) Exactly 1 parameter
            {
                throw new ParseException(ParseExceptionKind.UnexpectedSymbol, token, _raw);
            }
        }

        private void ValidateLiteral(Token token, Token lastToken)
        {
            bool expected = false;
            if (lastToken == null) // The first token.
            {
                expected = true;
            }
            else if (lastToken.Kind == TokenKind.OpenFunction ||    // Preceeded by opening punctuation
                lastToken.Kind == TokenKind.OpenHashtable ||        // or by a separator.
                lastToken.Kind == TokenKind.Separator)
            {
                expected = true;
            }

            if (!expected)
            {
                throw new ParseException(ParseExceptionKind.UnexpectedSymbol, token, _raw);
            }
        }

        private void ValidateSeparator(Stack<ContainerInfo> containers, Token token, Token lastToken)
        {
            ContainerInfo container = containers.Count > 0 ? containers.Peek() : null;          // Validate:
            if (container == null ||                                                            // 1) Container is not null
                !(container.Node is FunctionNode) ||                                            // 2) Container is a function
                container.Node.Parameters.Count < 1 ||                                          // 3) At least one parameter
                container.Node.Parameters.Count >= GetMaxParamCount(container.Token.Kind) ||    // 4) Under max parameters threshold
                lastToken.Kind == TokenKind.Separator)                                          // 5) Last token is not a separator
            {
                throw new ParseException(ParseExceptionKind.UnexpectedSymbol, token, _raw);
            }
        }

        private void HandleFunction()
        {
            // Validate follows "," or "[" or "(".
            if (_lastToken == null ||
                (_lastToken.Kind != TokenKind.Separator && _lastToken.Kind != TokenKind.StartIndex && _lastToken.Kind != TokenKind.StartParameter))
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
            if (!TryGetNextToken || _token.Kind != TokenKind.StartParameter)
            {
                throw new ParseException(ParseExceptionKind.ExpectedStartParameter, _lastToken, _raw);
            }
        }

        private ExtensionObjectNode CreateExtensionObject(Token token, int level)
        {
            ArgUtil.NotNull(token, nameof(token));
            switch (token.Kind)
            {
                case TokenKind.ExtensionObject:
                    throw new NotImplementedException();
                default:
                    // Should never reach here.
                    throw new NotSupportedException($"Unexpected hashtable token name: '{token.Kind}'");
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
                case ParseExceptionKind.ExpectedStartParameter:
                    description = "Expected '(' to follow function";
                    break;
                // case ParseExceptionKind.ExpectedOpenHashtable:
                //     description = "Expected '[' to follow hashtable";
                //     break;
                case ParseExceptionKind.UnclosedFunction:
                    description = "Unclosed function";
                    break;
                case ParseExceptionKind.UnclosedIndex:
                    description = "Unclosed index";
                    break;
                case ParseExceptionKind.UnrecognizedValue:
                    description = "Unrecognized value";
                    break;
                case ParseExceptionKind.UnexpectedSymbol:
                    description = "Unexpected symbol";
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
        ExpectedStartParameter,
        ExpectedStartIndex,
        UnclosedFunction,
        UnclosedIndex,
        UnexpectedSymbol,
        UnrecognizedValue,
    }
}