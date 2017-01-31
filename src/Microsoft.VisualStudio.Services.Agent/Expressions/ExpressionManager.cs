using System.Collections.Generic;

namespace Microsoft.VisualStudio.Services.Agent.Expressions
{
    /*
succeeded()
succeededOrFailed()
always()
 
SomeComplexobject('firstlevelobject')['foo']['bar']
 
variables(‘nosuch’).does.not[‘null ref’]
 
camelCase
 
write debug to build-log (task-level)
    */
    // public sealed class ExpressionManager
    // {
    //     public Node Parse(string condition, ITraceWriter trace, IEnumerable<string> extensionNames)
    //     {
    //         return new Parser(condition, trace, extensionNames).Root;
    //     }

    //     public bool EvaluateCondition(Node tree, ITraceWriter trace, IDictionary<string, object> extensions)
    //     {
    //         var context = new EvaluationContext(trace, extensions);
    //         bool result = tree != null ? tree.GetValueAsBool(context) : true;
    //         trace.Verbose($"Condition result: {result}");
    //         return result;
    //     }
    // }
}