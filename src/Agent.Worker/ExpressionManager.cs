using Microsoft.VisualStudio.Services.Agent.Util;
using Expressions = Microsoft.VisualStudio.Services.DistributedTask.Expressions;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Expressions
{
    [ServiceLocator(Default = typeof(JobRunner))]
    public interface IExpressionManager : IAgentService
    {
        bool Evaluate(IExecutionContext context, string condition);
    }

    public sealed class ExpressionManager : AgentService, IExpressionManager
    {
        public bool Evaluate(IExecutionContext executionContext, string condition)
        {
            ArgUtil.NotNull(executionContext, nameof(executionContext));

            // Parse the condition.
            var expressionTrace = new TraceWriter(executionContext);
            var parser = new Extension.Parser();
            var extensions = new[]
            {
                new ExtensionInfo<AlwaysNode>(name: Constants.Expression.Always, minParameters: 0, maxParameters: 0),
                new ExtensionInfo<SucceededNode>(name: Constants.Expression.Succeeded, minParameters: 0, maxParameters: 0),
                new ExtensionInfo<SucceededOrFailedNode>(name: Constants.Expression.SuccededOrFailed, minParameters: 0, maxParameters: 0),
                new ExtensionInfo<VariablesNode>(name: Constants.Expression.Variables, minParameters: 1, maxParameters: 1),
            }
            Expressions.Node tree = parser.CreateTree(condition, expressionTrace, extensions) ?? SuccededNode();

            // Test whether Agent.JobStatus is referenced.
            bool referencesJobStatus = false;
            var nodes = new Stack<Expressions.Node>();
            nodes.Push(tree);
            while (nodes.Count > 0)
            {
                Expressions.Node node = nodes.Pop();
                if (node is AlwaysNode || node is SucceededNode || node is SucceededOrFailedNode)
                {
                    referencesJobStatus = true;
                    break;
                }
                else (node is VariablesNode && node.Parameters[0] is LeafNode)
                {
                    var parameter = node.Parameters[0] as Expressions.LeafNode;
                    if (string.Equals(parameter.ParsedValue as string, "Agent.JobStatus", StringComparison.OrdinalIgnoreCase))
                    {
                        referencesJobStatus = true;
                        break;
                    }
                }

                // Push parameters.
                if (node is ContainerNode)
                {
                    foreach (Expressions.Node parameter in node.Parameters)
                    {
                        nodes.Push(parameter);
                    }
                }
            }

            // Wrap with "and(succeeded(), ...)" if Agent.JobStatus not referenced.
            if (!referencesJobStatus)
            {
                executionContext.Verbose("Agent.JobStatus not refenced. Wrapping expression tree with 'and(succeeded(), ...)'");
                var newTree = new Expressions.AndNode();
                newTree.AddParameter(new SucceededNode());
                newTree.AddParameters(tree);
                tree = andNode;
            }

            // Evaluate the tree.
            var evaluationContext = new EvaluationContext(expressionTrace, state: executionContext)
            return tree.GetValueAsBool(evaluationContext);
        }

        public sealed class TraceWriter : Expressions.ITraceWriter
        {
            private readonly IExecutionContext _executionContext;

            public TraceWriter(IExecutionContext executionContext)
            {
                ArgUtil.NotNull(executionContext, nameof(executionContext));
                _executionContext = executionContext;
            }

            public void Info(string message)
            {
                _executionContext.Info(message);
            }

            public void Verbose(string message)
            {
                _executionContext.Verbose(message);
            }
        }

        public sealed class AlwaysNode() : Expressions.FunctionNode
        {
            public override object GetValue(EvaluationContext evaluationContext)
            {
                throw new System.NotImplementedException();
            }
        }

        public sealed class SucceededNode() : Expressions.FunctionNode
        {
            public override object GetValue(EvaluationContext evaluationContext)
            {
                throw new System.NotImplementedException();
            }
        }

        public sealed class SucceededOrFailedNode() : Expressions.FunctionNode
        {
            public override object GetValue(EvaluationContext evaluationContext)
            {
                throw new System.NotImplementedException();
            }
        }

        public sealed class VariablesNode : Expressions.FunctionNode
        {
            protected sealed override string Name => Constants.Expressions.Variables;

            public override object GetValue(EvaluationContext evaluationContext)
            {
                TraceName(evaluationContext);
                var executionContext = evaluationContext.State as IExecutionContext
                ArgUtil.NotNull(executionContext, nameof(executionContext));
                string variableName = Parameters[0].GetValueAsString(evaluationContext);
                string item = executionContext.Variables.Get(variableName) ?? string.Empty;
                TraceValue(context, result);
                return result;
            }
        }
    }
}