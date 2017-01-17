using System;
using System.Globalization;
using System.Text;

namespace Microsoft.VisualStudio.Services.Agent
{
    public sealed partial class Condition
    {
        private Token GetNextToken()
        {
            // Skip whitespace.
            while (_index < _raw.Length && char.IsWhiteSpace(_raw[_index]))
            {
                _index++;
            }

            // Test end of string.
            if (_index >= _raw.Length)
            {
                return null;
            }

            // Read the first character to determine the type of token.
            char c = _raw[_index];
            switch (c)
            {
                case Constants.Conditions.CloseHashtable:
                    return new Token(TokenKind.CloseHashtable, _index++);
                case Constants.Conditions.CloseFunction:
                    return new Token(TokenKind.CloseFunction, _index++);
                case Constants.Conditions.OpenHashtable:
                    return new Token(TokenKind.OpenHashtable, _index++);
                case Constants.Conditions.OpenFunction:
                    return new Token(TokenKind.OpenFunction, _index++);
                case Constants.Conditions.Separator:
                    return new Token(TokenKind.Separator, _index++);
                case '\'':
                    return ReadStringToken();
                default:
                    if (c == '-' || c == '.' || (c >= '0' && c <= '9'))
                    {
                        return ReadNumberOrVersionToken();
                    }

                    return ReadKeywordToken();
            }
        }

        private Token ReadNumberOrVersionToken()
        {
            int startIndex = _index;
            int periods = 0;
            do
            {
                if (_raw[_index] == '.')
                {
                    periods++;
                }

                _index++;
            }
            while (_index < _raw.Length && !TestWhitespaceOrPunctuation(_raw[_index]));

            int length = _index - startIndex;
            string str = _raw.Substring(startIndex, length);
            if (periods >= 2)
            {
                Version version;
                if (Version.TryParse(str, out version))
                {
                    return new Token(TokenKind.Version, startIndex, length, version);
                }
            }
            else
            {
                // Note, NumberStyles.AllowThousands cannot be allowed since comma has special meaning as a token separator.
                decimal d;
                if (decimal.TryParse(
                        str,
                        NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out d))
                {
                    return new Token(TokenKind.Number, startIndex, length, d);
                }
            }

            return new Token(TokenKind.Unrecognized, startIndex, length);
        }

        private Token ReadKeywordToken()
        {
            // Read to the end of the keyword.
            int startIndex = _index;
            _index++; // Skip the first char. It is already known to be the start of the keyword.
            while (_index < _raw.Length && !TestWhitespaceOrPunctuation(_raw[_index]))
            {
                _index++;
            }

            // Convert to token.
            int length = _index - startIndex;
            string str = _raw.Substring(startIndex, length);
            if (str.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.True, startIndex, length, true);
            }
            else if (str.Equals(bool.FalseString, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.False, startIndex, length, false);
            }
            // Functions
            else if (str.Equals(Constants.Conditions.And, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.And, startIndex, length);
            }
            else if (str.Equals(Constants.Conditions.Equal, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.Equal, startIndex, length);
            }
            else if (str.Equals(Constants.Conditions.GreaterThan, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.GreaterThan, startIndex, length);
            }
            else if (str.Equals(Constants.Conditions.GreaterThanOrEqual, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.GreaterThanOrEqual, startIndex, length);
            }
            else if (str.Equals(Constants.Conditions.LessThan, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.LessThan, startIndex, length);
            }
            else if (str.Equals(Constants.Conditions.LessThanOrEqual, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.LessThanOrEqual, startIndex, length);
            }
            else if (str.Equals(Constants.Conditions.Not, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.Not, startIndex, length);
            }
            else if (str.Equals(Constants.Conditions.NotEqual, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.NotEqual, startIndex, length);
            }
            else if (str.Equals(Constants.Conditions.Or, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.Or, startIndex, length);
            }
            else if (str.Equals(Constants.Conditions.Xor, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.Xor, startIndex, length);
            }
            // Hashtables
            else if (str.Equals(Constants.Conditions.Capabilities, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.Capabilities, startIndex, length);
            }
            else if (str.Equals(Constants.Conditions.Variables, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.Variables, startIndex, length);
            }
            // Unrecognized
            else
            {
                return new Token(TokenKind.Unrecognized, startIndex, length);
            }
        }

        private Token ReadStringToken()
        {
            // TODO: Confirm double-single-quote for escaping is sufficient. Better than backslash-escaping since this is not a complex language and backslash is common to file-paths.
            int startIndex = _index;
            char c;
            bool closed = false;
            var str = new StringBuilder();
            _index++; // Skip the leading single-quote.
            while (_index < _raw.Length)
            {
                c = _raw[_index++];
                if (c == '\'')
                {
                    // End of string.
                    if (_index >= _raw.Length || _raw[_index] != '\'')
                    {
                        closed = true;
                        break;
                    }

                    // Escaped single quote.
                    _index++;
                }

                str.Append(c);
            }

            int length = _index - startIndex;
            if (closed)
            {
                return new Token(TokenKind.String, startIndex, length, str.ToString());
            }

            return new Token(TokenKind.Unrecognized, startIndex, length);
        }

        private static bool TestWhitespaceOrPunctuation(char c)
        {
            switch (c)
            {
                case Constants.Conditions.CloseFunction:
                case Constants.Conditions.CloseHashtable:
                case Constants.Conditions.OpenFunction:
                case Constants.Conditions.OpenHashtable:
                case Constants.Conditions.Separator:
                    return true;
                default:
                    return char.IsWhiteSpace(c);
            }
        }

        public sealed class Token
        {
            public Token(TokenKind kind, int index, int length = 1, object parsedValue = null)
            {
                Kind = kind;
                Index = index;
                Length = length;
                ParsedValue = parsedValue;
            }

            public TokenKind Kind { get; }

            public int Index { get; }

            public int Length { get; }

            public object ParsedValue { get; }
        }

        public enum TokenKind
        {
            // Punctuation
            CloseHashtable,
            CloseFunction,
            OpenHashtable,
            OpenFunction,
            Separator,

            // Literal value types
            True,
            False,
            Number,
            Version,
            String,

            // Functions
            And,
            Equal,
            GreaterThan,
            GreaterThanOrEqual,
            LessThan,
            LessThanOrEqual,
            Not,
            NotEqual,
            Or,
            Xor,

            // Hashtables
            Capabilities,
            Variables,

            Unrecognized,
        }
    }
}
