using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Microsoft.VisualStudio.Services.Agent.Expressions
{
    internal sealed class LexicalAnalyzer
    {
        private static readonly Regex s_keywordRegex = new Regex("^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.None);
        private readonly string _raw; // Raw expression string.
        private readonly ITraceWriter _trace;
        private readonly IDictionary<string, object> _extensionObjects;
        private int _index; // Index of raw condition string.
        private Token _lastToken;

        public LexicalAnalyzer(string expression, ITraceWriter trace, IDictionary<string, object> extensionObjects)
        {
            ArgUtil.NotNull(trace, nameof(trace));
            ArgUtil.NotNull(extensionObjects, nameof(extensionObjects));
            _raw = expression;
            _trace = trace;
            _extensionObjects = extensionObjects;
        }

        public Token GetNextToken()
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
            Token token;
            switch (c)
            {
                case Constants.StartIndex:
                    token = new Token(TokenKind.StartIndex, _index++);
                    break;
                case Constants.StartParameter:
                    token = new Token(TokenKind.StartParameter, _index++);
                    break;
                case Constants.EndIndex:
                    token = new Token(TokenKind.EndIndex, _index++);
                    break;
                case Constants.EndParameter:
                    token = new Token(TokenKind.EndParameter, _index++);
                    break;
                case Constants.Separator:
                    token = new Token(TokenKind.Separator, _index++);
                    break;
                case Constants.Dereference:
                    token = new Token(TokenKind.Dereference, _index++);
                    break;
                case '\'':
                    token = ReadStringToken();
                    break;
                default:
                    if (c == '-' || c == '.' || (c >= '0' && c <= '9'))
                    {
                        token = ReadNumberOrVersionToken();
                    }
                    else
                    {
                        token = ReadKeywordToken();
                    }

                    break;
            }

            _lastToken = token;
            return token;
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
            while (_index < _raw.Length && (!TestWhitespaceOrPunctuation(_raw[_index]) || _raw[_index] == '.'));

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

            int length = _index - startIndex;
            string str = _raw.Substring(startIndex, length);
            if (s_keywordRegex.IsMatch(str))
            {
                if (_lastToken != null && _lastToken.Kind == TokenKind.Dereference)
                {
                    return new Token(TokenKind.PropertyName, startIndex, length, str);
                }
            }

            // Convert to token.
            if (str.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.Boolean, startIndex, length, true);
            }
            else if (str.Equals(bool.FalseString, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.Boolean, startIndex, length, false);
            }
            // Functions
            else if (str.Equals(Constants.And, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.And, startIndex, length);
            }
            else if (str.Equals(Constants.Equal, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.Equal, startIndex, length);
            }
            else if (str.Equals(Constants.GreaterThan, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.GreaterThan, startIndex, length);
            }
            else if (str.Equals(Constants.GreaterThanOrEqual, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.GreaterThanOrEqual, startIndex, length);
            }
            else if (str.Equals(Constants.LessThan, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.LessThan, startIndex, length);
            }
            else if (str.Equals(Constants.LessThanOrEqual, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.LessThanOrEqual, startIndex, length);
            }
            else if (str.Equals(Constants.Not, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.Not, startIndex, length);
            }
            else if (str.Equals(Constants.NotEqual, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.NotEqual, startIndex, length);
            }
            else if (str.Equals(Constants.Or, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.Or, startIndex, length);
            }
            else if (str.Equals(Constants.Xor, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.Xor, startIndex, length);
            }
            // Hashtables
            else if (str.Equals(Constants.Capabilities, StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenKind.Capabilities, startIndex, length);
            }
            else if (str.Equals(Constants.Variables, StringComparison.OrdinalIgnoreCase))
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
                case Constants.StartIndex:
                case Constants.StartParameter:
                case Constants.EndIndex:
                case Constants.EndParameter:
                case Constants.Separator:
                case Constants.Dereference:
                    return true;
                default:
                    return char.IsWhiteSpace(c);
            }
        }
    }
}
