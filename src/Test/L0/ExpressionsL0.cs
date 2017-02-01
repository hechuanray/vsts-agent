using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Services.DistributedTask.Expressions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.VisualStudio.Services.Agent.Tests
{
    public sealed class ExpressionsL0
    {
        ////////////////////////////////////////////////////////////////////////////////
        // Type-cast rules
        ////////////////////////////////////////////////////////////////////////////////
        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void CastsToBoolean()
        {
            using (var hc = new TestHostContext(this))
            {
                Tracing trace = hc.GetTrace(nameof(CastsToBoolean));

                // Boolean
                trace.Info($"****************************************");
                trace.Info($"From Boolean");
                trace.Info($"****************************************");
                Assert.Equal(true, EvaluateAsBoolean(hc, "true"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "TRUE"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "false"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "FALSE"));

                // Number
                trace.Info($"****************************************");
                trace.Info($"From Number");
                trace.Info($"****************************************");
                Assert.Equal(true, EvaluateAsBoolean(hc, "1"));
                Assert.Equal(true, EvaluateAsBoolean(hc, ".5"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "0.5"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "2"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "-1"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "-.5"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "-0.5"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "-2"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "0"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "0.0"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "-0"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "-0.0"));

                // String
                trace.Info($"****************************************");
                trace.Info($"From String");
                trace.Info($"****************************************");
                Assert.Equal(true, EvaluateAsBoolean(hc, "'a'"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "'false'"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "'0'"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "' '"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "''"));

                // Version
                trace.Info($"****************************************");
                trace.Info($"From Version");
                trace.Info($"****************************************");
                Assert.Equal(true, EvaluateAsBoolean(hc, "1.2.3"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "1.2.3.4"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "0.0.0"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "0.0.0"));

                // Objects/Arrays
                trace.Info($"****************************************");
                trace.Info($"From Objects/Arrays");
                trace.Info($"****************************************");
                Assert.Equal(true, EvaluateAsBoolean(hc, "testData()", new IExtensionInfo[] { new ExtensionInfo<TestDataNode>("TestData", 0, 0) }, new object()));
                Assert.Equal(true, EvaluateAsBoolean(hc, "testData()", new IExtensionInfo[] { new ExtensionInfo<TestDataNode>("TestData", 0, 0) }, new object[0]));
                Assert.Equal(true, EvaluateAsBoolean(hc, "testData()", new IExtensionInfo[] { new ExtensionInfo<TestDataNode>("TestData", 0, 0) }, new int[0]));
                Assert.Equal(true, EvaluateAsBoolean(hc, "testData()", new IExtensionInfo[] { new ExtensionInfo<TestDataNode>("TestData", 0, 0) }, new Dictionary<string, object>()));
                Assert.Equal(true, EvaluateAsBoolean(hc, "testData()", new IExtensionInfo[] { new ExtensionInfo<TestDataNode>("TestData", 0, 0) }, new JArray()));
                Assert.Equal(true, EvaluateAsBoolean(hc, "testData()", new IExtensionInfo[] { new ExtensionInfo<TestDataNode>("TestData", 0, 0) }, new JObject()));

                // Null
                trace.Info($"****************************************");
                trace.Info($"From Null");
                trace.Info($"****************************************");
                Assert.Equal(true, EvaluateAsBoolean(hc, "testData()", new IExtensionInfo[] { new ExtensionInfo<TestDataNode>("TestData", 0, 0) }, null));
            }
        }

        private sealed class TestDataNode : FunctionNode
        {
            protected override string Name => "TestData";

            public override object GetValue(EvaluationContext context)
            {
                TraceName(context);
                object result;
                if (Parameters.Count == 0)
                {
                    result = context.State;
                }
                else
                {
                    string key = string.Join(",", Parameters.Select(x => x.GetValueAsString(context)));
                    var dictionary = context.State as IDictionary<string, object>;
                    result = dictionary[key];
                }

                TraceValue(context, result);
                return result;
            }
        }

        ////////////////////////////////////////////////////////////////////////////////
        // Functions
        ////////////////////////////////////////////////////////////////////////////////
        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void EvaluatesAnd()
        {
            using (var hc = new TestHostContext(this))
            {
                Assert.Equal(true, EvaluateAsBoolean(hc, "and(true, true, true)")); // bool
                Assert.Equal(true, EvaluateAsBoolean(hc, "and(true, true)"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "and(true, true, false)"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "and(true, false)"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "and(false, true)"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "and(false, false)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "and(true, 1)")); // number
                Assert.Equal(false, EvaluateAsBoolean(hc, "and(true, 0)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "and(true, 'a')")); // string
                Assert.Equal(false, EvaluateAsBoolean(hc, "and(true, '')"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "and(true, 0.0.0.0)")); // version
                Assert.Equal(true, EvaluateAsBoolean(hc, "and(true, 1.2.3.4)"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void AndShortCircuitsAndAfterFirstFalse()
        {
            using (var hc = new TestHostContext(this))
            {
                // The gt function should never evaluate. It would would throw since 'not a number'
                // cannot be converted to a number.
                Assert.Equal(false, EvaluateAsBoolean(hc, "and(false, gt(1, 'not a number'))"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void EvaluatesEqual()
        {
            using (var hc = new TestHostContext(this))
            {
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq(true, true)")); // bool
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq(false, false)"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "eq(false, true)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq(2, 2)")); // number
                Assert.Equal(false, EvaluateAsBoolean(hc, "eq(1, 2)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq('abcDEF', 'ABCdef')")); // string
                Assert.Equal(false, EvaluateAsBoolean(hc, "eq('a', 'b')"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq(1.2.3, 1.2.3)")); // version
                Assert.Equal(false, EvaluateAsBoolean(hc, "eq(1.2.3, 1.2.3.0)"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "eq(1.2.3, 4.5.6)"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void EqualCastsToMatchLeftSide()
        {
            using (var hc = new TestHostContext(this))
            {
                // Cast to bool.
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq(true, 2)")); // number
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq(false, 0)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq(true, 'a')")); // string
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq(true, ' ')"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq(false, '')"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq(true, 1.2.3)")); // version
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq(true, 0.0.0)"));

                // Cast to string.
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq('TRue', true)")); // bool
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq('FALse', false)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq('123456.789', 123456.789)")); // number
                Assert.Equal(false, EvaluateAsBoolean(hc, "eq('123456.000', 123456.000)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq('1.2.3', 1.2.3)")); // version

                // Cast to number (best effort).
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq(1, true)")); // bool
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq(0, false)"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "eq(2, true)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq(123456.789, ' +123,456.7890 ')")); // string
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq(-123456.789, ' -123,456.7890 ')"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq(123000, ' 123,000.000 ')"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq(0, '')"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "eq(1, 'not a number')"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "eq(0, 'not a number')"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "eq(1.2, 1.2.0.0)")); // version

                // Cast to version (best effort).
                Assert.Equal(false, EvaluateAsBoolean(hc, "eq(1.2.3, false)")); // bool
                Assert.Equal(false, EvaluateAsBoolean(hc, "eq(1.2.3, true)"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "eq(1.2.0, 1.2)")); // number
                Assert.Equal(true, EvaluateAsBoolean(hc, "eq(1.2.0, ' 1.2.0 ')")); // string
                Assert.Equal(false, EvaluateAsBoolean(hc, "eq(1.2.0, '1.2')"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void EvaluatesGreaterThan()
        {
            using (var hc = new TestHostContext(this))
            {
                Assert.Equal(true, EvaluateAsBoolean(hc, "gt(true, false)")); // bool
                Assert.Equal(false, EvaluateAsBoolean(hc, "gt(true, true)"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "gt(false, true)"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "gt(false, false)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "gt(2, 1)")); // number
                Assert.Equal(false, EvaluateAsBoolean(hc, "gt(1, 2)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "gt('DEF', 'abc')")); // string
                Assert.Equal(true, EvaluateAsBoolean(hc, "gt('def', 'ABC')"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "gt('a', 'b')"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "gt(4.5.6, 1.2.3)")); // version
                Assert.Equal(false, EvaluateAsBoolean(hc, "gt(1.2.3, 4.5.6)"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "gt(1.2.3, 1.2.3)"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void EvaluatesNot()
        {
            using (var hc = new TestHostContext(this))
            {
                Assert.Equal(true, EvaluateAsBoolean(hc, "not(false)")); // bool
                Assert.Equal(false, EvaluateAsBoolean(hc, "not(true)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "not(0)")); // number
                Assert.Equal(false, EvaluateAsBoolean(hc, "not(1)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "not('')")); // string
                Assert.Equal(false, EvaluateAsBoolean(hc, "not('a')"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "not(' ')"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void EvaluatesNotEqual()
        {
            using (var hc = new TestHostContext(this))
            {
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(false, true)")); // bool
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(true, false)"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "ne(false, false)"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "ne(true, true)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(1, 2)")); // number
                Assert.Equal(false, EvaluateAsBoolean(hc, "ne(2, 2)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne('abc', 'def')")); // string
                Assert.Equal(false, EvaluateAsBoolean(hc, "ne('abcDEF', 'ABCdef')"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(1.2.3, 1.2.3.0)")); // version
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(1.2.3, 4.5.6)"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "ne(1.2.3, 1.2.3)"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void NotEqualCastsToMatchLeftSide()
        {
            using (var hc = new TestHostContext(this))
            {
                // Cast to bool.
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(false, 2)")); // number
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(true, 0)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(false, 'a')")); // string
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(false, ' ')"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(true, '')"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(false, 1.2.3)")); // version
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(false, 0.0.0)"));

                // Cast to string.
                Assert.Equal(false, EvaluateAsBoolean(hc, "ne('TRue', true)")); // bool
                Assert.Equal(false, EvaluateAsBoolean(hc, "ne('FALse', false)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne('123456.000', 123456.000)")); // number
                Assert.Equal(false, EvaluateAsBoolean(hc, "ne('123456.789', 123456.789)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne('1.2.3.0', 1.2.3)")); // version
                Assert.Equal(false, EvaluateAsBoolean(hc, "ne('1.2.3', 1.2.3)"));

                // Cast to number (best effort).
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(2, true)")); // bool
                Assert.Equal(false, EvaluateAsBoolean(hc, "ne(1, true)"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "ne(0, false)"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "ne(123456.789, ' +123,456.7890 ')")); // string
                Assert.Equal(false, EvaluateAsBoolean(hc, "ne(-123456.789, ' -123,456.7890 ')"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "ne(123000, ' 123,000.000 ')"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "ne(0, '')"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(1, 'not a number')"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(0, 'not a number')"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(1.2, 1.2.0.0)")); // version

                // Cast to version (best effort).
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(1.2.3, false)")); // bool
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(1.2.3, true)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(1.2.0, 1.2)")); // number
                Assert.Equal(false, EvaluateAsBoolean(hc, "ne(1.2.0, ' 1.2.0 ')")); // string
                Assert.Equal(true, EvaluateAsBoolean(hc, "ne(1.2.0, '1.2')"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void EvaluatesOr()
        {
            using (var hc = new TestHostContext(this))
            {
                Assert.Equal(true, EvaluateAsBoolean(hc, "or(false, false, true)")); // bool
                Assert.Equal(true, EvaluateAsBoolean(hc, "or(false, true, false)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "or(true, false, false)"));
                Assert.Equal(false, EvaluateAsBoolean(hc, "or(false, false, false)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "or(false, 1)")); // number
                Assert.Equal(false, EvaluateAsBoolean(hc, "or(false, 0)"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "or(false, 'a')")); // string
                Assert.Equal(false, EvaluateAsBoolean(hc, "or(false, '')"));
                Assert.Equal(true, EvaluateAsBoolean(hc, "or(false, 1.2.3)")); // version
                Assert.Equal(true, EvaluateAsBoolean(hc, "or(false, 0.0.0)"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void ShortCircuitsOrAfterFirstTrue()
        {
            using (var hc = new TestHostContext(this))
            {
                // The gt function should never evaluate. It would would throw since 'not a number'
                // cannot be converted to a number.
                Assert.Equal(true, EvaluateAsBoolean(hc, "or(true, gt(1, 'not a number'))"));
            }
        }

        ////////////////////////////////////////////////////////////////////////////////
        // Extension functions
        ////////////////////////////////////////////////////////////////////////////////
        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void ExtensionReceivesState()
        {
            using (var hc = new TestHostContext(this))
            {
                try
                {
                    EvaluateAsBoolean(hc, "eq(1.2, 3.4a)");
                }
                catch (ParseException ex)
                {
                    Assert.Equal(ParseExceptionKind.UnrecognizedValue, ex.Kind);
                    Assert.Equal("3.4a", ex.RawToken);
                }
            }
        }

        ////////////////////////////////////////////////////////////////////////////////
        // Parse exceptions
        ////////////////////////////////////////////////////////////////////////////////
        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void ThrowsWhenInvalidNumber()
        {
            using (var hc = new TestHostContext(this))
            {
                try
                {
                    EvaluateAsBoolean(hc, "eq(1.2, 3.4a)");
                }
                catch (ParseException ex)
                {
                    Assert.Equal(ParseExceptionKind.UnrecognizedValue, ex.Kind);
                    Assert.Equal("3.4a", ex.RawToken);
                }
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void ThrowsWhenInvalidVersion()
        {
            using (var hc = new TestHostContext(this))
            {
                try
                {
                    EvaluateAsBoolean(hc, "eq(1.2.3, 4.5.6.7a)");
                }
                catch (ParseException ex)
                {
                    Assert.Equal(ParseExceptionKind.UnrecognizedValue, ex.Kind);
                    Assert.Equal("4.5.6.7a", ex.RawToken);
                }
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void ThrowsWhenInvalidString()
        {
            using (var hc = new TestHostContext(this))
            {
                try
                {
                    EvaluateAsBoolean(hc, "eq('hello', 'unterminated-string)");
                }
                catch (ParseException ex)
                {
                    Assert.Equal(ParseExceptionKind.UnrecognizedValue, ex.Kind);
                    Assert.Equal("'unterminated-string)", ex.RawToken);
                }
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void ThrowsWhenUnclosedFunction()
        {
            using (var hc = new TestHostContext(this))
            {
                try
                {
                    EvaluateAsBoolean(hc, "eq(1,2");
                }
                catch (ParseException ex)
                {
                    Assert.Equal(ParseExceptionKind.UnclosedFunction, ex.Kind);
                    Assert.Equal("eq", ex.RawToken);
                }
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void ThrowsWhenExpectedStartParameter()
        {
            using (var hc = new TestHostContext(this))
            {
                try
                {
                    EvaluateAsBoolean(hc, "not(eq 1,2)");
                }
                catch (ParseException ex)
                {
                    Assert.Equal(ParseExceptionKind.ExpectedStartParameter, ex.Kind);
                    Assert.Equal("eq", ex.RawToken);
                }
            }
        }

        private static bool EvaluateAsBoolean(IHostContext hostContext, string expression, IEnumerable<IExtensionInfo> extensions = null, object state = null)
        {
            var parser = new Parser();
            Node node = parser.CreateTree(expression, new TraceWriter(hostContext), extensions);
            var evaluationContext = new EvaluationContext(new TraceWriter(hostContext), state);
            return node.GetValueAsBoolean(evaluationContext);
        }

        private sealed class TraceWriter : ITraceWriter
        {
            private readonly IHostContext _context;
            private readonly Tracing _trace;

            public TraceWriter(IHostContext context)
            {
                _context = context;
                _trace = context.GetTrace("ExpressionManager");
            }

            public void Info(string message)
            {
                _trace.Info(message);
            }

            public void Verbose(string message)
            {
                _trace.Verbose(message);
            }
        }
    }
}
