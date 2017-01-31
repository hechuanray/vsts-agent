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

        public ITraceWriter Trace { get; }

        public object State { get; }
    }

    public interface IExtensionInfo
    {
        string Name { get; }
        int MinParameters { get; }
        int MaxParameters { get; }
        FunctionNode CreateNode();
    }

    public sealed class ExtensionInfo<T> : IExtensionInfo
        where T : FunctionNode, new()
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

        public FunctionNode CreateNode()
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