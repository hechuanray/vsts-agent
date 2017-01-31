using Microsoft.VisualStudio.Services.Agent.Util;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.VisualStudio.Services.Agent.Expressions
{
    public abstract class Node
    {
        private static readonly NumberStyles NumberStyles =
            NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowLeadingWhite |
            NumberStyles.AllowThousands |
            NumberStyles.AllowTrailingWhite;

        public abstract object GetValue(EvaluationContext context);

        internal ContainerNode Container { get; set; }

        internal int Level { get; set; }

        public bool GetValueAsBool(EvaluationContext context)
        {
            object val = GetValue(context);
            bool result;
            if (val is bool)
            {
                result = (bool)val;
            }
            else if (val is decimal)
            {
                result = (decimal)val != 0m; // 0 converts to false, otherwise true.
                TraceValue(context, result);
            }
            else if (val is Version)
            {
                result = true;
                TraceValue(context, result);
            }
            else
            {
                result = !string.IsNullOrEmpty(val as string);
                TraceValue(context, result);
            }

            return result;
        }

        public decimal GetValueAsNumber(EvaluationContext context)
        {
            object val = GetValue(context);
            if (val is decimal)
            {
                return (decimal)val;
            }

            decimal d;
            if (TryConvertToNumber(val, out d))
            {
                TraceValue(context, d);
                return d;
            }
            else if (val is Version)
            {
                throw new ConvertException(val, ValueKind.Number);
            }
            else if (val is string && !string.IsNullOrEmpty(val as string))
            {
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

            return 0;
        }

        public string GetValueAsString(EvaluationContext context)
        {
            string result;
            object val = GetValue(context);
            if (object.ReferenceEquals(val, null) || val is string)
            {
                result = val as string;
            }
            else if (val is bool)
            {
                result = string.Format(CultureInfo.InvariantCulture, "{0}", val);
                TraceValue(context, result);
            }
            else if (val is decimal)
            {
                decimal d = (decimal)val;
                result = d.ToString("G", CultureInfo.InvariantCulture);
                if (result.Contains("."))
                {
                    result = result.TrimEnd('0').TrimEnd('.'); // Omit trailing zeros after the decimal point.
                }

                TraceValue(context, result);
            }
            else
            {
                Version v = val as Version;
                result = v.ToString();
                TraceValue(context, result);
            }

            return result;
        }

        public Version GetValueAsVersion(EvaluationContext context)
        {
            object val = GetValue(context);
            if (val is Version)
            {
                return val as Version;
            }

            Version v;
            if (TryConvertToVersion(val, out v))
            {
                TraceValue(context, v);
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

        public bool TryGetValueAsNumber(EvaluationContext context, out decimal result)
        {
            object val = GetValue(context);
            if (val is decimal)
            {
                result = (decimal)val;
                return true;
            }

            if (TryConvertToNumber(val, out result))
            {
                TraceValue(context, result);
                return true;
            }

            TraceValue(context, val: null, isUnconverted: false, conversionSoftFailed: "Unable to convert to Number");
            return false;
        }

        public bool TryGetValueAsVersion(EvaluationContext context, out Version result)
        {
            object val = GetValue(context);
            if (val is Version)
            {
                result = (Version)val;
                return true;
            }

            if (TryConvertToVersion(val, out result))
            {
                TraceValue(context, result);
                return true;
            }

            TraceValue(context, val: null, isUnconverted: false, conversionSoftFailed: "Unable to convert to Version");
            return false;
        }

        protected void TraceInfo(EvaluationContext context, string message)
        {
            context.Trace.Info(string.Empty.PadLeft(Level * 2, '.') + (message ?? string.Empty));
        }

        protected void TraceValue(EvaluationContext context, object val, bool isUnconverted = false, string conversionSoftFailed = "")
        {
            string prefix = isUnconverted ? string.Empty : "=> ";
            if (!string.IsNullOrEmpty(conversionSoftFailed))
            {
                TraceInfo(context, StringUtil.Format("{0}{1}", prefix, conversionSoftFailed));
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
            else if (val is string)
            {
                kind = ValueKind.String;
            }
            else
            {
                val = "Object";
                kind = ValueKind.Object;
            }

            TraceInfo(context, String.Format(CultureInfo.InvariantCulture, "{0}{1} ({2})", prefix, val, kind));
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

    internal class LeafNode : Node
    {
        private readonly object _value;

        public LeafNode(object val)
        {
            _value = val;
        }

        public sealed override object GetValue(EvaluationContext context)
        {
            TraceValue(context, _value, isUnconverted: true, conversionSoftFailed: string.Empty);
            return _value;
        }
    }

    public abstract class ContainerNode : Node
    {
        private readonly List<Node> _parameters = new List<Node>();

        public IReadOnlyList<Node> Parameters => _parameters.AsReadOnly();

        public void AddParameter(Node node)
        {
            _parameters.Add(node);
            node.Container = this;
            node.Level = Level + 1;
        }

        public void ReplaceParameter(int index, Node node)
        {
            _parameters[index] = node;
            node.Container = this;
            node.Level = Level + 1;
        }

        protected object GetItemProperty(object item, string property)
        {
            if (item is IDictionary<string, object>)
            {
                var dictionary = item as IDictionary<string, object>;
                object result;
                if (dictionary.TryGetValue(property ?? string.Empty, out result))
                {
                    return result;
                }
            }

            return null;
        }
    }

    internal sealed class IndexerNode : ContainerNode
    {
        public sealed override object GetValue(EvaluationContext context)
        {
            TraceInfo(context, $"GetItemProperty");
            object item = Parameters[0].GetValue(context);
            string property = Parameters[1].GetValueAsString(context);
            object result = GetItemProperty(item, property);
            TraceValue(context, result);
            return result;
        }
    }

    internal abstract class FunctionNode : ContainerNode
    {
        protected abstract string Name { get; }

        protected void TraceName(EvaluationContext context)
        {
            TraceInfo(context, $"{Name} (Function)");
        }
    }

    internal sealed class ExtensionNode : FunctionNode
    {
        public ExtensionNode(string name)
        {
            Name = name;
        }

        protected sealed override string Name { get; }

        public sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            object item = context.Extensions[Name];
            string property = Parameters[0].GetValueAsString(context);
            object result = GetItemProperty(item, property);
            TraceValue(context, result);
            return result;
        }
    }

    internal sealed class AndNode : FunctionNode
    {
        protected sealed override string Name => "And";

        public sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result = true;
            foreach (Node parameter in Parameters)
            {
                if (!parameter.GetValueAsBool(context))
                {
                    result = false;
                    break;
                }
            }

            TraceValue(context, result);
            return result;
        }
    }

    internal sealed class ContainsNode : FunctionNode
    {
        protected sealed override string Name => "Contains";

        public override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            string left = Parameters[0].GetValueAsString(context) ?? string.Empty;
            string right = Parameters[1].GetValueAsString(context) ?? string.Empty;
            bool result = left.IndexOf(right, StringComparison.OrdinalIgnoreCase) >= 0;
            TraceValue(context, result);
            return result;
        }
    }

    internal sealed class EndsWithNode : FunctionNode
    {
        protected sealed override string Name => "EndsWith";

        public override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            string left = Parameters[0].GetValueAsString(context) ?? string.Empty;
            string right = Parameters[1].GetValueAsString(context) ?? string.Empty;
            bool result = left.EndsWith(right, StringComparison.OrdinalIgnoreCase);
            TraceValue(context, result);
            return result;
        }
    }

    internal sealed class EqualNode : FunctionNode
    {
        protected sealed override string Name => "Equal";

        public override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result;
            object left = Parameters[0].GetValue(context);
            if (left is bool)
            {
                bool right = Parameters[1].GetValueAsBool(context);
                result = (bool)left == right;
            }
            else if (left is decimal)
            {
                decimal right;
                if (Parameters[1].TryGetValueAsNumber(context, out right))
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
                if (Parameters[1].TryGetValueAsVersion(context, out right))
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
                string right = Parameters[1].GetValueAsString(context);
                result = string.Equals(
                    left as string ?? string.Empty,
                    right ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase);
            }

            TraceValue(context, result);
            return result;
        }
    }

    internal sealed class GreaterThanNode : FunctionNode
    {
        protected sealed override string Name => "GreaterThan";

        public sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result;
            object left = Parameters[0].GetValue(context);
            if (left is bool)
            {
                bool right = Parameters[1].GetValueAsBool(context);
                result = ((bool)left).CompareTo(right) > 0;
            }
            else if (left is decimal)
            {
                decimal right = Parameters[1].GetValueAsNumber(context);
                result = ((decimal)left).CompareTo(right) > 0;
            }
            else if (left is Version)
            {
                Version right = Parameters[1].GetValueAsVersion(context);
                result = (left as Version).CompareTo(right) > 0;
            }
            else
            {
                string right = Parameters[1].GetValueAsString(context);
                result = string.Compare(left as string ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase) > 0;
            }

            TraceValue(context, result);
            return result;
        }
    }

    internal sealed class GreaterThanOrEqualNode : FunctionNode
    {
        protected sealed override string Name => "GreaterThanOrEqual";

        public sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result;
            object left = Parameters[0].GetValue(context);
            if (left is bool)
            {
                bool right = Parameters[1].GetValueAsBool(context);
                result = ((bool)left).CompareTo(right) >= 0;
            }
            else if (left is decimal)
            {
                decimal right = Parameters[1].GetValueAsNumber(context);
                result = ((decimal)left).CompareTo(right) >= 0;
            }
            else if (left is Version)
            {
                Version right = Parameters[1].GetValueAsVersion(context);
                result = (left as Version).CompareTo(right) >= 0;
            }
            else
            {
                string right = Parameters[1].GetValueAsString(context);
                result = string.Compare(left as string ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            TraceValue(context, result);
            return result;
        }
    }

    internal sealed class InNode : FunctionNode
    {
        protected sealed override string Name => "In";

        public override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result = false;
            object left = Parameters[0].GetValue(context);
            for (int i = 1; i < Parameters.Count; i++)
            {
                if (left is bool)
                {
                    bool right = Parameters[i].GetValueAsBool(context);
                    result = (bool)left == right;
                }
                else if (left is decimal)
                {
                    decimal right;
                    if (Parameters[i].TryGetValueAsNumber(context, out right))
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
                    if (Parameters[i].TryGetValueAsVersion(context, out right))
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
                    string right = Parameters[i].GetValueAsString(context);
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

            TraceValue(context, result);
            return result;
        }
    }

    internal sealed class LessThanNode : FunctionNode
    {
        protected sealed override string Name => "LessThan";

        public sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result;
            object left = Parameters[0].GetValue(context);
            if (left is bool)
            {
                bool right = Parameters[1].GetValueAsBool(context);
                result = ((bool)left).CompareTo(right) < 0;
            }
            else if (left is decimal)
            {
                decimal right = Parameters[1].GetValueAsNumber(context);
                result = ((decimal)left).CompareTo(right) < 0;
            }
            else if (left is Version)
            {
                Version right = Parameters[1].GetValueAsVersion(context);
                result = (left as Version).CompareTo(right) < 0;
            }
            else
            {
                string right = Parameters[1].GetValueAsString(context);
                result = string.Compare(left as string ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase) < 0;
            }

            TraceValue(context, result);
            return result;
        }
    }

    internal sealed class LessThanOrEqualNode : FunctionNode
    {
        protected sealed override string Name => "LessThanOrEqual";

        public sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result;
            object left = Parameters[0].GetValue(context);
            if (left is bool)
            {
                bool right = Parameters[1].GetValueAsBool(context);
                result = ((bool)left).CompareTo(right) <= 0;
            }
            else if (left is decimal)
            {
                decimal right = Parameters[1].GetValueAsNumber(context);
                result = ((decimal)left).CompareTo(right) <= 0;
            }
            else if (left is Version)
            {
                Version right = Parameters[1].GetValueAsVersion(context);
                result = (left as Version).CompareTo(right) <= 0;
            }
            else
            {
                string right = Parameters[1].GetValueAsString(context);
                result = string.Compare(left as string ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase) <= 0;
            }

            TraceValue(context, result);
            return result;
        }
    }

    internal sealed class NotEqualNode : FunctionNode
    {
        protected sealed override string Name => "NotEqual";

        public sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result;
            object left = Parameters[0].GetValue(context);
            if (left is bool)
            {
                bool right = Parameters[1].GetValueAsBool(context);
                result = (bool)left != right;
            }
            else if (left is decimal)
            {
                decimal right;
                if (Parameters[1].TryGetValueAsNumber(context, out right))
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
                if (Parameters[1].TryGetValueAsVersion(context, out right))
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
                string right = Parameters[1].GetValueAsString(context);
                result = !string.Equals(
                    left as string ?? string.Empty,
                    right ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase);
            }

            TraceValue(context, result);
            return result;
        }
    }

    internal sealed class NotNode : FunctionNode
    {
        protected sealed override string Name => "Not";

        public sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result = !Parameters[0].GetValueAsBool(context);
            TraceValue(context, result);
            return result;
        }
    }

    internal sealed class NotInNode : FunctionNode
    {
        protected sealed override string Name => "NotIn";

        public sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool found = false;
            object left = Parameters[0].GetValue(context);
            for (int i = 1; i < Parameters.Count; i++)
            {
                if (left is bool)
                {
                    bool right = Parameters[i].GetValueAsBool(context);
                    found = (bool)left == right;
                }
                else if (left is decimal)
                {
                    decimal right;
                    if (Parameters[i].TryGetValueAsNumber(context, out right))
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
                    if (Parameters[i].TryGetValueAsVersion(context, out right))
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
                    string right = Parameters[i].GetValueAsString(context);
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
            TraceValue(context, result);
            return result;
        }
    }

    internal sealed class OrNode : FunctionNode
    {
        protected sealed override string Name => "Or";

        public sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result = false;
            foreach (Node parameter in Parameters)
            {
                if (parameter.GetValueAsBool(context))
                {
                    result = true;
                    break;
                }
            }

            TraceValue(context, result);
            return result;
        }
    }

    internal sealed class StartsWithNode : FunctionNode
    {
        protected sealed override string Name => "StartsWith";

        public override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            string left = Parameters[0].GetValueAsString(context) ?? string.Empty;
            string right = Parameters[1].GetValueAsString(context) ?? string.Empty;
            bool result = left.StartsWith(right, StringComparison.OrdinalIgnoreCase);
            TraceValue(context, result);
            return result;
        }
    }

    internal sealed class XorNode : FunctionNode
    {
        protected sealed override string Name => "Xor";

        public sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result = Parameters[0].GetValueAsBool(context) ^ Parameters[1].GetValueAsBool(context);
            TraceValue(context, result);
            return result;
        }
    }

    internal enum ValueKind
    {
        Boolean,
        Number,
        Object,
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

    public sealed class EvaluationContext
    {
        public EvaluationContext(ITraceWriter trace, IDictionary<string, object> extensions)
        {
            ArgUtil.NotNull(trace, nameof(trace));
            Trace = trace;
            Extensions = extensions ?? new Dictionary<string, object>(0);
        }

        public ITraceWriter Trace { get; }

        public IDictionary<string, object> Extensions { get; }
    }
}