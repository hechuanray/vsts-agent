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

        public int Level { get; internal set; }

        internal ContainerNode Container { get; set; }

        protected abstract object Evaluate(EvaluationContext context);

        public bool EvaluateBoolean(EvaluationContext context)
        {
            ValueKind kind;
            object val = EvaluateCanonical(context, out kind);
            return ConvertToBoolean(context, Level, val, kind);
        }

        public object EvaluateCanonical(EvaluationContext context, out ValueKind kind)
        {
            return ConvertToCanonicalValue(Evaluate(context), out kind);
        }

        public decimal EvaluateNumber(EvaluationContext context)
        {
            ValueKind kind;
            object val = EvaluateCanonical(context, out kind);
            return ConvertToNumber(context, Level, val, kind);
        }

        public string EvaluateString(EvaluationContext context)
        {
            ValueKind kind;
            object val = EvaluateCanonical(context, out kind);
            return ConvertToString(context, Level, val, kind);
        }

        public Version EvaluateVersion(EvaluationContext context)
        {
            ValueKind kind;
            object val = EvaluateCanonical(context, out kind);
            return ConvertToVersion(context, Level, val, kind);
        }

        internal static void TraceValue(EvaluationContext context, int level, object val, bool isLiteral)
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
                    TraceVerbose(context, level, String.Format(CultureInfo.InvariantCulture, "{0}{1}: '{2}'", prefix, kind, val));
                    break;
                default:
                    TraceVerbose(context, level, string.Format(CultureInfo.InvariantCulture, "{0}{1}", prefix, kind));
                    break;
            }
        }

        internal static void TraceVerbose(EvaluationContext context, int level, string message)
        {
            context.Trace.Verbose(string.Empty.PadLeft(level * 2, '.') + (message ?? string.Empty));
        }

        protected static int Compare(EvaluationContext context, int level, Node left, Node right)
        {
            ValueKind leftKind;
            object leftObj = left.EvaluateCanonical(context, out leftKind);
            ValueKind rightKind;
            object rightObj = right.EvaluateCanonical(context, out rightKind);
            return Compare(context, level, leftObj, leftKind, rightObj, rightKind);
        }

        protected static int Compare(EvaluationContext context, int level, object left, ValueKind leftKind, object right, ValueKind rightKind)
        {
            switch (leftKind)
            {
                case ValueKind.Boolean:
                case ValueKind.Number:
                case ValueKind.String:
                case ValueKind.Version:
                    break;
                default:
                    left = ConvertToNumber(context, level, left, leftKind); // Will throw or succeed
                    leftKind = ValueKind.Number;
                    break;
            }

            if (leftKind == ValueKind.Boolean)
            {
                bool b = ConvertToBoolean(context, level, right, rightKind);
                return ((bool)left).CompareTo(b);
            }
            else if (leftKind == ValueKind.Number)
            {
                decimal d = ConvertToNumber(context, level, right, rightKind);
                return ((decimal)left).CompareTo(d);
            }
            else if (leftKind == ValueKind.String)
            {
                string s = ConvertToString(context, level, right, rightKind);
                return string.Compare(left as string ?? string.Empty, s ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }
            else //if (leftKind == ValueKind.Version)
            {
                Version v = ConvertToVersion(context, level, right, rightKind);
                return (left as Version).CompareTo(v);
            }
        }

        protected static bool ConvertToBoolean(EvaluationContext context, int level, object val, ValueKind kind)
        {
            bool result;
            switch (kind)
            {
                case ValueKind.Boolean:
                    result = (bool)val; // Not converted. Do not trace.
                    return result;

                case ValueKind.Number:
                    result = (decimal)val != 0m; // 0 converts to false, otherwise true.
                    TraceValue(context, level, result);
                    return result;

                case ValueKind.String:
                    result = !string.IsNullOrEmpty(val as string);
                    TraceValue(context, level, result);
                    return result;

                case ValueKind.Array:
                case ValueKind.Object:
                case ValueKind.Version:
                    result = true;
                    TraceValue(context, level, result);
                    return result;

                case ValueKind.Null:
                    result = true;
                    TraceValue(context, level, result);
                    return result;

                default:
                    throw new NotSupportedException($"Unable to convert value to Boolean. Unexpected value kind '{kind}'.");
            }
        }

        protected static object ConvertToNull(EvaluationContext context, int level, object val, ValueKind kind)
        {
            object result;
            if (TryConvertToNull(context, level, val, kind, out result))
            {
                return result;
            }

            throw new ConvertException(val, fromKind: kind, toKind: ValueKind.Null);
        }

        protected static decimal ConvertToNumber(EvaluationContext context, int level, object val, ValueKind kind)
        {
            decimal result;
            if (TryConvertToNumber(context, level, val, kind, out result))
            {
                return result;
            }

            throw new ConvertException(val, fromKind: kind, toKind: ValueKind.Number);
        }

        protected static string ConvertToString(EvaluationContext context, int level, object val, ValueKind kind)
        {
            string result;
            if (TryConvertToString(context, level, val, kind, out result))
            {
                return result;
            }

            throw new ConvertException(val, fromKind: kind, toKind: ValueKind.String);
        }

        protected static Version ConvertToVersion(EvaluationContext context, int level, object val, ValueKind kind)
        {
            Version result;
            if (TryConvertToVersion(context, level, val, kind, out result))
            {
                return result;
            }

            throw new ConvertException(val, fromKind: kind, toKind: ValueKind.Version);
        }

        protected static bool Equals(EvaluationContext context, int level, Node left, Node right)
        {
            ValueKind leftKind;
            object leftObj = left.EvaluateCanonical(context, out leftKind);
            ValueKind rightKind;
            object rightObj = right.EvaluateCanonical(context, out rightKind);
            return Equals(context, level, leftObj, leftKind, rightObj, rightKind);
        }

        protected static bool Equals(EvaluationContext context, int level, object left, ValueKind leftKind, object right, ValueKind rightKind)
        {
            if (leftKind == ValueKind.Boolean)
            {
                bool b = ConvertToBoolean(context, level, right, rightKind);
                return (bool)left == b;
            }
            else if (leftKind == ValueKind.Number)
            {
                decimal d;
                if (TryConvertToNumber(context, level, right, rightKind, out d))
                {
                    return (decimal)left == d;
                }
            }
            else if (leftKind == ValueKind.Version)
            {
                Version v;
                if (TryConvertToVersion(context, level, right, rightKind, out v))
                {
                    return (Version)left == v;
                }
            }
            else if (leftKind == ValueKind.String)
            {
                string s;
                if (TryConvertToString(context, level, right, rightKind, out s))
                {
                    return string.Equals(
                        left as string ?? string.Empty,
                        s ?? string.Empty,
                        StringComparison.OrdinalIgnoreCase);
                }
            }
            else if (leftKind == ValueKind.Array || leftKind == ValueKind.Object)
            {
                return leftKind == rightKind && object.ReferenceEquals(left, right);
            }
            else if (leftKind == ValueKind.Null)
            {
                object n;
                if (TryConvertToNull(context, level, right, rightKind, out n))
                {
                    return true;
                }
            }

            return false;
        }

        protected static bool TryConvertToNull(EvaluationContext context, int level, object val, ValueKind kind, out object result)
        {
            switch (kind)
            {
                case ValueKind.Null:
                    result = null; // Not converted. Don't trace again.
                    return true;

                case ValueKind.String:
                    if (string.IsNullOrEmpty(val as string))
                    {
                        result = null;
                        TraceValue(context, level, null);
                        return true;
                    }

                    break;
            }

            result = null;
            TraceCoercionFailed(context, level, fromKind: kind, toKind: ValueKind.Null);
            return false;
        }

        protected static bool TryConvertToNumber(EvaluationContext context, int level, object val, ValueKind kind, out decimal result)
        {
            switch (kind)
            {
                case ValueKind.Boolean:
                    result = (bool)val ? 1m : 0m;
                    TraceValue(context, level, result);
                    return true;

                case ValueKind.Number:
                    result = (decimal)val; // Not converted. Don't trace again.
                    return true;

                case ValueKind.Version:
                    result = default(decimal);
                    TraceCoercionFailed(context, level, fromKind: kind, toKind: ValueKind.Number);
                    return false;

                case ValueKind.String:
                    string s = val as string ?? string.Empty;
                    if (string.IsNullOrEmpty(s))
                    {
                        result = 0m;
                        TraceValue(context, level, result);
                        return true;
                    }

                    if (decimal.TryParse(s, NumberStyles, CultureInfo.InvariantCulture, out result))
                    {
                        TraceValue(context, level, result);
                        return true;
                    }

                    TraceCoercionFailed(context, level, fromKind: kind, toKind: ValueKind.Number);
                    return false;

                case ValueKind.Array:
                case ValueKind.Object:
                    result = default(decimal);
                    TraceCoercionFailed(context, level, fromKind: kind, toKind: ValueKind.Number);
                    return false;

                case ValueKind.Null:
                    result = 0m;
                    TraceValue(context, level, result);
                    return true;

                default:
                    throw new NotSupportedException($"Unable to determine whether value can be converted to Number. Unexpected value kind '{kind}'.");
            }
        }

        protected static bool TryConvertToString(EvaluationContext context, int level, object val, ValueKind kind, out string result)
        {
            switch (kind)
            {
                case ValueKind.Boolean:
                    result = string.Format(CultureInfo.InvariantCulture, "{0}", val);
                    TraceValue(context, level, result);
                    return true;

                case ValueKind.Number:
                    result = ((decimal)val).ToString("G", CultureInfo.InvariantCulture);
                    if (result.Contains("."))
                    {
                        result = result.TrimEnd('0').TrimEnd('.'); // Omit trailing zeros after the decimal point.
                    }

                    TraceValue(context, level, result);
                    return true;

                case ValueKind.String:
                    result = val as string; // Not converted. Don't trace again.
                    return true;

                case ValueKind.Version:
                    Version v = val as Version;
                    result = v.ToString();
                    TraceValue(context, level, result);
                    return true;

                case ValueKind.Null:
                    result = string.Empty;
                    TraceValue(context, level, result);
                    return true;

                case ValueKind.Array:
                case ValueKind.Object:
                    result = null;
                    TraceCoercionFailed(context, level, fromKind: kind, toKind: ValueKind.String);
                    return false;

                default:
                    throw new NotSupportedException($"Unable to convert to String. Unexpected value kind '{kind}'.");
            }
        }

        protected static bool TryConvertToVersion(EvaluationContext context, int level, object val, ValueKind kind, out Version result)
        {
            switch (kind)
            {
                case ValueKind.Boolean:
                    result = null;
                    TraceCoercionFailed(context, level, fromKind: kind, toKind: ValueKind.Version);
                    return false;

                case ValueKind.Number:
                    if (Version.TryParse(string.Format(CultureInfo.InvariantCulture, "{0}", val), out result))
                    {
                        TraceValue(context, level, result);
                        return true;
                    }

                    TraceCoercionFailed(context, level, fromKind: kind, toKind: ValueKind.Version);
                    return false;

                case ValueKind.Version:
                    result = val as Version; // Not converted. Don't trace again.
                    return true;

                case ValueKind.String:
                    string s = val as string ?? string.Empty;
                    if (Version.TryParse(s, out result))
                    {
                        TraceValue(context, level, result);
                        return true;
                    }

                    TraceCoercionFailed(context, level, fromKind: kind, toKind: ValueKind.Version);
                    return false;

                case ValueKind.Array:
                case ValueKind.Object:
                case ValueKind.Null:
                    result = null;
                    TraceCoercionFailed(context, level, fromKind: kind, toKind: ValueKind.Version);
                    return false;

                default:
                    throw new NotSupportedException($"Unable to convert to Version. Unexpected value kind '{kind}'.");
            }
        }

        // protected bool TryGetValueAsVersion(EvaluationContext context, out Version result)
        // {
        //     ValueKind kind;
        //     object val = GetCanonicalValue(context, out kind);
        //     return TryConvertToVersion(context, val, kind, out result);
        // }

        protected static void TraceValue(EvaluationContext context, int level, object val)
        {
            TraceValue(context, level, val, isLiteral: false);
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

        private static void TraceCoercionFailed(EvaluationContext context, int level, ValueKind fromKind, ValueKind toKind)
        {
            TraceVerbose(context, level, string.Format(CultureInfo.InvariantCulture, "=> Unable to coerce {0} to {1}.", fromKind, toKind));
        }
    }

    public class LeafNode : Node
    {
        public LeafNode(object val)
        {
            Value = val;
        }

        public object Value { get; }

        protected sealed override object Evaluate(EvaluationContext context)
        {
            TraceValue(context, Level, Value, isLiteral: true);
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
        protected sealed override object Evaluate(EvaluationContext context)
        {
            TraceVerbose(context, Level, $"Indexer");
            object result = null;
            ValueKind itemKind;
            object item = Parameters[0].EvaluateCanonical(context, out itemKind);
            if (itemKind == ValueKind.Array && item is JArray)
            {
                var jarray = item as JArray;
                ValueKind indexKind;
                object index = Parameters[1].EvaluateCanonical(context, out indexKind);
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
                    if (TryConvertToNumber(context, Parameters[1].Level, index, indexKind, out d))
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
                ValueKind indexKind;
                object index = Parameters[1].EvaluateCanonical(context, out indexKind);
                string s;
                if (TryConvertToString(context, Parameters[1].Level, index, indexKind, out s))
                {
                    result = jobject[s];
                }
            }

            TraceValue(context, Level, result);
            return result;
        }
    }

    public abstract class FunctionNode : ContainerNode
    {
        protected abstract string Name { get; }

        protected void TraceName(EvaluationContext context)
        {
            TraceVerbose(context, Level, $"Function: {Name}");
        }
    }

    public sealed class AndNode : FunctionNode
    {
        protected sealed override string Name => "and";

        protected sealed override object Evaluate(EvaluationContext context)
        {
            TraceName(context);
            bool result = true;
            foreach (Node parameter in Parameters)
            {
                if (!parameter.EvaluateBoolean(context))
                {
                    result = false;
                    break;
                }
            }

            TraceValue(context, Level, result);
            return result;
        }
    }

    internal sealed class ContainsNode : FunctionNode
    {
        protected sealed override string Name => "contains";

        protected sealed override object Evaluate(EvaluationContext context)
        {
            TraceName(context);
            string left = Parameters[0].EvaluateString(context) ?? string.Empty;
            string right = Parameters[1].EvaluateString(context) ?? string.Empty;
            bool result = left.IndexOf(right, StringComparison.OrdinalIgnoreCase) >= 0;
            TraceValue(context, Level, result);
            return result;
        }
    }

    internal sealed class EndsWithNode : FunctionNode
    {
        protected sealed override string Name => "endsWith";

        protected sealed override object Evaluate(EvaluationContext context)
        {
            TraceName(context);
            string left = Parameters[0].EvaluateString(context) ?? string.Empty;
            string right = Parameters[1].EvaluateString(context) ?? string.Empty;
            bool result = left.EndsWith(right, StringComparison.OrdinalIgnoreCase);
            TraceValue(context, Level, result);
            return result;
        }
    }

    internal sealed class EqualNode : FunctionNode
    {
        protected sealed override string Name => "equal";

        protected sealed override object Evaluate(EvaluationContext context)
        {
            TraceName(context);
            bool result = Equals(context, Parameters[0].Level, Parameters[0], Parameters[1]);
            TraceValue(context, Level, result);
            return result;
        }
    }

    internal sealed class GreaterThanNode : FunctionNode
    {
        protected sealed override string Name => "greaterThan";

        protected sealed override object Evaluate(EvaluationContext context)
        {
            TraceName(context);
            bool result = Compare(context, Parameters[0].Level, Parameters[0], Parameters[1]) > 0;
            TraceValue(context, Level, result);
            return result;
        }
    }

    internal sealed class GreaterThanOrEqualNode : FunctionNode
    {
        protected sealed override string Name => "greaterThanOrEqual";

        protected sealed override object Evaluate(EvaluationContext context)
        {
            TraceName(context);
            bool result = Compare(context, Parameters[0].Level, Parameters[0], Parameters[1]) >= 0;
            TraceValue(context, Level, result);
            return result;
        }
    }

    internal sealed class InNode : FunctionNode
    {
        protected sealed override string Name => "in";

        protected sealed override object Evaluate(EvaluationContext context)
        {
            TraceName(context);
            bool result = false;
            ValueKind leftKind;
            object left = Parameters[0].EvaluateCanonical(context, out leftKind);
            for (int i = 1; i < Parameters.Count; i++)
            {
                ValueKind rightKind;
                object right = Parameters[i].EvaluateCanonical(context, out rightKind);
                result = Equals(context, Parameters[0].Level, left, leftKind, right, rightKind);
                if (result)
                {
                    break;
                }
            }

            TraceValue(context, Level, result);
            return result;
        }
    }

    internal sealed class LessThanNode : FunctionNode
    {
        protected sealed override string Name => "lessThan";

        protected sealed override object Evaluate(EvaluationContext context)
        {
            TraceName(context);
            bool result = Compare(context, Parameters[0].Level, Parameters[0], Parameters[1]) < 0;
            TraceValue(context, Level, result);
            return result;
        }
    }

    internal sealed class LessThanOrEqualNode : FunctionNode
    {
        protected sealed override string Name => "lessThanOrEqual";

        protected sealed override object Evaluate(EvaluationContext context)
        {
            TraceName(context);
            bool result = Compare(context, Parameters[0].Level, Parameters[0], Parameters[1]) <= 0;
            TraceValue(context, Level, result);
            return result;
        }
    }

    internal sealed class NotEqualNode : FunctionNode
    {
        protected sealed override string Name => "notEqual";

        protected sealed override object Evaluate(EvaluationContext context)
        {
            TraceName(context);
            bool result = !Equals(context, Parameters[0].Level, Parameters[0], Parameters[1]);
            TraceValue(context, Level, result);
            return result;
        }
    }

    internal sealed class NotNode : FunctionNode
    {
        protected sealed override string Name => "not";

        protected sealed override object Evaluate(EvaluationContext context)
        {
            TraceName(context);
            bool result = !Parameters[0].EvaluateBoolean(context);
            TraceValue(context, Level, result);
            return result;
        }
    }

    internal sealed class NotInNode : FunctionNode
    {
        protected sealed override string Name => "notIn";

        protected sealed override object Evaluate(EvaluationContext context)
        {
            TraceName(context);
            bool found = false;
            ValueKind leftKind;
            object left = Parameters[0].EvaluateCanonical(context, out leftKind);
            for (int i = 1; i < Parameters.Count; i++)
            {
                ValueKind rightKind;
                object right = Parameters[1].EvaluateCanonical(context, out rightKind);
                found = Equals(context, Parameters[0].Level, left, leftKind, right, rightKind);
                if (found)
                {
                    break;
                }
            }

            bool result = !found;
            TraceValue(context, Level, result);
            return result;
        }
    }

    internal sealed class OrNode : FunctionNode
    {
        protected sealed override string Name => "or";

        protected sealed override object Evaluate(EvaluationContext context)
        {
            TraceName(context);
            bool result = false;
            foreach (Node parameter in Parameters)
            {
                if (parameter.EvaluateBoolean(context))
                {
                    result = true;
                    break;
                }
            }

            TraceValue(context, Level, result);
            return result;
        }
    }

    internal sealed class StartsWithNode : FunctionNode
    {
        protected sealed override string Name => "startsWith";

        protected sealed override object Evaluate(EvaluationContext context)
        {
            TraceName(context);
            string left = Parameters[0].EvaluateString(context) ?? string.Empty;
            string right = Parameters[1].EvaluateString(context) ?? string.Empty;
            bool result = left.StartsWith(right, StringComparison.OrdinalIgnoreCase);
            TraceValue(context, Level, result);
            return result;
        }
    }

    internal sealed class XorNode : FunctionNode
    {
        protected sealed override string Name => "xor";

        protected sealed override object Evaluate(EvaluationContext context)
        {
            TraceName(context);
            bool result = Parameters[0].EvaluateBoolean(context) ^ Parameters[1].EvaluateBoolean(context);
            TraceValue(context, Level, result);
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