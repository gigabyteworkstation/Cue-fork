using System;
using System.Collections.Generic;

namespace Cue.Sound
{
    // A tiny expression engine: parses a formula string once into a node tree,
    // then evaluates it every frame against a variable resolver. Lets any
    // parameter be a literal formula like "arousal * 0.5 + sin(time*3)".
    //
    // Grammar (recursive descent, precedence low->high):
    //   or   : and ('||' and)*
    //   and  : cmp ('&&' cmp)*
    //   cmp  : add (('<'|'>'|'<='|'>='|'=='|'!=') add)*
    //   add  : mul (('+'|'-') mul)*
    //   mul  : un  (('*'|'/'|'%') un)*
    //   un   : ('-'|'!')? pow
    //   pow  : prim ('^' un)?
    //   prim : number | ident | ident '(' args ')' | '(' or ')'
    //
    // Identifiers may contain letters, digits, '.', '_' (e.g. pen.velocity).
    // No C# enums anywhere (they crash VaM's runtime compiler).

    public interface IVarResolver
    {
        float Resolve(string name);
    }

    public abstract class ExprNode
    {
        public abstract float Eval(IVarResolver r);
    }

    public sealed class Expr
    {
        private readonly ExprNode root_;
        private readonly string error_;

        private Expr(ExprNode root, string error) { root_ = root; error_ = error; }

        public bool Ok       { get { return root_ != null; } }
        public string Error  { get { return error_; } }

        public float Eval(IVarResolver r)
        {
            if (root_ == null) return 0f;
            try { return root_.Eval(r); }
            catch (Exception) { return 0f; }
        }

        public static Expr Parse(string s)
        {
            if (string.IsNullOrEmpty(s))
                return new Expr(null, "empty");

            try
            {
                var p = new Parser(s);
                var n = p.ParseTop();
                return new Expr(n, null);
            }
            catch (Exception e)
            {
                return new Expr(null, e.Message);
            }
        }

        // ---- nodes -------------------------------------------------------
        private sealed class Num : ExprNode
        {
            private readonly float v_;
            public Num(float v) { v_ = v; }
            public override float Eval(IVarResolver r) { return v_; }
        }

        private sealed class Var : ExprNode
        {
            private readonly string n_;
            public Var(string n) { n_ = n; }
            public override float Eval(IVarResolver r) { return (r != null) ? r.Resolve(n_) : 0f; }
        }

        private sealed class Bin : ExprNode
        {
            private readonly int op_;
            private readonly ExprNode a_, b_;
            public Bin(int op, ExprNode a, ExprNode b) { op_ = op; a_ = a; b_ = b; }
            public override float Eval(IVarResolver r)
            {
                float a = a_.Eval(r), b = b_.Eval(r);
                switch (op_)
                {
                    case '+': return a + b;
                    case '-': return a - b;
                    case '*': return a * b;
                    case '/': return (Math.Abs(b) > 1e-9f) ? a / b : 0f;
                    case '%': return (Math.Abs(b) > 1e-9f) ? a % b : 0f;
                    case '^': return (float)Math.Pow(a, b);
                    case '<': return a <  b ? 1f : 0f;
                    case '>': return a >  b ? 1f : 0f;
                    case 'l': return a <= b ? 1f : 0f;
                    case 'g': return a >= b ? 1f : 0f;
                    case 'e': return Math.Abs(a - b) < 1e-5f ? 1f : 0f;
                    case 'n': return Math.Abs(a - b) >= 1e-5f ? 1f : 0f;
                    case '&': return (a != 0f && b != 0f) ? 1f : 0f;
                    case '|': return (a != 0f || b != 0f) ? 1f : 0f;
                }
                return 0f;
            }
        }

        private sealed class Un : ExprNode
        {
            private readonly int op_;
            private readonly ExprNode a_;
            public Un(int op, ExprNode a) { op_ = op; a_ = a; }
            public override float Eval(IVarResolver r)
            {
                float a = a_.Eval(r);
                if (op_ == '-') return -a;
                if (op_ == '!') return (a == 0f) ? 1f : 0f;
                return a;
            }
        }

        private sealed class Call : ExprNode
        {
            private readonly string fn_;
            private readonly ExprNode[] args_;
            public Call(string fn, ExprNode[] args) { fn_ = fn; args_ = args; }
            public override float Eval(IVarResolver r)
            {
                int n = args_.Length;
                float a = n > 0 ? args_[0].Eval(r) : 0f;
                float b = n > 1 ? args_[1].Eval(r) : 0f;
                float c = n > 2 ? args_[2].Eval(r) : 0f;

                switch (fn_)
                {
                    case "sin":   return (float)Math.Sin(a);
                    case "cos":   return (float)Math.Cos(a);
                    case "tan":   return (float)Math.Tan(a);
                    case "abs":   return Math.Abs(a);
                    case "sqrt":  return (a > 0f) ? (float)Math.Sqrt(a) : 0f;
                    case "floor": return (float)Math.Floor(a);
                    case "ceil":  return (float)Math.Ceiling(a);
                    case "round": return (float)Math.Round(a);
                    case "sign":  return Math.Sign(a);
                    case "exp":   return (float)Math.Exp(a);
                    case "log":   return (a > 0f) ? (float)Math.Log(a) : 0f;
                    case "frac":  return a - (float)Math.Floor(a);
                    case "min":   return Math.Min(a, b);
                    case "max":   return Math.Max(a, b);
                    case "pow":   return (float)Math.Pow(a, b);
                    case "mod":   return (Math.Abs(b) > 1e-9f) ? a % b : 0f;
                    case "clamp": return (a < b) ? b : (a > c ? c : a);
                    case "clamp01": return (a < 0f) ? 0f : (a > 1f ? 1f : a);
                    case "lerp":  return a + (b - a) * (c < 0f ? 0f : (c > 1f ? 1f : c));
                    case "step":  return (a >= b) ? 1f : 0f;
                    case "smooth": // smoothstep(edge0=a, edge1=b, x=c)
                    {
                        float t = (Math.Abs(b - a) < 1e-9f) ? 0f : (c - a) / (b - a);
                        t = t < 0f ? 0f : (t > 1f ? 1f : t);
                        return t * t * (3f - 2f * t);
                    }
                    case "pi":    return (float)Math.PI;
                }
                return 0f;
            }
        }

