using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Services.Agent.Util;
using DTExpressions = Microsoft.VisualStudio.Services.DistributedTask.Expressions;

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
            var parser = new DTExpressions.Parser();
            var extensions = new DTExpressions.IExtensionInfo[]
            {
                new DTExpressions.ExtensionInfo<AlwaysNode>(name: Constants.Expressions.Always, minParameters: 0, maxParameters: 0),
                new DTExpressions.ExtensionInfo<SucceededNode>(name: Constants.Expressions.Succeeded, minParameters: 0, maxParameters: 0),
                new DTExpressions.ExtensionInfo<SucceededOrFailedNode>(name: Constants.Expressions.SucceededOrFailed, minParameters: 0, maxParameters: 0),
                new DTExpressions.ExtensionInfo<VariablesNode>(name: Constants.Expressions.Variables, minParameters: 1, maxParameters: 1),
            };
            DTExpressions.Node tree = parser.CreateTree(condition, expressionTrace, extensions) ?? new SucceededNode();

            // // Test whether Agent.JobStatus is referenced.
            // bool referencesJobStatus = false;
            // var nodes = new Stack<DTExpressions.Node>();
            // nodes.Push(tree);
            // while (nodes.Count > 0)
            // {
            //     DTExpressions.Node node = nodes.Pop();
            //     if (node is AlwaysNode || node is SucceededNode || node is SucceededOrFailedNode)
            //     {
            //         referencesJobStatus = true;
            //         break;
            //     }
            //     else if (node is VariablesNode)
            //     {
            //         var variablesNode = node as VariablesNode;
            //         var leafParameter = variablesNode.Parameters[0] as DTExpressions.LeafNode;
            //         if (leafParameter != null && string.Equals(leafParameter.Value as string, "Agent.JobStatus", StringComparison.OrdinalIgnoreCase))
            //         {
            //             referencesJobStatus = true;
            //             break;
            //         }
            //     }

            //     // Push parameters.
            //     if (node is DTExpressions.ContainerNode)
            //     {
            //         foreach (DTExpressions.Node parameter in (node as DTExpressions.ContainerNode).Parameters)
            //         {
            //             nodes.Push(parameter);
            //         }
            //     }
            // }

            // // Wrap with "and(succeeded(), ...)" if Agent.JobStatus not referenced.
            // if (!referencesJobStatus)
            // {
            //     executionContext.Debug("Agent.JobStatus not refenced. Wrapping expression tree with 'and(succeeded(), ...)'");
            //     var newTree = new DTExpressions.AndNode();
            //     newTree.AddParameter(new SucceededNode());
            //     newTree.AddParameter(tree);
            //     tree = newTree;
            // }

            // Evaluate the tree.
            var evaluationContext = new DTExpressions.EvaluationContext(expressionTrace, state: executionContext);
            return tree.GetValueAsBoolean(evaluationContext);
        }

        public sealed class TraceWriter : DTExpressions.ITraceWriter
        {
            private readonly IExecutionContext _executionContext;

            public TraceWriter(IExecutionContext executionContext)
            {
                ArgUtil.NotNull(executionContext, nameof(executionContext));
                _executionContext = executionContext;
            }

            public void Info(string message)
            {
                _executionContext.Output(message);
            }

            public void Verbose(string message)
            {
                _executionContext.Debug(message);
            }
        }

        public sealed class AlwaysNode : DTExpressions.FunctionNode
        {
            protected sealed override string Name => Constants.Expressions.Always;

            protected sealed override object GetValue(DTExpressions.EvaluationContext evaluationContext)
            {
                throw new System.NotImplementedException();
            }
        }

        public sealed class SucceededNode : DTExpressions.FunctionNode
        {
            protected sealed override string Name => Constants.Expressions.Succeeded;

            protected sealed override object GetValue(DTExpressions.EvaluationContext evaluationContext)
            {
                throw new System.NotImplementedException();
            }
        }

        public sealed class SucceededOrFailedNode : DTExpressions.FunctionNode
        {
            protected sealed override string Name => Constants.Expressions.SucceededOrFailed;

            protected sealed override object GetValue(DTExpressions.EvaluationContext evaluationContext)
            {
                throw new System.NotImplementedException();
            }
        }

        public sealed class VariablesNode : DTExpressions.FunctionNode
        {
            protected sealed override string Name => Constants.Expressions.Variables;

            protected sealed override object GetValue(DTExpressions.EvaluationContext evaluationContext)
            {
                TraceName(evaluationContext);
                var executionContext = evaluationContext.State as IExecutionContext;
                ArgUtil.NotNull(executionContext, nameof(executionContext));
                string variableName = Parameters[0].GetValueAsString(evaluationContext);
                string result = executionContext.Variables.Get(variableName) ?? string.Empty;
                TraceValue(evaluationContext, result);
                return result;
            }
        }
    }
}