
namespace Microsoft.VisualStudio.Services.Agent.Expressions
{
    internal static class Constants
    {
        // Punctuation
        public const char StartIndex = '[';
        public const char StartParameter = '(';
        public const char EndIndex = ']';
        public const char EndParameter = ')';
        public const char Separator = ',';
        public const char Dereference = '.';

        // Functions
        public const string And = "and";
        public const string Equal = "eq";
        public const string GreaterThan = "gt";
        public const string GreaterThanOrEqual = "ge";
        public const string LessThan = "lt";
        public const string LessThanOrEqual = "le";
        public const string Not = "not";
        public const string NotEqual = "ne";
        public const string Or = "or";
        public const string Xor = "xor";
    }
}