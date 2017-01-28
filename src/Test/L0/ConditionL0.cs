using Microsoft.VisualStudio.Services.Agent.Expressions;
using Xunit;

namespace Microsoft.VisualStudio.Services.Agent.Tests
{
    public sealed class ConditionL0
    {
        ////////////////////////////////////////////////////////////////////////////////
        // Simple conditions
        ////////////////////////////////////////////////////////////////////////////////
        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void EvaluatesBool()
        {
            using (var hc = new TestHostContext(this))
            {
                Assert.Equal(true, EvaluateCondition(hc, "true"));
                Assert.Equal(true, EvaluateCondition(hc, "TRUE"));
                Assert.Equal(false, EvaluateCondition(hc, "false"));
                Assert.Equal(false, EvaluateCondition(hc, "FALSE"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void TreatsNumberAsTruthy()
        {
            using (var hc = new TestHostContext(this))
            {
                Assert.Equal(true, EvaluateCondition(hc, "1"));
                Assert.Equal(true, EvaluateCondition(hc, ".5"));
                Assert.Equal(true, EvaluateCondition(hc, "0.5"));
                Assert.Equal(true, EvaluateCondition(hc, "2"));
                Assert.Equal(true, EvaluateCondition(hc, "-1"));
                Assert.Equal(true, EvaluateCondition(hc, "-.5"));
                Assert.Equal(true, EvaluateCondition(hc, "-0.5"));
                Assert.Equal(true, EvaluateCondition(hc, "-2"));
                Assert.Equal(false, EvaluateCondition(hc, "0"));
                Assert.Equal(false, EvaluateCondition(hc, "0.0"));
                Assert.Equal(false, EvaluateCondition(hc, "-0"));
                Assert.Equal(false, EvaluateCondition(hc, "-0.0"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void TreatsStringAsTruthy()
        {
            using (var hc = new TestHostContext(this))
            {
                Assert.Equal(true, EvaluateCondition(hc, "'a'"));
                Assert.Equal(true, EvaluateCondition(hc, "'false'"));
                Assert.Equal(true, EvaluateCondition(hc, "'0'"));
                Assert.Equal(true, EvaluateCondition(hc, "' '"));
                Assert.Equal(false, EvaluateCondition(hc, "''"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void TreatsVersionAsTruthy()
        {
            using (var hc = new TestHostContext(this))
            {
                Assert.Equal(true, EvaluateCondition(hc, "1.2.3"));
                Assert.Equal(true, EvaluateCondition(hc, "1.2.3.4"));
                Assert.Equal(true, EvaluateCondition(hc, "0.0.0"));
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
                Assert.Equal(true, EvaluateCondition(hc, "and(true, true, true)")); // bool
                Assert.Equal(true, EvaluateCondition(hc, "and(true, true)"));
                Assert.Equal(false, EvaluateCondition(hc, "and(true, true, false)"));
                Assert.Equal(false, EvaluateCondition(hc, "and(true, false)"));
                Assert.Equal(false, EvaluateCondition(hc, "and(false, true)"));
                Assert.Equal(false, EvaluateCondition(hc, "and(false, false)"));
                Assert.Equal(true, EvaluateCondition(hc, "and(true, 1)")); // number
                Assert.Equal(false, EvaluateCondition(hc, "and(true, 0)"));
                Assert.Equal(true, EvaluateCondition(hc, "and(true, 'a')")); // string
                Assert.Equal(false, EvaluateCondition(hc, "and(true, '')"));
                Assert.Equal(true, EvaluateCondition(hc, "and(true, 0.0.0.0)")); // version
                Assert.Equal(true, EvaluateCondition(hc, "and(true, 1.2.3.4)"));
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
                Assert.Equal(false, EvaluateCondition(hc, "and(false, gt(1, 'not a number'))"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void EvaluatesEqual()
        {
            using (var hc = new TestHostContext(this))
            {
                Assert.Equal(true, EvaluateCondition(hc, "eq(true, true)")); // bool
                Assert.Equal(true, EvaluateCondition(hc, "eq(false, false)"));
                Assert.Equal(false, EvaluateCondition(hc, "eq(false, true)"));
                Assert.Equal(true, EvaluateCondition(hc, "eq(2, 2)")); // number
                Assert.Equal(false, EvaluateCondition(hc, "eq(1, 2)"));
                Assert.Equal(true, EvaluateCondition(hc, "eq('abcDEF', 'ABCdef')")); // string
                Assert.Equal(false, EvaluateCondition(hc, "eq('a', 'b')"));
                Assert.Equal(true, EvaluateCondition(hc, "eq(1.2.3, 1.2.3)")); // version
                Assert.Equal(false, EvaluateCondition(hc, "eq(1.2.3, 1.2.3.0)"));
                Assert.Equal(false, EvaluateCondition(hc, "eq(1.2.3, 4.5.6)"));
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
                Assert.Equal(true, EvaluateCondition(hc, "eq(true, 2)")); // number
                Assert.Equal(true, EvaluateCondition(hc, "eq(false, 0)"));
                Assert.Equal(true, EvaluateCondition(hc, "eq(true, 'a')")); // string
                Assert.Equal(true, EvaluateCondition(hc, "eq(true, ' ')"));
                Assert.Equal(true, EvaluateCondition(hc, "eq(false, '')"));
                Assert.Equal(true, EvaluateCondition(hc, "eq(true, 1.2.3)")); // version
                Assert.Equal(true, EvaluateCondition(hc, "eq(true, 0.0.0)"));

                // Cast to string.
                Assert.Equal(true, EvaluateCondition(hc, "eq('TRue', true)")); // bool
                Assert.Equal(true, EvaluateCondition(hc, "eq('FALse', false)"));
                Assert.Equal(true, EvaluateCondition(hc, "eq('123456.789', 123456.789)")); // number
                Assert.Equal(false, EvaluateCondition(hc, "eq('123456.000', 123456.000)"));
                Assert.Equal(true, EvaluateCondition(hc, "eq('1.2.3', 1.2.3)")); // version

                // Cast to number (best effort).
                Assert.Equal(true, EvaluateCondition(hc, "eq(1, true)")); // bool
                Assert.Equal(true, EvaluateCondition(hc, "eq(0, false)"));
                Assert.Equal(false, EvaluateCondition(hc, "eq(2, true)"));
                Assert.Equal(true, EvaluateCondition(hc, "eq(123456.789, ' +123,456.7890 ')")); // string
                Assert.Equal(true, EvaluateCondition(hc, "eq(-123456.789, ' -123,456.7890 ')"));
                Assert.Equal(true, EvaluateCondition(hc, "eq(123000, ' 123,000.000 ')"));
                Assert.Equal(false, EvaluateCondition(hc, "eq(1, 'not a number')"));
                Assert.Equal(false, EvaluateCondition(hc, "eq(0, 'not a number')"));
                Assert.Equal(false, EvaluateCondition(hc, "eq(1.2, 1.2.0.0)")); // version

                // Cast to version (best effort).
                Assert.Equal(false, EvaluateCondition(hc, "eq(1.2.3, false)")); // bool
                Assert.Equal(false, EvaluateCondition(hc, "eq(1.2.3, true)"));
                Assert.Equal(false, EvaluateCondition(hc, "eq(1.2.0, 1.2)")); // number
                Assert.Equal(true, EvaluateCondition(hc, "eq(1.2.0, ' 1.2.0 ')")); // string
                Assert.Equal(false, EvaluateCondition(hc, "eq(1.2.0, '1.2')"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void EvaluatesGreaterThan()
        {
            using (var hc = new TestHostContext(this))
            {
                Assert.Equal(true, EvaluateCondition(hc, "gt(true, false)")); // bool
                Assert.Equal(false, EvaluateCondition(hc, "gt(true, true)"));
                Assert.Equal(false, EvaluateCondition(hc, "gt(false, true)"));
                Assert.Equal(false, EvaluateCondition(hc, "gt(false, false)"));
                Assert.Equal(true, EvaluateCondition(hc, "gt(2, 1)")); // number
                Assert.Equal(false, EvaluateCondition(hc, "gt(1, 2)"));
                Assert.Equal(true, EvaluateCondition(hc, "gt('DEF', 'abc')")); // string
                Assert.Equal(true, EvaluateCondition(hc, "gt('def', 'ABC')"));
                Assert.Equal(false, EvaluateCondition(hc, "gt('a', 'b')"));
                Assert.Equal(true, EvaluateCondition(hc, "gt(4.5.6, 1.2.3)")); // version
                Assert.Equal(false, EvaluateCondition(hc, "gt(1.2.3, 4.5.6)"));
                Assert.Equal(false, EvaluateCondition(hc, "gt(1.2.3, 1.2.3)"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void EvaluatesNot()
        {
            using (var hc = new TestHostContext(this))
            {
                Assert.Equal(true, EvaluateCondition(hc, "not(false)")); // bool
                Assert.Equal(false, EvaluateCondition(hc, "not(true)"));
                Assert.Equal(true, EvaluateCondition(hc, "not(0)")); // number
                Assert.Equal(false, EvaluateCondition(hc, "not(1)"));
                Assert.Equal(true, EvaluateCondition(hc, "not('')")); // string
                Assert.Equal(false, EvaluateCondition(hc, "not('a')"));
                Assert.Equal(false, EvaluateCondition(hc, "not(' ')"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void EvaluatesNotEqual()
        {
            using (var hc = new TestHostContext(this))
            {
                Assert.Equal(true, EvaluateCondition(hc, "ne(false, true)")); // bool
                Assert.Equal(true, EvaluateCondition(hc, "ne(true, false)"));
                Assert.Equal(false, EvaluateCondition(hc, "ne(false, false)"));
                Assert.Equal(false, EvaluateCondition(hc, "ne(true, true)"));
                Assert.Equal(true, EvaluateCondition(hc, "ne(1, 2)")); // number
                Assert.Equal(false, EvaluateCondition(hc, "ne(2, 2)"));
                Assert.Equal(true, EvaluateCondition(hc, "ne('abc', 'def')")); // string
                Assert.Equal(false, EvaluateCondition(hc, "ne('abcDEF', 'ABCdef')"));
                Assert.Equal(true, EvaluateCondition(hc, "ne(1.2.3, 1.2.3.0)")); // version
                Assert.Equal(true, EvaluateCondition(hc, "ne(1.2.3, 4.5.6)"));
                Assert.Equal(false, EvaluateCondition(hc, "ne(1.2.3, 1.2.3)"));
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
                Assert.Equal(true, EvaluateCondition(hc, "ne(false, 2)")); // number
                Assert.Equal(true, EvaluateCondition(hc, "ne(true, 0)"));
                Assert.Equal(true, EvaluateCondition(hc, "ne(false, 'a')")); // string
                Assert.Equal(true, EvaluateCondition(hc, "ne(false, ' ')"));
                Assert.Equal(true, EvaluateCondition(hc, "ne(true, '')"));
                Assert.Equal(true, EvaluateCondition(hc, "ne(false, 1.2.3)")); // version
                Assert.Equal(true, EvaluateCondition(hc, "ne(false, 0.0.0)"));

                // Cast to string.
                Assert.Equal(false, EvaluateCondition(hc, "ne('TRue', true)")); // bool
                Assert.Equal(false, EvaluateCondition(hc, "ne('FALse', false)"));
                Assert.Equal(true, EvaluateCondition(hc, "ne('123456.000', 123456.000)")); // number
                Assert.Equal(false, EvaluateCondition(hc, "ne('123456.789', 123456.789)"));
                Assert.Equal(true, EvaluateCondition(hc, "ne('1.2.3.0', 1.2.3)")); // version
                Assert.Equal(false, EvaluateCondition(hc, "ne('1.2.3', 1.2.3)"));

                // Cast to number (best effort).
                Assert.Equal(true, EvaluateCondition(hc, "ne(2, true)")); // bool
                Assert.Equal(false, EvaluateCondition(hc, "ne(1, true)"));
                Assert.Equal(false, EvaluateCondition(hc, "ne(0, false)"));
                Assert.Equal(false, EvaluateCondition(hc, "ne(123456.789, ' +123,456.7890 ')")); // string
                Assert.Equal(false, EvaluateCondition(hc, "ne(-123456.789, ' -123,456.7890 ')"));
                Assert.Equal(false, EvaluateCondition(hc, "ne(123000, ' 123,000.000 ')"));
                Assert.Equal(true, EvaluateCondition(hc, "ne(1, 'not a number')"));
                Assert.Equal(true, EvaluateCondition(hc, "ne(0, 'not a number')"));
                Assert.Equal(true, EvaluateCondition(hc, "ne(1.2, 1.2.0.0)")); // version

                // Cast to version (best effort).
                Assert.Equal(true, EvaluateCondition(hc, "ne(1.2.3, false)")); // bool
                Assert.Equal(true, EvaluateCondition(hc, "ne(1.2.3, true)"));
                Assert.Equal(true, EvaluateCondition(hc, "ne(1.2.0, 1.2)")); // number
                Assert.Equal(false, EvaluateCondition(hc, "ne(1.2.0, ' 1.2.0 ')")); // string
                Assert.Equal(true, EvaluateCondition(hc, "ne(1.2.0, '1.2')"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Common")]
        public void EvaluatesOr()
        {
            using (var hc = new TestHostContext(this))
            {
                Assert.Equal(true, EvaluateCondition(hc, "or(false, false, true)")); // bool
                Assert.Equal(true, EvaluateCondition(hc, "or(false, true, false)"));
                Assert.Equal(true, EvaluateCondition(hc, "or(true, false, false)"));
                Assert.Equal(false, EvaluateCondition(hc, "or(false, false, false)"));
                Assert.Equal(true, EvaluateCondition(hc, "or(false, 1)")); // number
                Assert.Equal(false, EvaluateCondition(hc, "or(false, 0)"));
                Assert.Equal(true, EvaluateCondition(hc, "or(false, 'a')")); // string
                Assert.Equal(false, EvaluateCondition(hc, "or(false, '')"));
                Assert.Equal(true, EvaluateCondition(hc, "or(false, 1.2.3)")); // version
                Assert.Equal(true, EvaluateCondition(hc, "or(false, 0.0.0)"));
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
                Assert.Equal(true, EvaluateCondition(hc, "or(true, gt(1, 'not a number'))"));
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
                    EvaluateCondition(hc, "eq(1.2, 3.4a)");
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
                    EvaluateCondition(hc, "eq(1.2.3, 4.5.6.7a)");
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
                    EvaluateCondition(hc, "eq('hello', 'unterminated-string)");
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
                    EvaluateCondition(hc, "eq(1,2");
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
        public void ThrowsWhenExpectedOpenFunction()
        {
            using (var hc = new TestHostContext(this))
            {
                try
                {
                    EvaluateCondition(hc, "not(eq 1,2)");
                }
                catch (ParseException ex)
                {
                    Assert.Equal(ParseExceptionKind.ExpectedOpenFunction, ex.Kind);
                    Assert.Equal("eq", ex.RawToken);
                }
            }
        }

        private static bool EvaluateCondition(IHostContext context, string condition)
        {
            var expressionManager = new ExpressionManager(new TraceWriter(context));
            return expressionManager.EvaluateCondition(condition);
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
