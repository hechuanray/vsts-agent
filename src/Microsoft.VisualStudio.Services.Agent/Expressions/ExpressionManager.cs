using System.Collections.Generic;

namespace Microsoft.VisualStudio.Services.Agent.Expressions
{
    public sealed class ExpressionManager
    {
        private readonly ITraceWriter _trace;
        private readonly IDictionary<string, object> _extensionObjects;

        public ExpressionManager(ITraceWriter traceWriter, IDictionary<string, object> extensionObjects)
        {
            ArgUtil.NotNull(traceWriter, nameof(traceWriter));
            _trace = traceWriter;
            _extensionObjects = extensionObjects ?? new Dictionary<string, object>();
        }

        public bool EvaluateCondition(string condition)
        {
            _trace.Verbose($"Entering {nameof(EvaluateCondition)}");
            var parser = new Parser(condition, _trace, _extensionObjects);
            Node root = parser.Root;
            bool result = root != null ? root.GetValueAsBool() : true;
            _trace.Info($"Condition result: {result}");
            return result;
        }
    }

    public interface ITraceWriter
    {
        void Info(string message);
        void Verbose(string message);
    }
}