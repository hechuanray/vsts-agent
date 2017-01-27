namespace Microsoft.VisualStudio.Services.Agent.Expressions
{
    public sealed class ExpressionManager
    {
        private readonly IHostContext _context;
        private readonly Tracing _trace;

        public ExpressionManager(IHostContext context)
        {
            ArgUtil.NotNull(context, nameof(context));
            _context = context;
            _trace = _context.GetTrace(nameof(ExpressionManager));
        }

        public bool EvaluateCondition(string condition)
        {
            _trace.Entering();
            var parser = new Parser(_context, condition);
            Node root = parser.Root;
            bool result = root != null ? root.GetValueAsBool() : true;
            _trace.Info($"Result: {result}");
            return result;
        }
    }
}