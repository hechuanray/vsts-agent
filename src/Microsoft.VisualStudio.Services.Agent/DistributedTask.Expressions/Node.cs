using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Microsoft.VisualStudio.Services.DistributedTask.Expressions
{
    public abstract class Node
    {
        private static readonly NumberStyles NumberStyles =
            NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowLeadingWhite |
            NumberStyles.AllowThousands |
            NumberStyles.AllowTrailingWhite;

        internal ContainerNode Container { get; set; }

        internal int Level { get; set; }

        protected abstract object GetValue(EvaluationContext context);

        public object GetCanonicalValue(EvaluationContext context, out ValueKind kind)
        {
            return ConvertToCanonicalValue(GetValue(context), out kind);
        }

        public bool GetValueAsBoolean(EvaluationContext context)
        {
            bool result;
            ValueKind kind;
            object val = GetCanonicalValue(context, out kind);
            switch (kind)
            {
                case ValueKind.Boolean:
                    result = (bool)val;
                    break;
                case ValueKind.Number:
                    result = (decimal)val != 0m; // 0 converts to false, otherwise true.
                    TraceValue(context, result);
                    break;
                case ValueKind.String:
                    result = !string.IsNullOrEmpty(val as string);
                    TraceValue(context, result);
                    break;
                case ValueKind.Array:
                case ValueKind.Object:
                case ValueKind.Version:
                    result = true;
                    TraceValue(context, result);
                    break;
                case ValueKind.Null:
                    result = true;
                    TraceValue(context, result);
                    break;
                default:
                    throw new NotSupportedException($"Unable to convert value to Boolean. Unexpected value kind '{kind}'.");
            }

            return result;
        }

        public decimal GetValueAsNumber(EvaluationContext context)
        {
            ValueKind kind;
            object val = GetCanonicalValue(context, out kind);
            decimal d;
            if (kind == ValueKind.Number)
            {
                return (decimal)val;
            }
            else if (TryConvertToNumber(val, kind, out d))
            {
                TraceValue(context, d);
                return d;
            }

            throw new ConvertException(val, fromKind: kind, toKind: ValueKind.Number);
        }

        public string GetValueAsString(EvaluationContext context)
        {
            string result;
            ValueKind kind;
            object val = GetCanonicalValue(context, out kind);
            switch (kind)
            {
                case ValueKind.Boolean:
                    result = string.Format(CultureInfo.InvariantCulture, "{0}", val);
                    TraceValue(context, result);
                    return result;
                case ValueKind.Number:
                    result = ((decimal)val).ToString("G", CultureInfo.InvariantCulture);
                    if (result.Contains("."))
                    {
                        result = result.TrimEnd('0').TrimEnd('.'); // Omit trailing zeros after the decimal point.
                    }

                    TraceValue(context, result);
                    return result;
                case ValueKind.String:
                    result = val as string;
                    return result;
                case ValueKind.Version:
                    Version v = val as Version;
                    result = v.ToString();
                    TraceValue(context, result);
                    return result;
                case ValueKind.Array:
                case ValueKind.Object:
                case ValueKind.Null:
                    result = string.Empty;
                    TraceValue(context, result);
                    return result;
                default:
                    throw new NotSupportedException($"Unable to convert to String. Unexpected value kind '{kind}'.");
            }
        }

        public Version GetValueAsVersion(EvaluationContext context)
        {
            ValueKind kind;
            object val = GetCanonicalValue(context, out kind);
            Version v;
            if (kind == ValueKind.Version)
            {
                return val as Version;
            }
            else if (TryConvertToVersion(val, kind, out v))
            {
                TraceValue(context, v);
                return v;
            }

            throw new ConvertException(val, fromKind: kind, toKind: ValueKind.Version);
        }

        public bool TryGetValueAsNumber(EvaluationContext context, out decimal result)
        {
            ValueKind kind;
            object val = GetCanonicalValue(context, out kind);
            if (kind == ValueKind.Number)
            {
                result = (decimal)val;
                return true;
            }
            else if (TryConvertToNumber(val, kind, out result))
            {
                TraceValue(context, result);
                return true;
            }

            TraceCoercionFailed(context, fromKind: kind, toKind: ValueKind.Number);
            return false;
        }

        public bool TryGetValueAsVersion(EvaluationContext context, out Version result)
        {
            ValueKind kind;
            object val = GetCanonicalValue(context, out kind);
            if (kind == ValueKind.Version)
            {
                result = (Version)val;
                return true;
            }
            else if (TryConvertToVersion(val, kind, out result))
            {
                TraceValue(context, result);
                return true;
            }

            TraceCoercionFailed(context, fromKind: kind, toKind: ValueKind.Version);
            return false;
        }

        internal void TraceValue(EvaluationContext context, object val, bool isLiteral)
        {
            ValueKind kind;
            val = ConvertToCanonicalValue(val, out kind);
            string prefix = isLiteral ? string.Empty : "=> ";
            switch (kind)
            {
                case ValueKind.Boolean:
                case ValueKind.Number:
                case ValueKind.String:
                case ValueKind.Version:
                    TraceVerbose(context, String.Format(CultureInfo.InvariantCulture, "{0}{1}: '{2}'", prefix, kind, val));
                    break;
                default:
                    TraceVerbose(context, string.Format(CultureInfo.InvariantCulture, "{0}{1}", prefix, kind));
                    break;
            }
        }

        internal void TraceVerbose(EvaluationContext context, string message)
        {
            context.Trace.Verbose(string.Empty.PadLeft(Level * 2, '.') + (message ?? string.Empty));
        }

        protected void TraceValue(EvaluationContext context, object val)
        {
            TraceValue(context, val, isLiteral: false);
        }

        private void TraceCoercionFailed(EvaluationContext context, ValueKind fromKind, ValueKind toKind)
        {
            TraceVerbose(context, string.Format(CultureInfo.InvariantCulture, "=> Unable to coerce {0} to {1}.", fromKind, toKind));
        }

        private static object ConvertToCanonicalValue(object val, out ValueKind kind)
        {
            if (object.ReferenceEquals(val, null))
            {
                kind = ValueKind.Null;
                return null;
            }
            else if (val is JToken)
            {
                var jtoken = val as JToken;
                switch (jtoken.Type)
                {
                    case JTokenType.Array:
                        kind = ValueKind.Array;
                        return jtoken;
                    case JTokenType.Boolean:
                        kind = ValueKind.Boolean;
                        return jtoken.ToObject<bool>();
                    case JTokenType.Float:
                        kind = ValueKind.Number;
                        // todo: test the extents of the conversion
                        return jtoken.ToObject<decimal>();
                    case JTokenType.Integer:
                        kind = ValueKind.Number;
                        // todo: test the extents of the conversion
                        return jtoken.ToObject<decimal>();
                    case JTokenType.Null:
                        kind = ValueKind.Null;
                        return null;
                    case JTokenType.Object:
                        kind = ValueKind.Object;
                        return jtoken;
                    case JTokenType.String:
                        kind = ValueKind.String;
                        return jtoken.ToObject<string>();
                }
            }
            else if (val is string)
            {
                kind = ValueKind.Boolean;
                return val;
            }
            else if (val is Version)
            {
                kind = ValueKind.Version;
                return val;
            }
            else if (!val.GetType().GetTypeInfo().IsClass)
            {
                if (val is bool)
                {
                    kind = ValueKind.Boolean;
                    return val;
                }
                else if (val is decimal || val is byte || val is sbyte || val is short || val is ushort || val is int || val is uint || val is long || val is ulong || val is float || val is double)
                {
                    kind = ValueKind.Number;
                    // todo: test the extents of the conversion
                    return (decimal)val;
                }
            }

            kind = ValueKind.Object;
            return val;
        }

        private static bool TryConvertToNumber(object val, ValueKind kind, out decimal result)
        {
            switch (kind)
            {
                case ValueKind.Boolean:
                    result = (bool)val ? 1m : 0m;
                    return true;
                case ValueKind.Number:
                    result = (decimal)val;
                    return true;
                case ValueKind.Version:
                    result = default(decimal);
                    return false;
                case ValueKind.String:
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
                case ValueKind.Array:
                case ValueKind.Object:
                    result = default(decimal);
                    return false;
                case ValueKind.Null:
                    result = 0m;
                    return true;
                default:
                    throw new NotSupportedException($"Unable to determine whether value can be converted to Number. Unexpected value kind '{kind}'.");
            }
        }

        private static bool TryConvertToVersion(object val, ValueKind kind, out Version result)
        {
            switch (kind)
            {
                case ValueKind.Boolean:
                    result = null;
                    return false;
                case ValueKind.Number:
                    return Version.TryParse(string.Format(CultureInfo.InvariantCulture, "{0}", val), out result);
                case ValueKind.Version:
                    result = val as Version;
                    return true;
                case ValueKind.String:
                    string s = val as string ?? string.Empty;
                    return Version.TryParse(s, out result);
                case ValueKind.Array:
                case ValueKind.Object:
                case ValueKind.Null:
                    result = null;
                    return false;
                default:
                    throw new NotSupportedException($"Unable to convert to Version. Unexpected value kind '{kind}'.");
            }
        }
    }

    public class LeafNode : Node
    {
        public LeafNode(object val)
        {
            Value = val;
        }

        public object Value { get; }

        protected sealed override object GetValue(EvaluationContext context)
        {
            TraceValue(context, Value, isLiteral: true);
            return Value;
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
    }

    internal sealed class IndexerNode : ContainerNode
    {
        protected sealed override object GetValue(EvaluationContext context)
        {
            TraceVerbose(context, $"Indexer");
            object result = null;
            ValueKind itemKind;
            object item = Parameters[0].GetCanonicalValue(context, out itemKind);
            if (itemKind == ValueKind.Array && item is JArray)
            {
                var jarray = item as JArray;
                ValueKind indexKind;
                object index = Parameters[1].GetCanonicalValue(context, out indexKind);
                if (indexKind == ValueKind.Number)
                {
                    decimal d = (decimal)index;
                    if (d >= 0m && d < (decimal)jarray.Count && d == Math.Floor(d))
                    {
                        result = jarray[(int)d];
                    }
                }
                else if (indexKind == ValueKind.String && !string.IsNullOrEmpty(index as string))
                {
                    decimal d;
                    if (Parameters[1].TryGetValueAsNumber(context, out d))
                    {
                        if (d >= 0m && d < (decimal)jarray.Count && d == Math.Floor(d))
                        {
                            result = jarray[(int)d];
                        }
                    }
                }
            }
            else if (itemKind == ValueKind.Object && item is JObject)
            {
                var jobject = item as JObject;
                string key = Parameters[1].GetValueAsString(context);
                result = jobject[key];
            }

            TraceValue(context, result);
            return result;
        }

        // protected object GetItemProperty(object item, string property)
        // {
        //     if (item is IDictionary<string, object>)
        //     {
        //         var dictionary = item as IDictionary<string, object>;
        //         object result;
        //         if (dictionary.TryGetValue(property ?? string.Empty, out result))
        //         {
        //             return result;
        //         }
        //     }

        //     return null;
        // }
    }

    public abstract class FunctionNode : ContainerNode
    {
        protected abstract string Name { get; }

        protected void TraceName(EvaluationContext context)
        {
            TraceVerbose(context, $"Function: {Name}");
        }
    }

    public sealed class AndNode : FunctionNode
    {
        protected sealed override string Name => "and";

        protected sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result = true;
            foreach (Node parameter in Parameters)
            {
                if (!parameter.GetValueAsBoolean(context))
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
        protected sealed override string Name => "contains";

        protected sealed override object GetValue(EvaluationContext context)
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
        protected sealed override string Name => "endsWith";

        protected sealed override object GetValue(EvaluationContext context)
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
        protected sealed override string Name => "equal";

        protected sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result;
            object left = Parameters[0].GetValue(context);
            if (left is bool)
            {
                bool right = Parameters[1].GetValueAsBoolean(context);
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
        protected sealed override string Name => "greaterThan";

        protected sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result;
            object left = Parameters[0].GetValue(context);
            if (left is bool)
            {
                bool right = Parameters[1].GetValueAsBoolean(context);
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
        protected sealed override string Name => "greaterThanOrEqual";

        protected sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result;
            object left = Parameters[0].GetValue(context);
            if (left is bool)
            {
                bool right = Parameters[1].GetValueAsBoolean(context);
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
        protected sealed override string Name => "in";

        protected sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result = false;
            object left = Parameters[0].GetValue(context);
            for (int i = 1; i < Parameters.Count; i++)
            {
                if (left is bool)
                {
                    bool right = Parameters[i].GetValueAsBoolean(context);
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
        protected sealed override string Name => "lessThan";

        protected sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result;
            object left = Parameters[0].GetValue(context);
            if (left is bool)
            {
                bool right = Parameters[1].GetValueAsBoolean(context);
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
        protected sealed override string Name => "lessThanOrEqual";

        protected sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result;
            object left = Parameters[0].GetValue(context);
            if (left is bool)
            {
                bool right = Parameters[1].GetValueAsBoolean(context);
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
        protected sealed override string Name => "notEqual";

        protected sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result;
            object left = Parameters[0].GetValue(context);
            if (left is bool)
            {
                bool right = Parameters[1].GetValueAsBoolean(context);
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
        protected sealed override string Name => "not";

        protected sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result = !Parameters[0].GetValueAsBoolean(context);
            TraceValue(context, result);
            return result;
        }
    }

    internal sealed class NotInNode : FunctionNode
    {
        protected sealed override string Name => "notIn";

        protected sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool found = false;
            object left = Parameters[0].GetValue(context);
            for (int i = 1; i < Parameters.Count; i++)
            {
                if (left is bool)
                {
                    bool right = Parameters[i].GetValueAsBoolean(context);
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
        protected sealed override string Name => "or";

        protected sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result = false;
            foreach (Node parameter in Parameters)
            {
                if (parameter.GetValueAsBoolean(context))
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
        protected sealed override string Name => "startsWith";

        protected sealed override object GetValue(EvaluationContext context)
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
        protected sealed override string Name => "xor";

        protected sealed override object GetValue(EvaluationContext context)
        {
            TraceName(context);
            bool result = Parameters[0].GetValueAsBoolean(context) ^ Parameters[1].GetValueAsBoolean(context);
            TraceValue(context, result);
            return result;
        }
    }

    public enum ValueKind
    {
        Array,
        Boolean,
        Null,
        Number,
        Object,
        String,
        Version,
    }

    internal sealed class ConvertException : Exception
    {
        private readonly string _message;

        public ConvertException(object val, ValueKind fromKind, ValueKind toKind)
        {
            Value = val;
            FromKind = fromKind;
            ToKind = toKind;
            switch (fromKind)
            {
                case ValueKind.Boolean:
                case ValueKind.Number:
                case ValueKind.String:
                case ValueKind.Version:
                    // TODO: loc
                    _message = $"Unable to convert from {FromKind} to {ToKind}. Value: '{val}'";
                    break;
                default:
                    // TODO: loc
                    _message = $"Unable to convert from {FromKind} to {ToKind}. Value: '{val}'";
                    break;
            }
        }

        public object Value { get; private set; }

        public ValueKind FromKind { get; private set; }

        public ValueKind ToKind { get; private set; }

        public sealed override string Message => _message;
    }
}