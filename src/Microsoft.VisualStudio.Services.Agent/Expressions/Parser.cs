using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Services.Agent.Expressions
{
    internal sealed class Parser
    {
        private readonly LexicalAnalyzer _lexer;
        private readonly string _raw; // Raw expression string.
        private readonly ITraceWriter _trace;

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

        private void CreateTree()
        {
            _trace.Verbose($"Entering {nameof(CreateTree)}");
            var containers = new Stack<ContainerInfo>();
            Token token = null;
            Token lastToken = null;
            while ((token = _lexer.GetNextToken()) != null)
            {
                TraceToken(token, containers.Count);
                Node newNode = null;
                switch (token.Kind)
                {
                    case TokenKind.Unrecognized:
                        throw new ParseException(ParseExceptionKind.UnrecognizedValue, token, _raw);

                    // Punctuation
                    case TokenKind.CloseFunction:
                        ValidateCloseFunction(containers, token, lastToken);
                        containers.Pop();
                        break;
                    case TokenKind.CloseHashtable:
                        ValidateCloseHashtable(containers, token, lastToken);
                        containers.Pop();
                        break;
                    case TokenKind.OpenFunction:
                        ValidateOpenFunction(containers, token, lastToken);
                        break;
                    case TokenKind.OpenHashtable:
                        ValidateOpenHashtable(containers, token, lastToken);
                        break;
                    case TokenKind.Separator:
                        ValidateSeparator(containers, token, lastToken);
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
                        // Update the tree.
                        newNode = CreateFunction(token, containers.Count);
                        if (Root == null)
                        {
                            Root = newNode;
                        }
                        else
                        {
                            containers.Peek().Node.AddParameter(newNode);
                        }

                        // Push the container.
                        containers.Push(new ContainerInfo() { Node = newNode as ContainerNode, Token = token });

                        // Open-function token should follow.
                        lastToken = token;
                        token = _lexer.GetNextToken();
                        TraceToken(token, containers.Count);
                        if (token == null || token.Kind != TokenKind.OpenFunction)
                        {
                            throw new ParseException(ParseExceptionKind.ExpectedOpenFunction, lastToken, _raw);
                        }

                        break;

                    // Hashtables
                    case TokenKind.Capabilities:
                    case TokenKind.Variables:
                        // Update the tree.
                        newNode = CreateHashtable(token, containers.Count);
                        if (Root == null)
                        {
                            Root = newNode;
                        }
                        else
                        {
                            containers.Peek().Node.AddParameter(newNode);
                        }

                        // Push the container.
                        containers.Push(new ContainerInfo() { Node = newNode as ContainerNode, Token = token });

                        // Open-hashtable token should follow.
                        lastToken = token;
                        token = _lexer.GetNextToken();
                        TraceToken(token, containers.Count);
                        if (token == null || token.Kind != TokenKind.OpenHashtable)
                        {
                            throw new ParseException(ParseExceptionKind.ExpectedOpenHashtable, lastToken, _raw);
                        }

                        break;

                    // Literal values
                    case TokenKind.Boolean:
                    case TokenKind.Number:
                    case TokenKind.Version:
                    case TokenKind.String:
                        ValidateLiteral(token, lastToken);

                        // Update the tree.
                        newNode = new LiteralValueNode(token.ParsedValue, _trace, containers.Count);
                        if (Root == null)
                        {
                            Root = newNode;
                        }
                        else
                        {
                            containers.Peek().Node.AddParameter(newNode);
                        }

                        break;
                }

                lastToken = token;
            }

            // Validate all containers were closed.
            if (containers.Count > 0)
            {
                ContainerInfo container = containers.Peek();
                if (container.Node is FunctionNode)
                {
                    throw new ParseException(ParseExceptionKind.UnclosedFunction, container.Token, _raw);
                }
                else
                {
                    throw new ParseException(ParseExceptionKind.UnclosedHashtable, container.Token, _raw);
                }
            }
        }

        private void ValidateCloseFunction(Stack<ContainerInfo> containers, Token token, Token lastToken)
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

        private void ValidateCloseHashtable(Stack<ContainerInfo> containers, Token token, Token lastToken)
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

        private void ValidateOpenFunction(Stack<ContainerInfo> containers, Token token, Token lastToken)
        {
            ContainerInfo container = containers.Count > 0 ? containers.Peek() : null;
            //                                          // Validate:
            if (container == null ||                    // 1) Container is not null
                !(container.Node is FunctionNode) ||    // 2) Container is a function
                container.Token != lastToken)           // 3) Container is the last token
            {
                throw new ParseException(ParseExceptionKind.UnexpectedSymbol, token, _raw);
            }
        }

        private void ValidateOpenHashtable(Stack<ContainerInfo> containers, Token token, Token lastToken)
        {
            ContainerInfo container = containers.Count > 0 ? containers.Peek() : null;
            //                                          // Validate:
            if (container == null ||                    // 1) Container is not null
                !(container.Node is HashtableNode) ||   // 2) Container is a function
                container.Token != lastToken)           // 3) Container is the last token
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

        private FunctionNode CreateFunction(Token token, int level)
        {
            ArgUtil.NotNull(token, nameof(token));
            switch (token.Kind)
            {
                case TokenKind.And:
                    return new AndFunction(_trace, level);
                case TokenKind.Equal:
                    return new EqualFunction(_trace, level);
                case TokenKind.GreaterThan:
                    return new GreaterThanFunction(_trace, level);
                case TokenKind.GreaterThanOrEqual:
                    return new GreaterThanOrEqualFunction(_trace, level);
                case TokenKind.LessThan:
                    return new LessThanFunction(_trace, level);
                case TokenKind.LessThanOrEqual:
                    return new LessThanOrEqualFunction(_trace, level);
                case TokenKind.Not:
                    return new NotFunction(_trace, level);
                case TokenKind.NotEqual:
                    return new NotEqualFunction(_trace, level);
                case TokenKind.Or:
                    return new OrFunction(_trace, level);
                case TokenKind.Xor:
                    return new XorFunction(_trace, level);
                default:
                    // Should never reach here.
                    throw new NotSupportedException($"Unexpected function token name: '{token.Kind}'");
            }
        }

        private HashtableNode CreateHashtable(Token token, int level)
        {
            ArgUtil.NotNull(token, nameof(token));
            switch (token.Kind)
            {
                case TokenKind.Capabilities:
                case TokenKind.Variables:
                    throw new NotImplementedException();
                default:
                    // Should never reach here.
                    throw new NotSupportedException($"Unexpected hashtable token name: '{token.Kind}'");
            }
        }

        private void TraceToken(Token token, int level)
        {
            string indent = string.Empty.PadRight(level * 2, '.');
            switch (token.Kind)
            {
                case TokenKind.Number:
                case TokenKind.Version:
                case TokenKind.String:
                    _trace.Verbose($"{indent}{token.Kind} '{token.ParsedValue}'");
                    break;
                case TokenKind.Unrecognized:
                    _trace.Verbose($"{indent}{token.Kind} '{_raw.Substring(token.Index, token.Length)}'");
                    break;
                case TokenKind.CloseFunction:
                case TokenKind.CloseHashtable:
                case TokenKind.OpenFunction:
                case TokenKind.OpenHashtable:
                case TokenKind.Separator:
                    _trace.Verbose($"{indent}{_raw.Substring(token.Index, 1)}");
                    break;
                default:
                    _trace.Verbose($"{indent}{token.Kind}");
                    break;
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
                case ParseExceptionKind.ExpectedOpenFunction:
                    description = "Expected '(' to follow function";
                    break;
                case ParseExceptionKind.ExpectedOpenHashtable:
                    description = "Expected '[' to follow hashtable";
                    break;
                case ParseExceptionKind.UnclosedFunction:
                    description = "Unclosed function";
                    break;
                case ParseExceptionKind.UnclosedHashtable:
                    description = "Unclosed hashtable";
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
        ExpectedOpenFunction,
        ExpectedOpenHashtable,
        UnclosedFunction,
        UnclosedHashtable,
        UnexpectedSymbol,
        UnrecognizedValue,
    }
}