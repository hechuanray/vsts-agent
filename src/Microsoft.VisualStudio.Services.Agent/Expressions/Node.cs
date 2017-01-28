using Microsoft.VisualStudio.Services.Agent.Util;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.VisualStudio.Services.Agent.Expressions
{
    internal abstract class Node
    {
        private static readonly NumberStyles NumberStyles =
            NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowLeadingWhite |
            NumberStyles.AllowThousands |
            NumberStyles.AllowTrailingWhite;
        private readonly ITraceWriter _trace;
        private readonly int _level;

        public Node(ITraceWriter trace, int level)
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

    internal abstract class ContainerNode : Node
    {
        public ContainerNode(ITraceWriter trace, int level)
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

    internal sealed class LiteralValueNode : Node
    {
        private readonly object _value;

        public LiteralValueNode(object value, ITraceWriter trace, int level)
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

    internal abstract class HashtableNode : ContainerNode
    {
        public HashtableNode(ITraceWriter trace, int level)
            : base(trace, level)
        {
        }
    }

    internal abstract class FunctionNode : ContainerNode
    {
        public FunctionNode(ITraceWriter trace, int level)
            : base(trace, level)
        {
        }

        protected abstract string Name { get; }

        protected void TraceName()
        {
            TraceInfo($"{Name} (Function)");
        }
    }

    internal sealed class AndFunction : FunctionNode
    {
        public AndFunction(ITraceWriter trace, int level)
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

    internal sealed class ContainsFunction : FunctionNode
    {
        public ContainsFunction(ITraceWriter trace, int level)
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

    internal sealed class EndsWithFunction : FunctionNode
    {
        public EndsWithFunction(ITraceWriter trace, int level)
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

    internal sealed class EqualFunction : FunctionNode
    {
        public EqualFunction(ITraceWriter trace, int level)
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

    internal sealed class GreaterThanFunction : FunctionNode
    {
        public GreaterThanFunction(ITraceWriter trace, int level)
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

    internal sealed class GreaterThanOrEqualFunction : FunctionNode
    {
        public GreaterThanOrEqualFunction(ITraceWriter trace, int level)
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

    internal sealed class InFunction : FunctionNode
    {
        public InFunction(ITraceWriter trace, int level)
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

    internal sealed class LessThanFunction : FunctionNode
    {
        public LessThanFunction(ITraceWriter trace, int level)
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

    internal sealed class LessThanOrEqualFunction : FunctionNode
    {
        public LessThanOrEqualFunction(ITraceWriter trace, int level)
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

    internal sealed class NotEqualFunction : FunctionNode
    {
        public NotEqualFunction(ITraceWriter trace, int level)
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

    internal sealed class NotFunction : FunctionNode
    {
        public NotFunction(ITraceWriter trace, int level)
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

    internal sealed class NotInFunction : FunctionNode
    {
        public NotInFunction(ITraceWriter trace, int level)
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

    internal sealed class OrFunction : FunctionNode
    {
        public OrFunction(ITraceWriter trace, int level)
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

    internal sealed class StartsWithFunction : FunctionNode
    {
        public StartsWithFunction(ITraceWriter trace, int level)
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

    internal sealed class XorFunction : FunctionNode
    {
        public XorFunction(ITraceWriter trace, int level)
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

    internal enum ValueKind
    {
        Boolean,
        Number,
        String,
        Version,
    }

    internal sealed class ConvertException : Exception
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
}