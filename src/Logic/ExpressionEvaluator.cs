using System;
using System.Collections.Generic;
using System.Globalization;

namespace DeskMate.Logic
{
    /// <summary>Shunting-yard expression evaluator for the calculator.
    /// Supports + - * / % ^ ( ), functions sin cos tan asin acos atan log ln sqrt abs, constants pi e.
    /// Trig operates in degrees when <c>degrees</c> is true.</summary>
    public static class ExpressionEvaluator
    {
        enum TokKind { Num, Op, Func, LParen, RParen }

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
                if (c == '+' || c == '-' || c == '*' || c == '/' || c == '%' || c == '^' ||
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

        static double EvalRpn(List<Tok> rpn, bool degrees)
        {
            var st = new Stack<double>();
            foreach (var t in rpn)
            {
                if (t.Kind == TokKind.Num) { st.Push(t.Num); continue; }
                if (t.Kind == TokKind.Op)
                {
                    if (st.Count < 2) throw new FormatException("syntax");
                    double b = st.Pop(), a = st.Pop();
                    switch (t.Op)
                    {
                        case '+': st.Push(a + b); break;
                        case '-': st.Push(a - b); break;
                        case '*': st.Push(a * b); break;
                        case '/': st.Push(a / b); break;
                        case '%': st.Push(a % b); break;
                        case '^': st.Push(Math.Pow(a, b)); break;
                    }
                    continue;
                }
                if (t.Kind == TokKind.Func)
                {
                    if (st.Count < 1) throw new FormatException("syntax");
                    double x = st.Pop();
                    double inRad = degrees ? x * Math.PI / 180.0 : x;
                    switch (t.Func)
                    {
                        case "neg": st.Push(-x); break;
                        case "sin": st.Push(Math.Sin(inRad)); break;
                        case "cos": st.Push(Math.Cos(inRad)); break;
                        case "tan": st.Push(Math.Tan(inRad)); break;
                        case "asin": st.Push(degrees ? Math.Asin(x) * 180.0 / Math.PI : Math.Asin(x)); break;
                        case "acos": st.Push(degrees ? Math.Acos(x) * 180.0 / Math.PI : Math.Acos(x)); break;
                        case "atan": st.Push(degrees ? Math.Atan(x) * 180.0 / Math.PI : Math.Atan(x)); break;
                        case "log": st.Push(Math.Log10(x)); break;
                        case "ln": st.Push(Math.Log(x)); break;
                        case "sqrt": st.Push(Math.Sqrt(x)); break;
                        case "abs": st.Push(Math.Abs(x)); break;
                        default: throw new FormatException("unknown fn " + t.Func);
                    }
                    continue;
                }
                throw new FormatException("syntax");
            }
            if (st.Count != 1) throw new FormatException("syntax");
            double result = st.Pop();
            if (double.IsNaN(result) || double.IsInfinity(result)) throw new FormatException("math");
            return result;
        }
    }
}
