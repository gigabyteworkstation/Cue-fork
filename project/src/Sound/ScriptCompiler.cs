using System;
using System.Collections.Generic;

namespace Cue.Sound
{
    // Lexer + single-pass recursive-descent compiler: source text -> bytecode
    // (with backpatched jumps). Produces a ScriptProgram with one code segment
    // per hook, sharing the constants / names / variable pools.
    public static class Compiler
    {
        // token types (no enums)
        private const int T_NUM = 0, T_IDENT = 1, T_VAR = 2, T_KW = 3, T_OP = 4,
                          T_LP = 5, T_RP = 6, T_LB = 7, T_RB = 8, T_COMMA = 9,
                          T_EOF = 10, T_STR = 11;

        private struct Tok { public int t; public string s; public float n; }

        public static ScriptProgram Compile(string src)
        {
            return Compile(src, null);
        }

        // resolver maps an imported name -> that script's source text (or null).
        public static ScriptProgram Compile(string src, Func<string, string> resolver)
        {
            var p = new ScriptProgram();
            try
            {
                string full = Preprocess(src, resolver, new HashSet<string>());
                var c = new Ctx(full);
                c.ParseAll(p);
            }
            catch (Exception e)
            {
                p.error = e.Message;
            }
            return p;
        }

        // Handles `import "name"` at the top of a script by splicing in the
        // imported source ahead of the importer. Cycle-guarded; a missing import
        // is reported as a compile error rather than silently skipped.
        private static string Preprocess(string src, Func<string, string> resolver, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(src) || src.IndexOf("import") < 0)
                return src;