        // ---- parser ------------------------------------------------------
        private sealed class Parser
        {
            private readonly string s_;
            private int i_;

            public Parser(string s) { s_ = s; i_ = 0; }

            public ExprNode ParseTop()
            {
                var n = ParseOr();
                SkipWs();
                if (i_ < s_.Length)
                    throw new Exception("unexpected '" + s_[i_] + "'");
                return n;
            }

            private void SkipWs() { while (i_ < s_.Length && char.IsWhiteSpace(s_[i_])) i_++; }

            private bool Match(string tok)
            {
                SkipWs();
                if (i_ + tok.Length <= s_.Length && s_.Substring(i_, tok.Length) == tok)
                {
                    i_ += tok.Length;
                    return true;
                }
                return false;
            }

            private char Peek() { SkipWs(); return i_ < s_.Length ? s_[i_] : '\0'; }

            private ExprNode ParseOr()
            {
                var a = ParseAnd();
                while (Match("||")) a = new Bin('|', a, ParseAnd());
                return a;
            }

            private ExprNode ParseAnd()
            {
                var a = ParseCmp();
                while (Match("&&")) a = new Bin('&', a, ParseCmp());
                return a;
            }

            private ExprNode ParseCmp()
            {
                var a = ParseAdd();
                for (;;)
                {
                    if (Match("<=")) a = new Bin('l', a, ParseAdd());
                    else if (Match(">=")) a = new Bin('g', a, ParseAdd());
                    else if (Match("==")) a = new Bin('e', a, ParseAdd());
                    else if (Match("!=")) a = new Bin('n', a, ParseAdd());
                    else if (Match("<"))  a = new Bin('<', a, ParseAdd());
                    else if (Match(">"))  a = new Bin('>', a, ParseAdd());
                    else break;
                }
                return a;
            }

            private ExprNode ParseAdd()
            {
                var a = ParseMul();
                for (;;)
                {
                    char c = Peek();
                    if (c == '+') { i_++; a = new Bin('+', a, ParseMul()); }
                    else if (c == '-') { i_++; a = new Bin('-', a, ParseMul()); }
                    else break;
                }
                return a;
            }

            private ExprNode ParseMul()
            {
                var a = ParseUn();
                for (;;)
                {
                    char c = Peek();
                    if (c == '*') { i_++; a = new Bin('*', a, ParseUn()); }
                    else if (c == '/') { i_++; a = new Bin('/', a, ParseUn()); }
                    else if (c == '%') { i_++; a = new Bin('%', a, ParseUn()); }
                    else break;
                }
                return a;
            }

            private ExprNode ParseUn()
            {
                char c = Peek();
                if (c == '-') { i_++; return new Un('-', ParsePow()); }
                if (c == '!') { i_++; return new Un('!', ParsePow()); }
                if (c == '+') { i_++; return ParsePow(); }
                return ParsePow();
            }

            private ExprNode ParsePow()
            {
                var a = ParsePrim();
                if (Peek() == '^') { i_++; return new Bin('^', a, ParseUn()); }
                return a;
            }

            private ExprNode ParsePrim()
            {
                SkipWs();
                if (i_ >= s_.Length) throw new Exception("unexpected end");

                char c = s_[i_];

                if (c == '(')
                {
                    i_++;
                    var n = ParseOr();
                    SkipWs();
                    if (i_ >= s_.Length || s_[i_] != ')') throw new Exception("expected ')'");
                    i_++;
                    return n;
                }

                if (char.IsDigit(c) || c == '.')
                    return ParseNumber();

                if (IsIdentStart(c))
                    return ParseIdentOrCall();

                throw new Exception("unexpected '" + c + "'");
            }

            private ExprNode ParseNumber()
            {
                int start = i_;
                while (i_ < s_.Length && (char.IsDigit(s_[i_]) || s_[i_] == '.')) i_++;
                string t = s_.Substring(start, i_ - start);
                float v;
                if (!float.TryParse(t, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out v))
                    throw new Exception("bad number '" + t + "'");
                return new Num(v);
            }

            private static bool IsIdentStart(char c) { return char.IsLetter(c) || c == '_'; }
            private static bool IsIdentChar(char c)  { return char.IsLetterOrDigit(c) || c == '_' || c == '.'; }

            private ExprNode ParseIdentOrCall()
            {
                int start = i_;
                while (i_ < s_.Length && IsIdentChar(s_[i_])) i_++;
                string name = s_.Substring(start, i_ - start);

                SkipWs();
                if (i_ < s_.Length && s_[i_] == '(')
                {
                    i_++;
                    var args = new List<ExprNode>();
                    SkipWs();
                    if (i_ < s_.Length && s_[i_] == ')') { i_++; }
                    else
                    {
                        for (;;)
                        {
                            args.Add(ParseOr());
                            SkipWs();
                            if (i_ < s_.Length && s_[i_] == ',') { i_++; continue; }
                            if (i_ < s_.Length && s_[i_] == ')') { i_++; break; }
                            throw new Exception("expected ',' or ')'");
                        }
                    }
                    return new Call(name, args.ToArray());
                }

                return new Var(name);
            }
        }
    }
}
