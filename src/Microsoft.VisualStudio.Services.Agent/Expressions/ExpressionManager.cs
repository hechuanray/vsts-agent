namespace Microsoft.VisualStudio.Services.Agent.Expressions
{
    public sealed class ExpressionManager
    {
        private readonly ITraceWriter _trace;

        public ExpressionManager(ITraceWriter traceWriter)
        {
            ArgUtil.NotNull(traceWriter, nameof(traceWriter));
            _trace = traceWriter;
        }

        public bool EvaluateCondition(string condition)
        {
            _trace.Verbose($"Entering {nameof(EvaluateCondition)}");
            var parser = new Parser(condition, _trace);
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