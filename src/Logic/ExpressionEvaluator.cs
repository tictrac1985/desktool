using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DeskMate.Logic
{
    /// <summary>Shunting-yard expression evaluator for the calculator.
    /// Supports + - * / % ^ ( ), functions sin cos tan asin acos atan log ln sqrt abs, constants pi e.
    /// % has two context-decided meanings: between two operands it is the infix remainder
    /// (10%3 = 1); right after an operand with no operand following it is POSTFIX percent
    /// with Windows-calculator semantics — 16000*15% = 2400, 200+10% = 220.
    /// Trig operates in degrees when <c>degrees</c> is true.</summary>
    public static class ExpressionEvaluator
    {
        /// <summary>"20% من 450" or "20% of 450" → "(20/100)*(450)". The % key is a
        /// remainder operator, which is never what a person typing "percent of" means —
        /// without this, the most common everyday percentage question returns nonsense.</summary>
        public static string NormalizePercentPhrase(string expr)
        {
            if (string.IsNullOrWhiteSpace(expr)) return expr;
            var m = Regex.Match(expr, @"^\s*(\d+(?:\.\d+)?)\s*%\s*(?:من|of)\s+(.+?)\s*$",
                RegexOptions.IgnoreCase);
            if (!m.Success) return expr;
            return "((" + m.Groups[1].Value + ")/100)*(" + m.Groups[2].Value + ")";
        }

        enum TokKind { Num, Op, Func, LParen, RParen, Pct }

        class Tok
        {
            public TokKind Kind;
            public double Num;
            public char Op;
            public string Func;
        }

        public static double Eval(string expr, bool degrees = true)
        {
            if (string.IsNullOrWhiteSpace(expr)) throw new FormatException("empty");
            var tokens = Tokenize(expr);
            var rpn = ToRpn(tokens);
            return EvalRpn(rpn, degrees);
        }

        static List<Tok> Tokenize(string s)
        {
            var toks = new List<Tok>();
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t') { i++; continue; }
                if (char.IsDigit(c) || c == '.')
                {
                    int start = i;
                    while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                    toks.Add(new Tok { Kind = TokKind.Num, Num = double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture) });
                    continue;
                }
                if (char.IsLetter(c) || c == 'π')
                {
                    int start = i;
                    while (i < s.Length && char.IsLetter(s[i])) i++;
                    string word = s.Substring(start, Math.Max(1, i - start)).ToLowerInvariant();
                    if (c == 'π') { word = "pi"; if (i == start) i++; }
                    if (word == "pi") toks.Add(new Tok { Kind = TokKind.Num, Num = Math.PI });
                    else if (word == "e") toks.Add(new Tok { Kind = TokKind.Num, Num = Math.E });
                    else toks.Add(new Tok { Kind = TokKind.Func, Func = word });
                    continue;
                }
                if (c == '(') { toks.Add(new Tok { Kind = TokKind.LParen }); i++; continue; }
                if (c == ')') { toks.Add(new Tok { Kind = TokKind.RParen }); i++; continue; }
                if (c == '%')
                {
                    // Two meanings, decided by context:
                    //  · followed by an operand (10%3) → infix remainder
                    //  · right after an operand, followed by nothing / an operator / ')'
                    //    (16000*15%, 200+10%) → POSTFIX percent, the everyday meaning on
                    //    every pocket calculator. Without this the most common percentage
                    //    pattern — price × VAT% — died in a syntax error.
                    bool prevIsOperand = toks.Count > 0 &&
                        (toks[toks.Count - 1].Kind == TokKind.Num || toks[toks.Count - 1].Kind == TokKind.RParen);
                    int j = i + 1;
                    while (j < s.Length && (s[j] == ' ' || s[j] == '\t')) j++;
                    bool nextStartsOperand = j < s.Length &&
                        (char.IsDigit(s[j]) || s[j] == '.' || char.IsLetter(s[j]) || s[j] == 'π' || s[j] == '(');
                    if (prevIsOperand && !nextStartsOperand)
                        toks.Add(new Tok { Kind = TokKind.Pct });
                    else
                        toks.Add(new Tok { Kind = TokKind.Op, Op = '%' });
                    i++;
                    continue;
                }
                if (c == '+' || c == '-' || c == '*' || c == '/' || c == '^' ||
                    c == '×' || c == '÷' || c == '−')
                {
                    char op = c;
                    if (c == '×') op = '*';
                    if (c == '÷') op = '/';
                    if (c == '−') op = '-';
                    // unary minus/plus → represent as 'm' (negate) applied like a function
                    bool unary = toks.Count == 0 ||
                                 toks[toks.Count - 1].Kind == TokKind.Op ||
                                 toks[toks.Count - 1].Kind == TokKind.LParen;
                    if (unary && op == '-') toks.Add(new Tok { Kind = TokKind.Func, Func = "neg" });
                    else if (unary && op == '+') { /* ignore unary plus */ }
                    else toks.Add(new Tok { Kind = TokKind.Op, Op = op });
                    i++;
                    continue;
                }
                throw new FormatException("bad char '" + c + "'");
            }
            return toks;
        }

        static int Prec(char op)
        {
            switch (op)
            {
                case '+': case '-': return 1;
                case '*': case '/': case '%': return 2;
                case '^': return 3;
                default: return 0;
            }
        }

        static bool RightAssoc(char op) { return op == '^'; }

        static List<Tok> ToRpn(List<Tok> toks)
        {
            var outQ = new List<Tok>();
            var stack = new Stack<Tok>();
            foreach (var t in toks)
            {
                switch (t.Kind)
                {
                    case TokKind.Num: outQ.Add(t); break;
                    case TokKind.Pct:
                        // postfix: the operand it modifies has just been fully emitted, so it
                        // binds tighter than any pending operator (2*3% evaluates 3% first)
                        if (outQ.Count == 0) throw new FormatException("syntax");
                        outQ.Add(t);
                        break;
                    case TokKind.Func: stack.Push(t); break;
                    case TokKind.Op:
                        while (stack.Count > 0)
                        {
                            var top = stack.Peek();
                            if (top.Kind == TokKind.Func) { outQ.Add(stack.Pop()); continue; }
                            if (top.Kind == TokKind.Op &&
                                (Prec(top.Op) > Prec(t.Op) || (Prec(top.Op) == Prec(t.Op) && !RightAssoc(t.Op))))
                            { outQ.Add(stack.Pop()); continue; }
                            break;
                        }
                        stack.Push(t);
                        break;
                    case TokKind.LParen: stack.Push(t); break;
                    case TokKind.RParen:
                        bool found = false;
                        while (stack.Count > 0)
                        {
                            var top = stack.Pop();
                            if (top.Kind == TokKind.LParen) { found = true; break; }
                            outQ.Add(top);
                        }
                        if (!found) throw new FormatException("unbalanced )");
                        if (stack.Count > 0 && stack.Peek().Kind == TokKind.Func) outQ.Add(stack.Pop());
                        break;
                }
            }
            while (stack.Count > 0)
            {
                var top = stack.Pop();
                if (top.Kind == TokKind.LParen) throw new FormatException("unbalanced (");
                outQ.Add(top);
            }
            return outQ;
        }

        /// <summary>A stack value: the number, plus whether a postfix % is still attached
        /// to it. The % resolves LAZILY at the operator that consumes it — after * / ^ it
        /// means /100 (16000*15% = 16000×0.15), after + − it means "that percent OF the
        /// left operand" (200+10% = 220), exactly like the Windows calculator.</summary>
        struct Val { public double V; public bool Pct; }

        static double EvalRpn(List<Tok> rpn, bool degrees)
        {
            var st = new Stack<Val>();
            foreach (var t in rpn)
            {
                if (t.Kind == TokKind.Num) { st.Push(new Val { V = t.Num }); continue; }
                if (t.Kind == TokKind.Pct)
                {
                    if (st.Count < 1) throw new FormatException("syntax");
                    var pv = st.Pop();
                    st.Push(new Val { V = pv.V, Pct = true });
                    continue;
                }
                if (t.Kind == TokKind.Op)
                {
                    if (st.Count < 2) throw new FormatException("syntax");
                    Val b = st.Pop(), a = st.Pop();
                    double av = a.Pct ? a.V / 100.0 : a.V;
                    double bv = !b.Pct ? b.V
                        : (t.Op == '+' || t.Op == '-') ? a.V * b.V / 100.0
                        : b.V / 100.0;
                    switch (t.Op)
                    {
                        case '+': st.Push(new Val { V = av + bv }); break;
                        case '-': st.Push(new Val { V = av - bv }); break;
                        case '*': st.Push(new Val { V = av * bv }); break;
                        case '/': st.Push(new Val { V = av / bv }); break;
                        case '%': st.Push(new Val { V = av % bv }); break;
                        case '^': st.Push(new Val { V = Math.Pow(av, bv) }); break;
                    }
                    continue;
                }
                if (t.Kind == TokKind.Func)
                {
                    if (st.Count < 1) throw new FormatException("syntax");
                    var xv = st.Pop();
                    double x = xv.Pct ? xv.V / 100.0 : xv.V;
                    double inRad = degrees ? x * Math.PI / 180.0 : x;
                    switch (t.Func)
                    {
                        case "neg": st.Push(new Val { V = -x }); break;
                        case "sin": st.Push(new Val { V = Math.Sin(inRad) }); break;
                        case "cos": st.Push(new Val { V = Math.Cos(inRad) }); break;
                        case "tan": st.Push(new Val { V = Math.Tan(inRad) }); break;
                        case "asin": st.Push(new Val { V = degrees ? Math.Asin(x) * 180.0 / Math.PI : Math.Asin(x) }); break;
                        case "acos": st.Push(new Val { V = degrees ? Math.Acos(x) * 180.0 / Math.PI : Math.Acos(x) }); break;
                        case "atan": st.Push(new Val { V = degrees ? Math.Atan(x) * 180.0 / Math.PI : Math.Atan(x) }); break;
                        case "log": st.Push(new Val { V = Math.Log10(x) }); break;
                        case "ln": st.Push(new Val { V = Math.Log(x) }); break;
                        case "sqrt": st.Push(new Val { V = Math.Sqrt(x) }); break;
                        case "abs": st.Push(new Val { V = Math.Abs(x) }); break;
                        default: throw new FormatException("unknown fn " + t.Func);
                    }
                    continue;
                }
                throw new FormatException("syntax");
            }
            if (st.Count != 1) throw new FormatException("syntax");
            Val result = st.Pop();
            double finalV = result.Pct ? result.V / 100.0 : result.V;
            if (double.IsNaN(finalV) || double.IsInfinity(finalV)) throw new FormatException("math");
            return finalV;
        }
    }
}