            var sb = new System.Text.StringBuilder();
            var lines = src.Split('\n');
            for (int i = 0; i < lines.Length; ++i)
            {
                string line = lines[i];
                string t = line.Trim();
                if (t.StartsWith("import"))
                {
                    string name = ExtractImportName(t);
                    if (name == null)
                        throw new Exception("import expects a quoted name");
                    if (seen.Contains(name))
                        continue;                 // already pulled in / cycle
                    seen.Add(name);
                    string dep = (resolver != null) ? resolver(name) : null;
                    if (dep == null)
                        throw new Exception("cannot import '" + name + "'");
                    sb.Append(Preprocess(dep, resolver, seen));
                    sb.Append('\n');
                    continue;
                }
                sb.Append(line);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private static string ExtractImportName(string line)
        {
            int a = line.IndexOf('"');
            if (a < 0) return null;
            int b = line.IndexOf('"', a + 1);
            if (b <= a) return null;
            return line.Substring(a + 1, b - a - 1);
        }

        private static bool IsKeyword(string s)
        {
            return s == "let" || s == "if" || s == "else" || s == "while" ||
                   s == "on" || s == "fire" || s == "fixed" || s == "late" ||
                   s == "update" || s == "true" || s == "false";
        }

        // ---- lexer -------------------------------------------------------
        private static List<Tok> Lex(string s)
        {
            var toks = new List<Tok>();
            int i = 0, n = s.Length;

            while (i < n)
            {
                char c = s[i];

                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (c == '/' && i + 1 < n && s[i + 1] == '/')
                {
                    while (i < n && s[i] != '\n') i++;
                    continue;
                }

                if (char.IsDigit(c) || (c == '.' && i + 1 < n && char.IsDigit(s[i + 1])))
                {
                    int st = i;
                    while (i < n && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                    float v;
                    float.TryParse(s.Substring(st, i - st),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out v);
                    toks.Add(new Tok { t = T_NUM, n = v });
                    continue;
                }

                if (c == '$')
                {
                    i++;
                    int st = i;
                    while (i < n && (char.IsLetterOrDigit(s[i]) || s[i] == '_' || s[i] == '.')) i++;
                    toks.Add(new Tok { t = T_VAR, s = s.Substring(st, i - st) });
                    continue;
                }

                if (c == '"')
                {
                    i++;
                    var sb = new System.Text.StringBuilder();
                    while (i < n && s[i] != '"')
                    {
                        if (s[i] == '\\' && i + 1 < n)
                        {
                            char e = s[i + 1];
                            sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
                            i += 2;
                        }
                        else sb.Append(s[i++]);
                    }
                    if (i >= n) throw new Exception("unterminated string");
                    i++;   // closing quote
                    toks.Add(new Tok { t = T_STR, s = sb.ToString() });
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    int st = i;
                    while (i < n && (char.IsLetterOrDigit(s[i]) || s[i] == '_' || s[i] == '.')) i++;
                    string id = s.Substring(st, i - st);
                    toks.Add(new Tok { t = IsKeyword(id) ? T_KW : T_IDENT, s = id });
                    continue;
                }

                if (c == '(') { toks.Add(new Tok { t = T_LP }); i++; continue; }
                if (c == ')') { toks.Add(new Tok { t = T_RP }); i++; continue; }
                if (c == '{') { toks.Add(new Tok { t = T_LB }); i++; continue; }
                if (c == '}') { toks.Add(new Tok { t = T_RB }); i++; continue; }
                if (c == ',') { toks.Add(new Tok { t = T_COMMA }); i++; continue; }

                // operators (two-char first)
                if (i + 1 < n)
                {
                    string two = s.Substring(i, 2);
                    if (two == "<=" || two == ">=" || two == "==" || two == "!=" ||
                        two == "&&" || two == "||")
                    {
                        toks.Add(new Tok { t = T_OP, s = two });
                        i += 2;
                        continue;
                    }
                }

                if ("+-*/%^<>=!".IndexOf(c) >= 0)
                {
                    toks.Add(new Tok { t = T_OP, s = c.ToString() });
                    i++;
                    continue;
                }

                throw new Exception("unexpected char '" + c + "'");
            }

            toks.Add(new Tok { t = T_EOF });
            return toks;
        }

        // ---- parser / emitter --------------------------------------------
        private class Ctx
        {
            private readonly List<Tok> tk_;
            private int pos_;

            private List<int> code_;                    // current target segment
            private readonly List<float> consts_ = new List<float>();
            private readonly List<string> names_ = new List<string>();
            private readonly List<string> strs_ = new List<string>();
            private readonly Dictionary<string, int> locals_ = new Dictionary<string, int>();

            public Ctx(string src) { tk_ = Lex(src); pos_ = 0; }

            private Tok Cur { get { return tk_[pos_]; } }
            private Tok Next() { return tk_[pos_++]; }
            private bool IsKw(string s) { return Cur.t == T_KW && Cur.s == s; }
            private bool IsOp(string s) { return Cur.t == T_OP && Cur.s == s; }

            private void Expect(int t, string what)
            {
                if (Cur.t != t) throw new Exception("expected " + what);
                pos_++;
            }

            private int ConstIdx(float v)
            {
                for (int i = 0; i < consts_.Count; ++i) if (consts_[i] == v) return i;
                consts_.Add(v); return consts_.Count - 1;
            }
            private int NameIdx(string s)
            {
                for (int i = 0; i < names_.Count; ++i) if (names_[i] == s) return i;
                names_.Add(s); return names_.Count - 1;
            }
            private int StrIdx(string s)
            {
                for (int i = 0; i < strs_.Count; ++i) if (strs_[i] == s) return i;
                strs_.Add(s); return strs_.Count - 1;
            }
            private int Local(string s)
            {
                int idx;
                if (locals_.TryGetValue(s, out idx)) return idx;
                idx = locals_.Count; locals_[s] = idx; return idx;
            }

            private void E(int op) { code_.Add(op); }
            private void E(int op, int a) { code_.Add(op); code_.Add(a); }

            public void ParseAll(ScriptProgram prog)
            {
                var segs = new List<int>[Hook.Count];

                // top-level statements form the update segment; `on X { }` blocks
                // form the fixed/late segments
                code_ = new List<int>();
                segs[Hook.Update] = code_;

                while (Cur.t != T_EOF)
                {
                    if (IsKw("on"))
                    {
                        pos_++;
                        int hook = Hook.Update;
                        if (IsKw("fixed")) { hook = Hook.Fixed; pos_++; }
                        else if (IsKw("late")) { hook = Hook.Late; pos_++; }
                        else if (IsKw("update")) { hook = Hook.Update; pos_++; }
                        else throw new Exception("expected fixed/late/update after 'on'");

                        if (segs[hook] == null) segs[hook] = new List<int>();
                        var prev = code_;
                        code_ = segs[hook];
                        Expect(T_LB, "'{'");
                        Block();
                        code_ = prev;
                    }
                    else
                    {
                        Statement();
                    }
                }

                prog.consts = consts_.ToArray();
                prog.names = names_.ToArray();
                prog.strs = strs_.ToArray();
                prog.numVars = locals_.Count;
                for (int h = 0; h < Hook.Count; ++h)
                {
                    if (segs[h] != null && segs[h].Count > 0)
                    {
                        segs[h].Add(Op.HALT);
                        prog.code[h] = segs[h].ToArray();
                    }
                }
            }

            private void Block()
            {
                while (Cur.t != T_RB && Cur.t != T_EOF)
                    Statement();
                Expect(T_RB, "'}'");
            }

            private void Statement()
            {
                if (Cur.t == T_LB) { pos_++; Block(); return; }

                if (IsKw("let"))
                {
                    pos_++;
                    if (Cur.t != T_IDENT) throw new Exception("expected name after let");
                    string nm = Next().s;
                    Expect2Op("=");
                    Expr();
                    E(Op.STOREL, Local(nm));
                    return;
                }

                if (IsKw("fire"))
                {
                    pos_++;
                    string nm;
                    if (Cur.t == T_IDENT || Cur.t == T_VAR) nm = Next().s;
                    else throw new Exception("expected trigger name after fire");
                    E(Op.FIRE, NameIdx(nm));
                    return;
                }

                if (IsKw("if"))    { IfStmt(); return; }
                if (IsKw("while")) { WhileStmt(); return; }

                // assignment to local or $var, else expression statement
                if (Cur.t == T_IDENT && tk_[pos_ + 1].t == T_OP && tk_[pos_ + 1].s == "=")
                {
                    string nm = Next().s; pos_++;  // skip '='
                    Expr();
                    E(Op.STOREL, Local(nm));
                    return;
                }
                if (Cur.t == T_VAR && tk_[pos_ + 1].t == T_OP && tk_[pos_ + 1].s == "=")
                {
                    string nm = Next().s; pos_++;
                    Expr();
                    E(Op.SETV, NameIdx(nm));
                    return;
                }

                // bare expression -> discard
                Expr();
                E(Op.POP);
            }

            private void Expect2Op(string s)
            {
                if (!(Cur.t == T_OP && Cur.s == s)) throw new Exception("expected '" + s + "'");
                pos_++;
            }

            private void IfStmt()
            {
                pos_++;
                Expect(T_LP, "'('");
                Expr();
                Expect(T_RP, "')'");
                E(Op.JZ, 0); int jzAt = code_.Count - 1;
                Expect(T_LB, "'{'");
                Block();

                if (IsKw("else"))
                {
                    pos_++;
                    E(Op.JMP, 0); int jmpAt = code_.Count - 1;
                    code_[jzAt] = code_.Count;       // jz -> else block
                    Expect(T_LB, "'{'");
                    Block();
                    code_[jmpAt] = code_.Count;       // jmp -> after else
                }
                else
                {
                    code_[jzAt] = code_.Count;
                }
            }

            private void WhileStmt()
            {
                pos_++;
                int start = code_.Count;
                Expect(T_LP, "'('");
                Expr();
                Expect(T_RP, "')'");
                E(Op.JZ, 0); int jzAt = code_.Count - 1;
                Expect(T_LB, "'{'");
                Block();
                E(Op.JMP, start);
                code_[jzAt] = code_.Count;
            }

            // expression precedence (low -> high)
            private void Expr() { Or(); }

            private void Or()
            {
                And();
                while (IsOp("||")) { pos_++; And(); E(Op.OR); }
            }
            private void And()
            {
                Cmp();
                while (IsOp("&&")) { pos_++; Cmp(); E(Op.AND); }
            }
            private void Cmp()
            {
                Add();
                for (;;)
                {
                    if (IsOp("<"))  { pos_++; Add(); E(Op.LT); }
                    else if (IsOp(">"))  { pos_++; Add(); E(Op.GT); }
                    else if (IsOp("<=")) { pos_++; Add(); E(Op.LE); }
                    else if (IsOp(">=")) { pos_++; Add(); E(Op.GE); }
                    else if (IsOp("==")) { pos_++; Add(); E(Op.EQ); }
                    else if (IsOp("!=")) { pos_++; Add(); E(Op.NE); }
                    else break;
                }
            }
            private void Add()
            {
                Mul();
                for (;;)
                {
                    if (IsOp("+")) { pos_++; Mul(); E(Op.ADD); }
                    else if (IsOp("-")) { pos_++; Mul(); E(Op.SUB); }
                    else break;
                }
            }
            private void Mul()
            {
                Un();
                for (;;)
                {
                    if (IsOp("*")) { pos_++; Un(); E(Op.MUL); }
                    else if (IsOp("/")) { pos_++; Un(); E(Op.DIV); }
                    else if (IsOp("%")) { pos_++; Un(); E(Op.MOD); }
                    else break;
                }
            }
            private void Un()
            {
                if (IsOp("-")) { pos_++; Pow(); E(Op.NEG); return; }
                if (IsOp("!")) { pos_++; Pow(); E(Op.NOT); return; }
                if (IsOp("+")) { pos_++; }
                Pow();
            }
            private void Pow()
            {
                Prim();
                if (IsOp("^")) { pos_++; Un(); E(Op.POW); }
            }
            private void Prim()
            {
                if (Cur.t == T_NUM) { E(Op.PUSHC, ConstIdx(Next().n)); return; }
                if (Cur.t == T_VAR) { E(Op.LOADV, NameIdx(Next().s)); return; }

                if (Cur.t == T_LP)
                {
                    pos_++; Expr(); Expect(T_RP, "')'"); return;
                }

                if (IsKw("true"))  { pos_++; E(Op.PUSHC, ConstIdx(1f)); return; }
                if (IsKw("false")) { pos_++; E(Op.PUSHC, ConstIdx(0f)); return; }

                if (Cur.t == T_IDENT)
                {
                    string id = Next().s;

                    if (Cur.t == T_LP)   // function call
                    {
                        int fn = Builtins.Find(id);
                        if (fn >= 0)
                        {
                            pos_++;
                            int argc = 0;
                            if (Cur.t != T_RP)
                            {
                                Expr(); argc++;
                                while (Cur.t == T_COMMA) { pos_++; Expr(); argc++; }
                            }
                            Expect(T_RP, "')'");
                            code_.Add(Op.CALL); code_.Add(fn); code_.Add(argc);
                            return;
                        }

                        int hid = Host.Find(id);
                        if (hid >= 0)
                        {
                            HostCall(id, hid);
                            return;
                        }

                        throw new Exception("unknown function '" + id + "'");
                    }

                    E(Op.LOADL, Local(id));   // local variable read
                    return;
                }

                throw new Exception("unexpected token in expression");
            }

            // host call: parse args against the function's typed signature.
            // string args (a leading prefix) come from string literals -> PUSHS;
            // float args are full expressions. Trailing floats are optional.
            private void HostCall(string name, int hid)
            {
                pos_++;   // '('
                int sargc = 0, fargc = 0, argi = 0;

                if (Cur.t != T_RP)
                {
                    for (;;)
                    {
                        int kind = Host.KindAt(hid, argi);
                        if (kind == Host.KString)
                        {
                            if (Cur.t != T_STR)
                                throw new Exception("argument " + (argi + 1) + " of " + name + " must be a \"string\"");
                            E(Op.PUSHS, StrIdx(Next().s));
                            sargc++;
                        }
                        else
                        {
                            Expr();
                            fargc++;
                        }
                        argi++;

                        if (Cur.t == T_COMMA) { pos_++; continue; }
                        break;
                    }
                }

                Expect(T_RP, "')'");

                if (sargc < Host.ReqStrings(hid))
                    throw new Exception(name + " is missing a required \"string\" argument");

                code_.Add(Op.HCALL); code_.Add(hid); code_.Add(fargc); code_.Add(sargc);
            }
        }
    }
}
