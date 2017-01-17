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
                        if (_root == null)
                        {
                            _root = newNode;
                        }
                        else
                        {
                            containers.Peek().Node.AddParameter(newNode);
                        }

                        // Push the container.
                        containers.Push(new ContainerInfo() { Node = newNode as ContainerNode, Token = token });

                        // Open-function token should follow.
                        lastToken = token;
                        token = GetNextToken();
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
                        if (_root == null)
                        {
                            _root = newNode;
                        }
                        else
                        {
                            containers.Peek().Node.AddParameter(newNode);
                        }

                        // Push the container.
                        containers.Push(new ContainerInfo() { Node = newNode as ContainerNode, Token = token });

                        // Open-hashtable token should follow.
                        lastToken = token;
                        token = GetNextToken();
                        TraceToken(token, containers.Count);
                        if (token == null || token.Kind != TokenKind.OpenHashtable)
                        {
                            throw new ParseException(ParseExceptionKind.ExpectedOpenHashtable, lastToken, _raw);
                        }

                        break;

                    // Literal values
                    case TokenKind.False:
                    case TokenKind.True:
                    case TokenKind.Number:
                    case TokenKind.Version:
                    case TokenKind.String:
                        ValidateLiteral(token, lastToken);

                        // Update the tree.
                        newNode = new LiteralValueNode(token.ParsedValue, _trace, containers.Count);
                        if (_root == null)
                        {
                            _root = newNode;
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

        public enum ParseExceptionKind
        {
            ExpectedOpenFunction,
            ExpectedOpenHashtable,
            UnclosedFunction,
            UnclosedHashtable,
            UnexpectedSymbol,
            UnrecognizedValue,
        }

        public enum ValueKind
        {
            Boolean,
            Number,
            String,
            Version,
        }

        public sealed class ConvertException : Exception
        {
            private readonly string _message;

            public ConvertException(object val, ValueKind toKind, Exception inner = null)
                : base(string.Empty, inner)
            {
                Value = val;
                if (val is bool)
                {
                    FromKind = ValueKind.Boolean;
                }
                else if (val is decimal)
                {
                    FromKind = ValueKind.Number;
                }
                else if (val is Version)
                {
                    FromKind = ValueKind.Version;
                }
                else
                {
                    FromKind = ValueKind.String;
                }

                // TODO: loc
                _message = $"Unable to convert value '{0}' from type {GetKindString(FromKind)} to type {GetKindString(toKind)}.";
                if (inner != null)
                {
                    _message = string.Concat(_message, " ", inner.Message);
                }
            }

            public object Value { get; private set; }

            public ValueKind FromKind { get; private set; }

            public ValueKind ToKind { get; private set; }

            public sealed override string Message => _message;

            private static string GetKindString(ValueKind kind)
            {
                // TODO: loc
                return kind.ToString();
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
                else if (val is Version)
                {
                    result = true;
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
                else if (val is Version)
                {
                    throw new ConvertException(val, ValueKind.Number);
                }

                Exception inner = null;
                try
                {
                    decimal.Parse(
                        val as string ?? string.Empty,
                        NumberStyles,
                        CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    inner = ex;
                }

                throw new ConvertException(val, ValueKind.Number, inner);
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
                else if (val is decimal)
                {
                    decimal d = (decimal)val;
                    result = d.ToString("G", CultureInfo.InvariantCulture);
                    if (result.Contains("."))
                    {
                        result = result.TrimEnd('0').TrimEnd('.'); // Omit trailing zeros after the decimal point.
                    }

                    TraceValue(result);
                }
                else
                {
                    Version v = val as Version;
                    result = v.ToString();
                    TraceValue(result);
                }

                return result;
            }

            public Version GetValueAsVersion()
            {
                object val = GetValue();
                if (val is Version)
                {
                    return val as Version;
                }

                Version v;
                if (TryConvertToVersion(val, out v))
                {
                    TraceValue(v);
                    return v;
                }

                if (val is bool || val is decimal)
                {
                    throw new ConvertException(val, ValueKind.Version);
                }

                Exception inner = null;
                try
                {
                    Version.Parse(val as string ?? string.Empty);
                }
                catch (Exception ex)
                {
                    inner = ex;
                }

                throw new ConvertException(val, ValueKind.Version, inner);
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

                TraceValue(val: null, isUnconverted: false, conversionFailed: "Unable to convert to Number");
                return false;
            }

            public bool TryGetValueAsVersion(out Version result)
            {
                object val = GetValue();
                if (val is Version)
                {
                    result = (Version)val;
                    return true;
                }

                if (TryConvertToVersion(val, out result))
                {
                    TraceValue(result);
                    return true;
                }

                TraceValue(val: null, isUnconverted: false, conversionFailed: "Unable to convert to Version");
                return false;
            }

            protected void TraceInfo(string message)
            {
                _trace.Info(string.Empty.PadLeft(_level * 2, '.') + (message ?? string.Empty));
            }

            protected void TraceValue(object val, bool isUnconverted = false, string conversionFailed = "")
            {
                string prefix = isUnconverted ? string.Empty : "=> ";
                if (!string.IsNullOrEmpty(conversionFailed))
                {
                    TraceInfo(StringUtil.Format("{0}{1}", prefix, conversionFailed));
                }

                ValueKind kind;
                if (val is bool)
                {
                    kind = ValueKind.Boolean;
                }
                else if (val is decimal)
                {
                    kind = ValueKind.Number;
                }
                else if (val is Version)
                {
                    kind = ValueKind.Version;
                }
                else
                {
                    kind = ValueKind.String;
                }

                TraceInfo(String.Format(CultureInfo.InvariantCulture, "{0}{1} ({2})", prefix, val, kind));
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
                else if (val is Version)
                {
                    result = default(decimal);
                    return false;
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

            private bool TryConvertToVersion(object val, out Version result)
            {
                if (val is bool)
                {
                    result = null;
                    return false;
                }
                else if (val is decimal)
                {
                    return Version.TryParse(String.Format(CultureInfo.InvariantCulture, "{0}", val), out result);
                }
                else if (val is Version)
                {
                    result = val as Version;
                    return true;
                }

                string s = val as string ?? string.Empty;
                return Version.TryParse(s, out result);
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

            protected sealed override string Name => "And";

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

        private sealed class ContainsFunction : FunctionNode
        {
            public ContainsFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected sealed override string Name => "Contains";

            public override object GetValue()
            {
                TraceName();
                string left = Parameters[0].GetValueAsString() ?? string.Empty;
                string right = Parameters[1].GetValueAsString() ?? string.Empty;
                bool result = left.IndexOf(right, StringComparison.OrdinalIgnoreCase) >= 0;
                TraceValue(result);
                return result;
            }
        }

        private sealed class EndsWithFunction : FunctionNode
        {
            public EndsWithFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected sealed override string Name => "EndsWith";

            public override object GetValue()
            {
                TraceName();
                string left = Parameters[0].GetValueAsString() ?? string.Empty;
                string right = Parameters[1].GetValueAsString() ?? string.Empty;
                bool result = left.EndsWith(right, StringComparison.OrdinalIgnoreCase);
                TraceValue(result);
                return result;
            }
        }

        private sealed class EqualFunction : FunctionNode
        {
            public EqualFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected sealed override string Name => "Equal";

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
                else if (left is Version)
                {
                    Version right;
                    if (Parameters[1].TryGetValueAsVersion(out right))
                    {
                        result = (Version)left == right;
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

        private sealed class GreaterThanFunction : FunctionNode
        {
            public GreaterThanFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected sealed override string Name => "GreaterThan";

            public sealed override object GetValue()
            {
                TraceName();
                bool result;
                object left = Parameters[0].GetValue();
                if (left is bool)
                {
                    bool right = Parameters[1].GetValueAsBool();
                    result = ((bool)left).CompareTo(right) > 0;
                }
                else if (left is decimal)
                {
                    decimal right = Parameters[1].GetValueAsNumber();
                    result = ((decimal)left).CompareTo(right) > 0;
                }
                else if (left is Version)
                {
                    Version right = Parameters[1].GetValueAsVersion();
                    result = (left as Version).CompareTo(right) > 0;
                }
                else
                {
                    string right = Parameters[1].GetValueAsString();
                    result = string.Compare(left as string ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase) > 0;
                }

                TraceValue(result);
                return result;
            }
        }

        private sealed class GreaterThanOrEqualFunction : FunctionNode
        {
            public GreaterThanOrEqualFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected sealed override string Name => "GreaterThanOrEqual";

            public sealed override object GetValue()
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
                else if (left is Version)
                {
                    Version right = Parameters[1].GetValueAsVersion();
                    result = (left as Version).CompareTo(right) >= 0;
                }
                else
                {
                    string right = Parameters[1].GetValueAsString();
                    result = string.Compare(left as string ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
                }

                TraceValue(result);
                return result;
            }
        }

        private sealed class InFunction : FunctionNode
        {
            public InFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected sealed override string Name => "In";

            public override object GetValue()
            {
                TraceName();
                bool result = false;
                object left = Parameters[0].GetValue();
                for (int i = 1; i < Parameters.Count; i++)
                {
                    if (left is bool)
                    {
                        bool right = Parameters[i].GetValueAsBool();
                        result = (bool)left == right;
                    }
                    else if (left is decimal)
                    {
                        decimal right;
                        if (Parameters[i].TryGetValueAsNumber(out right))
                        {
                            result = (decimal)left == right;
                        }
                        else
                        {
                            result = false;
                        }
                    }
                    else if (left is Version)
                    {
                        Version right;
                        if (Parameters[i].TryGetValueAsVersion(out right))
                        {
                            result = (Version)left == right;
                        }
                        else
                        {
                            result = false;
                        }
                    }
                    else
                    {
                        string right = Parameters[i].GetValueAsString();
                        result = string.Equals(
                            left as string ?? string.Empty,
                            right ?? string.Empty,
                            StringComparison.OrdinalIgnoreCase);
                    }

                    if (result)
                    {
                        break;
                    }
                }

                TraceValue(result);
                return result;
            }
        }

        private sealed class LessThanFunction : FunctionNode
        {
            public LessThanFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected sealed override string Name => "LessThan";

            public sealed override object GetValue()
            {
                TraceName();
                bool result;
                object left = Parameters[0].GetValue();
                if (left is bool)
                {
                    bool right = Parameters[1].GetValueAsBool();
                    result = ((bool)left).CompareTo(right) < 0;
                }
                else if (left is decimal)
                {
                    decimal right = Parameters[1].GetValueAsNumber();
                    result = ((decimal)left).CompareTo(right) < 0;
                }
                else if (left is Version)
                {
                    Version right = Parameters[1].GetValueAsVersion();
                    result = (left as Version).CompareTo(right) < 0;
                }
                else
                {
                    string right = Parameters[1].GetValueAsString();
                    result = string.Compare(left as string ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase) < 0;
                }

                TraceValue(result);
                return result;
            }
        }

        private sealed class LessThanOrEqualFunction : FunctionNode
        {
            public LessThanOrEqualFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected sealed override string Name => "LessThanOrEqual";

            public sealed override object GetValue()
            {
                TraceName();
                bool result;
                object left = Parameters[0].GetValue();
                if (left is bool)
                {
                    bool right = Parameters[1].GetValueAsBool();
                    result = ((bool)left).CompareTo(right) <= 0;
                }
                else if (left is decimal)
                {
                    decimal right = Parameters[1].GetValueAsNumber();
                    result = ((decimal)left).CompareTo(right) <= 0;
                }
                else if (left is Version)
                {
                    Version right = Parameters[1].GetValueAsVersion();
                    result = (left as Version).CompareTo(right) <= 0;
                }
                else
                {
                    string right = Parameters[1].GetValueAsString();
                    result = string.Compare(left as string ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase) <= 0;
                }

                TraceValue(result);
                return result;
            }
        }

        private sealed class NotEqualFunction : FunctionNode
        {
            public NotEqualFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected sealed override string Name => "NotEqual";

            public sealed override object GetValue()
            {
                TraceName();
                bool result;
                object left = Parameters[0].GetValue();
                if (left is bool)
                {
                    bool right = Parameters[1].GetValueAsBool();
                    result = (bool)left != right;
                }
                else if (left is decimal)
                {
                    decimal right;
                    if (Parameters[1].TryGetValueAsNumber(out right))
                    {
                        result = (decimal)left != right;
                    }
                    else
                    {
                        result = true;
                    }
                }
                else if (left is Version)
                {
                    Version right;
                    if (Parameters[1].TryGetValueAsVersion(out right))
                    {
                        result = (Version)left != right;
                    }
                    else
                    {
                        result = true;
                    }
                }
                else
                {
                    string right = Parameters[1].GetValueAsString();
                    result = !string.Equals(
                        left as string ?? string.Empty,
                        right ?? string.Empty,
                        StringComparison.OrdinalIgnoreCase);
                }

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

            protected sealed override string Name => "Not";

            public sealed override object GetValue()
            {
                TraceName();
                bool result = !Parameters[0].GetValueAsBool();
                TraceValue(result);
                return result;
            }
        }

        private sealed class NotInFunction : FunctionNode
        {
            public NotInFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected sealed override string Name => "NotIn";

            public sealed override object GetValue()
            {
                TraceName();
                bool found = false;
                object left = Parameters[0].GetValue();
                for (int i = 1; i < Parameters.Count; i++)
                {
                    if (left is bool)
                    {
                        bool right = Parameters[i].GetValueAsBool();
                        found = (bool)left == right;
                    }
                    else if (left is decimal)
                    {
                        decimal right;
                        if (Parameters[i].TryGetValueAsNumber(out right))
                        {
                            found = (decimal)left == right;
                        }
                        else
                        {
                            found = false;
                        }
                    }
                    else if (left is Version)
                    {
                        Version right;
                        if (Parameters[i].TryGetValueAsVersion(out right))
                        {
                            found = (Version)left == right;
                        }
                        else
                        {
                            found = false;
                        }
                    }
                    else
                    {
                        string right = Parameters[i].GetValueAsString();
                        found = string.Equals(
                            left as string ?? string.Empty,
                            right ?? string.Empty,
                            StringComparison.OrdinalIgnoreCase);
                    }

                    if (found)
                    {
                        break;
                    }
                }

                bool result = !found;
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

            protected sealed override string Name => "Or";

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

        private sealed class StartsWithFunction : FunctionNode
        {
            public StartsWithFunction(Tracing trace, int level)
                : base(trace, level)
            {
            }

            protected sealed override string Name => "StartsWith";

            public override object GetValue()
            {
                TraceName();
                string left = Parameters[0].GetValueAsString() ?? string.Empty;
                string right = Parameters[1].GetValueAsString() ?? string.Empty;
                bool result = left.StartsWith(right, StringComparison.OrdinalIgnoreCase);
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

            protected sealed override string Name => "Xor";

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