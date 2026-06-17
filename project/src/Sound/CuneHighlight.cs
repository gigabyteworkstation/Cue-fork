using System.Text;

namespace Cue.Sound
{
    // Tolerant syntax colorizer for Cuneiform source. Produces the SAME text with
    // UGUI rich-text <color> tags inserted around tokens. Because those tags are
    // zero-width (the Text renderer consumes them), the colored string lays out
    // glyph-for-glyph identically to the raw source — which is what lets the
    // editor overlay sit exactly on top of the editable text.
    //
    // Never throws and never rewrites characters (no escaping): every source
    // character is emitted verbatim, so the overlay can never drift from the
    // InputField's own layout.
    public static class Highlight
    {
        // VS Code "Dark+"-ish palette.
        private const string Cmt  = "#6A9955";  // comments
        private const string Str  = "#CE9178";  // strings
        private const string Num  = "#B5CEA8";  // numbers
        private const string Kw   = "#569CD6";  // keywords
        private const string Var  = "#4EC9B0";  // $signals / table vars
        private const string Call = "#DCDCAA";  // function call names

        public static string Colorize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            var sb = new StringBuilder(s.Length + 64);
            int i = 0, n = s.Length;

            while (i < n)
            {
                char c = s[i];

                // line comment
                if (c == '/' && i + 1 < n && s[i + 1] == '/')
                {
                    int st = i;
                    while (i < n && s[i] != '\n') i++;
                    Wrap(sb, Cmt, s, st, i);
                    continue;
                }

                // string literal
                if (c == '"')
                {
                    int st = i; i++;
                    while (i < n && s[i] != '"')
                        i += (s[i] == '\\' && i + 1 < n) ? 2 : 1;
                    if (i < n) i++;   // closing quote
                    Wrap(sb, Str, s, st, i);
                    continue;
                }

                // number
                if (char.IsDigit(c) || (c == '.' && i + 1 < n && char.IsDigit(s[i + 1])))
                {
                    int st = i;
                    while (i < n && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                    Wrap(sb, Num, s, st, i);
                    continue;
                }

                // $signal / table var
                if (c == '$')
                {
                    int st = i; i++;
                    while (i < n && (char.IsLetterOrDigit(s[i]) || s[i] == '_' || s[i] == '.')) i++;
                    Wrap(sb, Var, s, st, i);
                    continue;
                }

                // identifier / keyword / call
                if (char.IsLetter(c) || c == '_')
                {
                    int st = i;
                    while (i < n && (char.IsLetterOrDigit(s[i]) || s[i] == '_' || s[i] == '.')) i++;
                    int len = i - st;

                    if (IsKeyword(s, st, len))
                        Wrap(sb, Kw, s, st, i);
                    else if (NextNonSpaceIsParen(s, i, n))
                        Wrap(sb, Call, s, st, i);
                    else
                        sb.Append(s, st, len);
                    continue;
                }

                // operators / punctuation / whitespace -> verbatim
                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }

        private static void Wrap(StringBuilder sb, string hex, string s, int start, int end)
        {
            sb.Append("<color=").Append(hex).Append('>');
            sb.Append(s, start, end - start);
            sb.Append("</color>");
        }

        private static bool NextNonSpaceIsParen(string s, int i, int n)
        {
            while (i < n && (s[i] == ' ' || s[i] == '\t')) i++;
            return i < n && s[i] == '(';
        }

        private static bool IsKeyword(string s, int start, int len)
        {
            return Eq(s, start, len, "let")  || Eq(s, start, len, "if")    ||
                   Eq(s, start, len, "else") || Eq(s, start, len, "while") ||
                   Eq(s, start, len, "on")   || Eq(s, start, len, "fire")  ||
                   Eq(s, start, len, "fixed")|| Eq(s, start, len, "late")  ||
                   Eq(s, start, len, "update")|| Eq(s, start, len, "true") ||
                   Eq(s, start, len, "false")|| Eq(s, start, len, "import");
        }

        private static bool Eq(string s, int start, int len, string kw)
        {
            if (len != kw.Length) return false;
            for (int k = 0; k < len; ++k)
                if (s[start + k] != kw[k]) return false;
            return true;
        }
    }
}
