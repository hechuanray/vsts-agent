using Microsoft.VisualStudio.Services.Agent.Util;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.VisualStudio.Services.DistributedTask.Expressions
{
    public sealed class EvaluationContext
    {
        public EvaluationContext(ITraceWriter trace, object state)
        {
            ArgUtil.NotNull(trace, nameof(trace));
            Trace = trace;
            State = state;
        }

        public object State { get; }

        public ITraceWriter Trace { get; }
    }

    public sealed class ExtensionInfo<T> where T : FunctionNode, new()
    {
        public ExtensionInfo(string name, int minParameters, int maxParameters)
        {
            Name = name;
            MinParameters = minParameters;
            MaxParameters = maxParameters;
        }

        public string Name { get; }

        public int MinParameters { get; }

        public int MaxParameters { get; }

        internal T Create()
        {
            return new T();
        }
    }

    public interface ITraceWriter
    {
        void Info(string message);
        void Verbose(string message);
    }
}