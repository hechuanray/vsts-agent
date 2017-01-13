using Microsoft.VisualStudio.Services.Agent.Util;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.VisualStudio.Services.Agent
{
    public sealed partial class Condition
    {
        private sealed class ContainerInfo
        {
            public ContainerNode Node { get; set; }

            public Token Token { get; set; }
        }

        private void CreateTree()
        {
            _trace.Entering();
            var containers = new Stack<ContainerInfo>();
            Token token = null;
            Token lastToken = null;
            while ((token = GetNextToken()) != null)
            {
                TraceToken(token, containers.Count);
                Node newNode = null;
                switch (token.Kind)
                {
                    case TokenKind.Unrecognized:
                        ThrowParseException("Unrecognized value", token);
                        break;

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
                        newNode = CreateFunction(token, containers.Count);
                        break;

                    // Hashtables
                    case TokenKind.Capabilities:
                    case TokenKind.Variables:
                        newNode = CreateHashtable(token, containers.Count);
                        break;

                    // Literal values
                    case TokenKind.False:
                    case TokenKind.True:
                    case TokenKind.Number:
                    case TokenKind.Version:
                    case TokenKind.String:
                        ValidateLiteral(token, lastToken);
                        newNode = new LiteralValueNode(token.ParsedValue, _trace, containers.Count);
                        break;
                }

                if (newNode != null)
                {
                    // Update the tree.
                    if (_root == null)
                    {
                        _root = newNode;
                    }
                    else
                    {
                        containers.Peek().Node.AddParameter(newNode);
                    }

                    // Adjust the container stack.
                    if (newNode is ContainerNode)
                    {
                        containers.Push(new ContainerInfo() { Node = newNode as ContainerNode, Token = token });
                    }
                }

                lastToken = token;
            }

            // Validate all containers were closed.
            if (containers.Count > 0)
            {
                ContainerInfo container = containers.Peek();
                if (container.Token == lastToken)
                {
                    if (container.Node is FunctionNode)
                    {
                        ThrowParseException("Expected '(' to follow function", lastToken);
                    }
                    else
                    {
                        ThrowParseException("Expected '[' to follow hashtable", lastToken);
                    }
                }
                else
                {
                    if (container.Node is FunctionNode)
                    {
                        ThrowParseException("Unclosed function", container.Token);
                    }
                    else
                    {
                        ThrowParseException("Unclosed hashtable", container.Token);
                    }
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
                ThrowParseException("Unexpected symbol", token);
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
                ThrowParseException("Unexpected symbol", token);
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
                ThrowParseException("Unexpected symbol", token);
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
                ThrowParseException("Unexpected symbol", token);
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
                ThrowParseException("Unexpected symbol", token);
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
                ThrowParseException("Unexpected symbol", token);
            }
        }

        // // private void CreateTree_old()
        // // {
        // //     _trace.Entering();
        // //     int level = 0;
        // //     ContainerNode container = null;
        // //     for (int tokenIndex = 0; tokenIndex < _tokens.Count; tokenIndex++)
        // //     {
        // //         Token token = _tokens[tokenIndex];
        // //         ThrowIfInvalid(token);

        // //         // Check if punctuation.
        // //         var punctuation = token as PunctuationToken;
        // //         if (punctuation != null)
        // //         {
        // //             ValidatePunctuation(container, punctuation, tokenIndex);
        // //             if (punctuation.Value == Constants.Conditions.CloseFunction ||
        // //                 punctuation.Value == Constants.Conditions.CloseHashtable)
        // //             {
        // //                 container = container.Container; // Pop container.
        // //                 level--;
        // //             }

        // //             continue;
        // //         }

        // //         // Validate the token and create the node.
        // //         Node newNode = null;
        // //         if (token is LiteralToken)
        // //         {
        // //             var literalToken = token as LiteralToken;
        // //             ValidateLiteral_old(literalToken, tokenIndex);
        // //             string traceFormat = literalToken is StringToken ? "'{0}' ({1})" : "{0} ({1})";
        // //             _trace.Verbose(string.Empty.PadLeft(level * 2) + traceFormat, literalToken.Value, literalToken.Value.GetType().Name);
        // //             newNode = new LiteralNode(literalToken, _trace, level);
        // //         }
        // //         else if (token is FunctionToken)
        // //         {
        // //             var functionToken = token as FunctionToken;
        // //             ValidateFunction_old(functionToken, tokenIndex);
        // //             tokenIndex++; // Skip the open paren that follows.
        // //             _trace.Verbose(string.Empty.PadLeft(level * 2) + $"{functionToken.Name} (Function)");
        // //             newNode = CreateFunction(functionToken, level);
        // //         }
        // //         else if (token is HashtableToken)
        // //         {
        // //             var hashtableToken = token as HashtableToken;
        // //             ValidateHashtable(hashtableToken, tokenIndex);
        // //             tokenIndex++; // Skip the open bracket that follows.
        // //             _trace.Verbose(string.Empty.PadLeft(level * 2) + $"{hashtableToken.Name} (Hashtable)");
        // //             newNode = CreateHashtable(hashtableToken, level);
        // //         }
        // //         else
        // //         {
        // //             throw new NotSupportedException("Unexpected token type: " + token.GetType().FullName);
        // //         }

        // //         // Update the tree.
        // //         if (_root == null)
        // //         {
        // //             _root = newNode;
        // //         }
        // //         else
        // //         {
        // //             container.AddParameter(newNode);
        // //         }

        // //         // Push the container node.
        // //         if (newNode is ContainerNode)
        // //         {
        // //             container = newNode as ContainerNode;
        // //             level++;
        // //         }
        // //     }
        // // }

        // // private void ThrowIfInvalid(Token token)
        // // {
        // //     ArgUtil.NotNull(token, nameof(token));
        // //     if (token is InvalidToken)
        // //     {
        // //         if (token is MalformedNumberToken)
        // //         {
        // //             ThrowParseException("Unable to parse number", token);
        // //         }
        // //         else if (token is UnterminatedStringToken)
        // //         {
        // //             ThrowParseException("Unterminated string", token);
        // //         }
        // //         else if (token is UnrecognizedToken)
        // //         {
        // //             ThrowParseException("Unrecognized keyword", token);
        // //         }

        // //         throw new NotSupportedException("Unexpected token type: " + token.GetType().FullName);
        // //     }
        // // }

        // // private void ValidateLiteral_old(LiteralToken token, int tokenIndex)
        // // {
        // //     ArgUtil.NotNull(token, nameof(token));

        // //     // Validate nothing follows, a separator follows, or close punction follows.
        // //     Token nextToken = tokenIndex + 1 < _tokens.Count ? _tokens[tokenIndex + 1] : null;
        // //     ValidateNullOrSeparatorOrClosePunctuation(nextToken);
        // // }

        // // private void ValidateHashtable(HashtableToken token, int tokenIndex)
        // // {
        // //     ArgUtil.NotNull(token, nameof(token));

        // //     // Validate open bracket follows.
        // //     PunctuationToken nextToken = tokenIndex + 1 < _tokens.Count ? _tokens[tokenIndex + 1] as PunctuationToken : null;
        // //     if (nextToken == null || nextToken.Value != Constants.Conditions.OpenHashtable)
        // //     {
        // //         ThrowParseException($"Expected '{Constants.Conditions.OpenHashtable}' to follow symbol", token);
        // //     }

        // //     // Validate a literal, hashtable, or function follows.
        // //     Token nextNextToken = tokenIndex + 2 < _tokens.Count ? _tokens[tokenIndex + 2] : null;
        // //     if (nextNextToken as LiteralToken == null && nextNextToken as HashtableToken == null && nextNextToken as FunctionToken == null)
        // //     {
        // //         ThrowParseException("Expected a value to follow symbol", nextToken);
        // //     }
        // // }

        // // private void ValidateFunction_old(FunctionToken token, int tokenIndex)
        // // {
        // //     ArgUtil.NotNull(token, nameof(token));

        // //     // Valdiate open paren follows.
        // //     PunctuationToken nextToken = tokenIndex + 1 < _tokens.Count ? _tokens[tokenIndex + 1] as PunctuationToken : null;
        // //     if (nextToken == null || nextToken.Value != Constants.Conditions.OpenFunction)
        // //     {
        // //         ThrowParseException($"Expected '{Constants.Conditions.OpenFunction}' to follow symbol", token);
        // //     }

        // //     // Validate a literal, hashtable, or function follows.
        // //     Token nextNextToken = tokenIndex + 2 < _tokens.Count ? _tokens[tokenIndex + 2] : null;
        // //     if (nextNextToken as LiteralToken == null && nextNextToken as HashtableToken == null && nextNextToken as FunctionToken == null)
        // //     {
        // //         ThrowParseException("Expected a value to follow symbol", nextToken);
        // //     }
        // // }

        // // private void ValidatePunctuation(ContainerNode container, PunctuationToken token, int tokenIndex)
        // // {
        // //     ArgUtil.NotNull(token, nameof(token));

        // //     // Required open brackets and parens are validated and skipped when a hashtable
        // //     // or function node is created. Any open bracket or paren tokens found at this
        // //     // point are errors.
        // //     if (token.Value == Constants.Conditions.OpenFunction ||
        // //         token.Value == Constants.Conditions.OpenHashtable)
        // //     {
        // //         ThrowParseException("Unexpected symbol", token);
        // //     }

        // //     if (container == null)
        // //     {
        // //         // A condition cannot lead with punction.
        // //         // And punction should not trail the closing of the root node.
        // //         ThrowParseException("Unexpected symbol", token);
        // //     }

        // //     if (token.Value == Constants.Conditions.Separator)
        // //     {
        // //         // Validate current container is a function under max parameters threshold.
        // //         var function = container as FunctionNode;
        // //         if (function == null ||
        // //             function.Parameters.Count >= function.MaxParameters)
        // //         {
        // //             ThrowParseException("Unexpected symbol", token);
        // //         }

        // //         // Validate a literal, function, or hashtable follows.
        // //         Token nextToken = tokenIndex + 1 < _tokens.Count ? _tokens[tokenIndex + 1] : null;
        // //         if (nextToken == null ||
        // //             (!(nextToken is LiteralToken) && !(nextToken is FunctionToken) && !(nextToken is HashtableToken)))
        // //         {
        // //             ThrowParseException("Expected a value to follow the separator symbol", token);
        // //         }
        // //     }
        // //     else if (token.Value == Constants.Conditions.CloseHashtable)
        // //     {
        // //         // Validate nothing follows, a separator follows, or close punction follows.
        // //         Token nextToken = tokenIndex + 1 < _tokens.Count ? _tokens[tokenIndex + 1] : null;
        // //         ValidateNullOrSeparatorOrClosePunctuation(nextToken);
        // //     }
        // //     else if (token.Value == Constants.Conditions.CloseFunction)
        // //     {
        // //         // Validate current container is a function above min parameters threshold.
        // //         var function = container as FunctionNode;
        // //         if (function == null ||
        // //             function.Parameters.Count < function.MinParameters)
        // //         {
        // //             ThrowParseException("Unexpected symbol", token);
        // //         }

        // //         // Validate nothing follows, a separator follows, or close punction follows.
        // //         Token nextToken = tokenIndex + 1 < _tokens.Count ? _tokens[tokenIndex + 1] : null;
        // //         ValidateNullOrSeparatorOrClosePunctuation(nextToken);
        // //     }
        // // }

        // // private void ValidateNullOrSeparatorOrClosePunctuation(Token token)
        // // {
        // //     if (token == null)
        // //     {
        // //         return;
        // //     }

        // //     var punctuation = token as PunctuationToken;
        // //     if (punctuation != null)
        // //     {
        // //         switch (punctuation.Value)
        // //         {
        // //             case Constants.Conditions.CloseFunction:
        // //             case Constants.Conditions.CloseHashtable:
        // //             case Constants.Conditions.Separator:
        // //                 return;
        // //         }
        // //     }

        // //     ThrowParseException("Unexpected symbol", token);
        // // }

        private void ThrowParseException(string description, Token token)
        {
            string rawToken = _raw.Substring(token.Index, token.Length);
            int position = token.Index + 1;
            // TODO: loc
            throw new ParseException($"{description}: '{rawToken}'. Located at position {position} within condition expression: {_raw}");
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
                default:
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
                default:
                    throw new NotSupportedException($"Unexpected token kind '{kind}'. Unable to determine max param count.");
            }
        }

        private sealed class ParseException : Exception
        {
            public ParseException(string message)
                : base(message)
            {
            }
        }

        private abstract class Node
        {
            private static readonly NumberStyles NumberStyles =
                NumberStyles.AllowDecimalPoint |
                NumberStyles.AllowLeadingSign |
                NumberStyles.AllowLeadingWhite |
                NumberStyles.AllowThousands |
                NumberStyles.AllowTrailingWhite;
            private readonly Tracing _trace;
            private readonly int _level;

            public Node(Tracing trace, int level)
            {
                _trace = trace;
                _level = level;
            }

            public ContainerNode Container { get; set; }

            public abstract object GetValue();

            public bool GetValueAsBool()
            {
                object val = GetValue();
                bool result;
                if (val is bool)
                {
                    result = (bool)val;
                }
                else if (val is decimal)
                {
                    result = (decimal)val != 0m; // 0 converts to false, otherwise true.
                    TraceValue(result);
                }
                else
                {
                    result = !string.IsNullOrEmpty(val as string);
                    TraceValue(result);
                }

                return result;
            }

            public decimal GetValueAsNumber()
            {
                object val = GetValue();
                if (val is decimal)
                {
                    return (decimal)val;
                }

                decimal d;
                if (TryConvertToNumber(val, out d))
                {
                    TraceValue(d);
                    return d;
                }

                try
                {
                    return decimal.Parse(
                        val as string ?? string.Empty,
                        NumberStyles,
                        CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    // TODO: loc
                    throw new Exception($"Unable to convert value '{val}' to a number. {ex.Message}");
                }
            }

            public string GetValueAsString()
            {
                string result;
                object val = GetValue();
                if (object.ReferenceEquals(val, null) || val is string)
                {
                    result = val as string;
                }
                else if (val is bool)
                {
                    result = string.Format(CultureInfo.InvariantCulture, "{0}", val);
                    TraceValue(result);
                }
                else
                {
                    decimal d = (decimal)val;
                    result = d.ToString("G", CultureInfo.InvariantCulture);
                    if (result.Contains("."))
                    {
                        result = result.TrimEnd('0').TrimEnd('.'); // Omit trailing zeros after the decimal point.
                    }

                    TraceValue(result);
                }

                return result;
            }

            public bool TryGetValueAsNumber(out decimal result)
            {
                object val = GetValue();
                if (val is decimal)
                {
                    result = (decimal)val;
                    return true;
                }

                if (TryConvertToNumber(val, out result))
                {
                    TraceValue(result);
                    return true;
                }

                TraceValue(val: null, isUnconverted: false, isNotANumber: true);
                return false;
            }

            protected void TraceInfo(string message)
            {
                _trace.Info(string.Empty.PadLeft(_level * 2, '.') + (message ?? string.Empty));
            }

            protected void TraceValue(object val, bool isUnconverted = false, bool isNotANumber = false)
            {
                string prefix = isUnconverted ? string.Empty : "=> ";
                if (isNotANumber)
                {
                    TraceInfo(StringUtil.Format("{0}NaN", prefix));
                }
                else if (val is bool || val is decimal)
                {
                    TraceInfo(StringUtil.Format("{0}{1} ({2})", prefix, val, val.GetType().Name));
                }
                else
                {
                    TraceInfo(StringUtil.Format("{0}{1} (String)", prefix, val));
                }
            }

            private bool TryConvertToNumber(object val, out decimal result)
            {
                if (val is bool)
                {
                    result = (bool)val ? 1m : 0m;
                    return true;
                }
                else if (val is decimal)
                {
                    result = (decimal)val;
                    return true;
                }

                string s = val as string ?? string.Empty;
                if (string.IsNullOrEmpty(s))
                {
                    result = 0m;
                    return true;
                }

                return decimal.TryParse(
                    s,
                    NumberStyles,
                    CultureInfo.InvariantCulture,
                    out result);
            }
        }

        private abstract class ContainerNode : Node
        {
            public ContainerNode(Tracing trace, int level)
                : base(trace, level)
            {
            }

            private readonly List<Node> _parameters = new List<Node>();

            public IReadOnlyList<Node> Parameters => _parameters.AsReadOnly();

            public void AddParameter(Node node)
            {
                _parameters.Add(node);
                node.Container = this;
            }
        }

        private sealed class LiteralValueNode : Node
        {
            private readonly object _value;

            public LiteralValueNode(object value, Tracing trace, int level)
                : base(trace, level)
            {
                _value = value;
            }

            public sealed override object GetValue()
            {
                TraceValue(_value, isUnconverted: true);
                return _value;
            }
        }

        private abstract class HashtableNode : ContainerNode
        {
            public HashtableNode(Tracing trace, int level)
                : base(trace, level)
            {
            }
        }

        private abstract class FunctionNode : ContainerNode
        {
            public FunctionNode(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected abstract string Name { get; }
            
            protected void TraceName()
            {
                TraceInfo($"{Name} (Function)");
            }
        }

        private sealed class AndFunction : FunctionNode
        {
            public AndFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected override string Name => "And";

            public sealed override object GetValue()
            {
                TraceName();
                bool result = true;
                foreach (Node parameter in Parameters)
                {
                    if (!parameter.GetValueAsBool())
                    {
                        result = false;
                        break;
                    }
                }

                TraceValue(result);
                return result;
            }
        }

        private class EqualFunction : FunctionNode
        {
            public EqualFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected override string Name => "Equal";

            public override object GetValue()
            {
                TraceName();
                bool result;
                object left = Parameters[0].GetValue();
                if (left is bool)
                {
                    bool right = Parameters[1].GetValueAsBool();
                    result = (bool)left == right;
                }
                else if (left is decimal)
                {
                    decimal right;
                    if (Parameters[1].TryGetValueAsNumber(out right))
                    {
                        result = (decimal)left == right;
                    }
                    else
                    {
                        result = false;
                    }
                }
                else
                {
                    string right = Parameters[1].GetValueAsString();
                    result = string.Equals(
                        left as string ?? string.Empty,
                        right ?? string.Empty,
                        StringComparison.OrdinalIgnoreCase);
                }

                TraceValue(result);
                return result;
            }
        }

        private class GreaterThanFunction : FunctionNode
        {
            public GreaterThanFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected override string Name => "GreaterThan";

            public override object GetValue()
            {
                TraceName();
                bool result;
                object left = Parameters[0].GetValue();
                if (left is bool)
                {
                    bool right = Parameters[1].GetValueAsBool();
                    result = ((bool)left).CompareTo(right) >= 1;
                }
                else if (left is decimal)
                {
                    decimal right = Parameters[1].GetValueAsNumber();
                    result = ((decimal)left).CompareTo(right) >= 1;
                }
                else
                {
                    string upperLeft = (left as string ?? string.Empty).ToUpperInvariant();
                    string upperRight = (Parameters[1].GetValueAsString() ?? string.Empty).ToUpperInvariant();
                    result = upperLeft.CompareTo(upperRight) >= 1;
                }

                TraceValue(result);
                return result;
            }
        }

        private class GreaterThanOrEqualFunction : FunctionNode
        {
            public GreaterThanOrEqualFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected override string Name => "GreaterThanOrEqual";

            public override object GetValue()
            {
                TraceName();
                bool result;
                object left = Parameters[0].GetValue();
                if (left is bool)
                {
                    bool right = Parameters[1].GetValueAsBool();
                    result = ((bool)left).CompareTo(right) >= 0;
                }
                else if (left is decimal)
                {
                    decimal right = Parameters[1].GetValueAsNumber();
                    result = ((decimal)left).CompareTo(right) >= 0;
                }
                else
                {
                    string upperLeft = (left as string ?? string.Empty).ToUpperInvariant();
                    string upperRight = (Parameters[1].GetValueAsString() ?? string.Empty).ToUpperInvariant();
                    result = upperLeft.CompareTo(upperRight) >= 0;
                }

                TraceValue(result);
                return result;
            }
        }

        private sealed class LessThanFunction : GreaterThanOrEqualFunction
        {
            public LessThanFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected override string Name => "LessThan";

            public sealed override object GetValue()
            {
                bool result = !(bool)base.GetValue();
                TraceValue(result);
                return result;
            }
        }

        private sealed class LessThanOrEqualFunction : GreaterThanFunction
        {
            public LessThanOrEqualFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected override string Name => "LessThanOrEqual";

            public sealed override object GetValue()
            {
                bool result = !(bool)base.GetValue();
                TraceValue(result);
                return result;
            }
        }

        private sealed class NotEqualFunction : EqualFunction
        {
            public NotEqualFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected override string Name => "NotEqual";

            public sealed override object GetValue()
            {
                bool result = !(bool)base.GetValue();
                TraceValue(result);
                return result;
            }
        }

        private sealed class NotFunction : FunctionNode
        {
            public NotFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected override string Name => "Not";

            public sealed override object GetValue()
            {
                TraceName();
                bool result = !Parameters[0].GetValueAsBool();
                TraceValue(result);
                return result;
            }
        }

        private sealed class OrFunction : FunctionNode
        {
            public OrFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected override string Name => "Or";

            public sealed override object GetValue()
            {
                TraceName();
                bool result = false;
                foreach (Node parameter in Parameters)
                {
                    if (parameter.GetValueAsBool())
                    {
                        result = true;
                        break;
                    }
                }

                TraceValue(result);
                return result;
            }
        }

        private sealed class XorFunction : FunctionNode
        {
            public XorFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected override string Name => "Xor";

            public sealed override object GetValue()
            {
                TraceName();
                bool result = Parameters[0].GetValueAsBool() ^ Parameters[1].GetValueAsBool();
                TraceValue(result);
                return result;
            }
        }
    }
}