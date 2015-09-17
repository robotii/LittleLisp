using System;
using System.IO;

namespace LittleLisp
{
    public delegate Obj Primitive(Obj env, Obj args); // Delegate for the primitive function.

    public class Obj // The object type
    {
        public int Type; // This represents the type of the object.
        public int Value; // Int
        public Obj First; // Cell / Environment Frame
        public Obj Rest;
        public string Name; // Symbol
        public Primitive Fn; // Primitive
        public Obj Env; // Function or Macro
    };

    public class LittleLisp
    {
        // The Lisp object types
        private const int Tint = 1;
        private const int Tcell = 2;
        private const int Tsymbol = 3;
        private const int Tprimitive = 4;
        private const int Tfunction = 5;
        private const int Tmacro = 6;
        private const int Tenv = 7;
        private const int Tnil = 8;
        private const int Tdot = 9;
        private const int Tcparen = 10;
        private const int Ttrue = 11;

        // Constants
        private const string SymbolChars = "~!@#$%^&*-_=+:/?<>";
        private readonly Obj Nil;
        private readonly Obj Dot;
        private readonly Obj Cparen;
        private readonly Obj True;
        private readonly Obj Rootenv;

        private string _currentSource;
        private int _currentPos;

        // The list containing all symbols
        private static Obj _symbols;

        private int _nextSymbol;

        // Destructively reverses the given list.
        private Obj Reverse(Obj p)
        {
            Obj ret = Nil;
            while (p != Nil)
            {
                Obj head = p;
                p = p.Rest;
                head.Rest = ret;
                ret = head;
            }
            return ret;
        }

        private static Obj MakeInt(int value)
        {
            return new Obj { Type = Tint, Value = value };
        }

        private static Obj MakeSymbol(string name)
        {
            return new Obj { Type = Tsymbol, Name = name };
        }

        private static Obj MakePrimitive(Primitive fn)
        {
            return new Obj { Type = Tprimitive, Fn = fn };
        }

        private static Obj MakeFunction(int type, Obj params_, Obj body, Obj env)
        {
            return new Obj { Type = type, First = params_, Rest = body, Env = env };
        }

        private static Obj MakeSpecial(int subtype)
        {
            return new Obj { Type = subtype };
        }

        private static Obj MakeEnv(Obj vars, Obj up)
        {
            return new Obj { Type = Tenv, First = vars, Rest = up };
        }

        private static Obj Join(Obj first, Obj rest)
        {
            return new Obj { First = first, Rest = rest, Type = Tcell };
        }

        private static Obj Acons(Obj x, Obj y, Obj a)
        {
            return Join(Join(x, y), a); // Returns ((x . y) . a)
        }

        private static int OpenBrackets(string s)
        {
            return s.Replace("(", "").Length - s.Replace("(", "").Length;
        }

        private static void Error(string fmt)
        {
            Console.Error.WriteLine(fmt);
            throw new Exception(fmt);
        }

        private static void Print(string s)
        {
            Console.Write(s);
        }

        private char GetChar()
        {
            return _currentPos >= _currentSource.Length ? (char)0 : _currentSource[_currentPos++];
        }

        private static bool IsDigit(char c)
        {
            return c >= '0' && '9' >= c;
        }

        private char Peek()
        {
            return _currentPos >= _currentSource.Length ? (char)0 : _currentSource[_currentPos];
        }

        private static bool IsAlnum(char c)
        {
            return IsAlpha(c) || IsDigit(c);
        }

        private static bool IsAlpha(char c)
        {
            return (c >= 'a' && 'z' >= c) || (c >= 'A' && 'Z' <= c);
        }

        // Skips the input until newline is found. Newline is one of \r, \r\n or \n.
        private void SkipLine()
        {
            char c = GetChar();
            while (c != 0 && c != '\n' && c != '\r')
                c = GetChar();
        }

        // Reads a list. Note that '(' has already been read.
        private Obj ReadList()
        {
            Obj head = Nil;
            while (true)
            {
                Obj obj = Read();
                if (obj == null)
                    Error("Unclosed parenthesis");
                if (obj == Cparen)
                    return Reverse(head);
                if (obj == Dot)
                {
                    Obj last = Read();
                    if (Read() != Cparen)
                        Error("Closed parenthesis expected after dot");
                    Obj ret = Reverse(head);
                    (head).Rest = last;
                    return ret;
                }
                head = Join(obj, head);
            }
        }

        // May create a new symbol. If there's a symbol with the same name, it will not create a new symbol
        // but return the existing one.
        private Obj Intern(string name)
        {
            for (Obj p = _symbols; p != Nil; p = p.Rest)
                if (name == p.First.Name)
                    return p.First;
            Obj sym = MakeSymbol(name);
            _symbols = Join(sym, _symbols);
            return sym;
        }

        // Reader macro ' (single quote). It reads an expression and returns (quote <expr>).
        private Obj ReadQuote()
        {
            Obj sym = Intern("quote");
            return Join(sym, Join(Read(), Nil));
        }

        private int ReadNumber(int val)
        {
            while (IsDigit(Peek()))
                val = val * 10 + (GetChar() - '0');
            return val;
        }

        private Obj ReadSymbol(string s)
        {
            while (IsAlnum(Peek()) || SymbolChars.IndexOf(Peek()) > -1)
                s += GetChar();
            return Intern(s);
        }

        private Obj Read()
        {
            while (true)
            {
                char c = GetChar();
                if (c == '\t' || c == '\r' || c == '\n' || c == ' ')
                    continue;
                if (c == (char)0)
                    return null;
                if (c == ';')
                {
                    SkipLine();
                    continue;
                }
                if (c == '(')
                    return ReadList();
                if (c == ')')
                    return Cparen;
                if (c == '.')
                    return Dot;
                if (c == '\'')
                    return ReadQuote();
                if (IsDigit(c))
                    return MakeInt(ReadNumber(c - '0'));
                if (c == '-' && IsDigit(Peek()))
                    return MakeInt(-ReadNumber(0));
                if (IsAlpha(c) || SymbolChars.IndexOf(c) > -1)
                    return ReadSymbol(c.ToString());
                Error(string.Format("Don't know how to handle {0}", c));
            }
        }

        // Prints the given object.
        private void Print(Obj obj)
        {
            if (obj.Type == Tint)
                Print(obj.Value.ToString());
            else if (obj.Type == Tcell)
            {
                Print("(");
                while (true)
                {
                    Print(obj.First);
                    if (obj.Rest == Nil)
                        break;
                    if (obj.Rest.Type != Tcell)
                    {
                        Print(" . ");
                        Print(obj.Rest);
                        break;
                    }
                    Print(" ");
                    obj = obj.Rest;
                }
                Print(")");
            }
            else if (obj.Type == Tsymbol)
                Print(obj.Name);
            else if (obj.Type == Tprimitive)
                Print("<primitive>");
            else if (obj.Type == Tfunction)
                Print("<function>");
            else if (obj.Type == Tmacro)
                Print("<macro>");
            else if (obj.Type == Tnil)
                Print("()");
            else if (obj.Type == Ttrue)
                Print("t");
            else
                Error(string.Format("Bug: print: Unknown tag type: {0}", obj.Type));
        }

        private int ListLength(Obj list)
        {
            int len = 0;
            for (; list.Type == Tcell; list = list.Rest)
                len++;
            return list == Nil ? len : -1;
        }

        private static void AddVariable(Obj env, Obj sym, Obj val)
        {
            env.First = Acons(sym, val, env.First);
        }

        // Returns a newly created environment frame.
        private Obj PushEnv(Obj env, Obj vars, Obj values)
        {
            Obj map = Nil;
            for (; vars.Type == Tcell; vars = vars.Rest, values = values.Rest)
            {
                if (values.Type != Tcell)
                    Error("Cannot apply function: number of argument does not match");
                Obj sym = vars.First;
                Obj val = values.First;
                map = Acons(sym, val, map);
            }
            if (vars != Nil)
                map = Acons(vars, values, map);
            return MakeEnv(map, env);
        }

        // Evaluates the list elements from head and returns the last return value.
        private Obj Progn(Obj env, Obj list)
        {
            Obj r = null;
            for (Obj lp = list; lp != Nil; lp = lp.Rest)
                r = Eval(env, lp.First);
            return r;
        }

        // Evaluates all the list elements and returns their return values as a new list.
        private Obj EvalList(Obj env, Obj list)
        {
            Obj head = Nil;
            for (Obj lp = list; lp != Nil; lp = lp.Rest)
            {
                Obj expr = lp.First;
                Obj result = Eval(env, expr);
                head = Join(result, head);
            }
            return Reverse(head);
        }

        private bool IsList(Obj obj)
        {
            return obj == Nil || obj.Type == Tcell;
        }

        private Obj ApplyFunction(Obj env, Obj fn, Obj args)
        {
            Obj params_ = fn.First;
            Obj newenv = fn.Env;
            newenv = PushEnv(newenv, params_, args);
            Obj body = fn.Rest;
            return Progn(newenv, body);
        }

        // Apply fn with args.
        private Obj Apply(Obj env, Obj fn, Obj args)
        {
            if (!IsList(args))
                Error("argument must be a list");
            if (fn.Type == Tprimitive)
                return fn.Fn(env, args);
            if (fn.Type == Tfunction)
            {
                Obj eargs = EvalList(env, args);
                return ApplyFunction(env, fn, eargs);
            }
            Error("not supported");
            return Nil;
        }

        // Searches for a variable by symbol. Returns null if not found.
        private Obj Find(Obj env, Obj sym)
        {
            for (Obj p = env; p != Nil; p = p.Rest)
            {
                for (Obj cell = p.First; cell != Nil; cell = cell.Rest)
                {
                    Obj bind = cell.First;
                    if (sym == bind.First)
                        return bind;
                }
            }
            return null;
        }

        // Expands the given macro application form.
        private Obj MacroExpand(Obj env, Obj obj)
        {
            if (obj.Type != Tcell || obj.First.Type != Tsymbol)
                return obj;
            // Lookup the macro definition, if any
            Obj bind = Find(env, obj.First);
            if (bind == null || bind.Rest.Type != Tmacro)
                return obj;
            Obj macro = bind.Rest;
            Obj args = obj.Rest;
            return ApplyFunction(env, macro, args);
        }

        // Evaluates the S expression.
        private Obj Eval(Obj env, Obj obj)
        {
            while (true)
            {
                if (obj.Type == Tint || obj.Type == Tprimitive || obj.Type == Tfunction || obj.Type == Tnil || obj.Type == Ttrue)
                {
                    return obj; // Self-evaluating objects
                }
                if (obj.Type == Tsymbol)
                {
                    Obj bind = Find(env, obj); // Variable
                    if (bind == null)
                        Error(string.Format("Undefined symbol: {0}", obj.Name));
                    return bind.Rest;
                }
                if (obj.Type == Tcell)
                {
                    Obj expanded = MacroExpand(env, obj); // Function application form
                    if (expanded != obj)
                    {
                        obj = expanded;
                        continue;
                    }
                    Obj fn = obj.First;
                    fn = Eval(env, fn);
                    Obj args = (obj).Rest;
                    if (fn.Type != Tprimitive && fn.Type != Tfunction)
                        Error("The head of a list must be a function");
                    return Apply(env, fn, args);
                }

                Error(string.Format("Bug: eval: Unknown tag type: {0}", obj.Type));
                return Nil;
            }
        }

        // 'expr
        private Obj PrimQuote(Obj env, Obj list)
        {
            if (ListLength(list) != 1)
                Error("Malformed quote");
            return list.First;
        }

        // (join expr expr) (cons expr expr)
        private Obj PrimJoin(Obj env, Obj list)
        {
            if (ListLength(list) != 2)
                Error("Malformed join");
            Obj cell = EvalList(env, list);
            cell.Rest = cell.Rest.First;
            return cell;
        }

        // (first <cell>) (car <cell>)
        private Obj PrimFirst(Obj env, Obj list)
        {
            Obj args = EvalList(env, list);
            if (args.First.Type != Tcell || args.Rest != Nil)
                Error("Malformed first");
            return args.First.First;
        }

        // (rest <cell>) (cdr <cell>)
        private Obj PrimRest(Obj env, Obj list)
        {
            Obj args = EvalList(env, list);
            if (args.First.Type != Tcell || args.Rest != Nil)
                Error("Malformed rest");
            return args.First.Rest;
        }

        // (setq <symbol> expr)
        private Obj PrimSetq(Obj env, Obj list)
        {
            if (ListLength(list) != 2 || list.First.Type != Tsymbol)
                Error("Malformed setq");
            Obj bind = Find(env, list.First);
            if (bind == null)
                Error(string.Format("Unbound variable {0}", list.First.Name));
            return bind.Rest = Eval(env, list.Rest.First);
        }

        // (+ <integer> ...)
        private Obj PrimPlus(Obj env, Obj list)
        {
            int sum = 0;
            for (Obj args = EvalList(env, list); args != Nil; args = args.Rest)
            {
                if (args.First.Type != Tint)
                    Error("+ takes only numbers");
                sum += args.First.Value;
            }
            return MakeInt(sum);
        }

        // (- <integer> ...)
        private Obj PrimMinus(Obj env, Obj list)
        {
            Obj args = EvalList(env, list);
            for (Obj p = args; p != Nil; p = p.Rest)
                if (p.First.Type != Tint)
                    Error("- takes only numbers");
            int r = args.First.Value;
            if (args.Rest == Nil)
                return MakeInt(-r);
            for (Obj p = args.Rest; p != Nil; p = p.Rest)
                r -= p.First.Value;
            return MakeInt(r);
        }

        private Obj HandleFunction(Obj env, Obj list, int type)
        {
            if (list.Type != Tcell || !IsList(list.First) || list.Rest.Type != Tcell)
                Error("Malformed lambda");

            Obj p = list.First;
            for (; p.Type == Tcell; p = p.Rest)
                if (p.First.Type != Tsymbol)
                    Error("Parameter must be a symbol");
            if (p != Nil && p.Type != Tsymbol)
                Error("Parameter must be a symbol");

            return MakeFunction(type, list.First, list.Rest, env);
        }

        // (lambda (<symbol> ...) expr ...)
        private Obj PrimLambda(Obj env, Obj list)
        {
            return HandleFunction(env, list, Tfunction);
        }

        private Obj HandleDefun(Obj env, Obj list, int type)
        {
            if (list.First.Type != Tsymbol || list.Rest.Type != Tcell)
                Error("Malformed defun");
            Obj sym = list.First;
            Obj rest = list.Rest;
            Obj fn = HandleFunction(env, rest, type);
            AddVariable(env, sym, fn);
            return fn;
        }

        // (defun <symbol> (<symbol> ...) expr ...)
        private Obj PrimDefun(Obj env, Obj list)
        {
            return HandleDefun(env, list, Tfunction);
        }

        // (define <symbol> expr)
        private Obj PrimDefine(Obj env, Obj list)
        {
            if (ListLength(list) != 2 || list.First.Type != Tsymbol)
                Error("Malformed define");
            Obj sym = list.First;
            Obj value = Eval(env, list.Rest.First);
            AddVariable(env, sym, value);
            return value;
        }

        // (defmacro <symbol> (<symbol> ...) expr ...)
        private Obj PrimDefMacro(Obj env, Obj list)
        {
            return HandleDefun(env, list, Tmacro);
        }

        // (macroexpand expr)
        private Obj PrimMacroExpand(Obj env, Obj list)
        {
            if (ListLength(list) != 1)
                Error("Malformed macroexpand");
            return MacroExpand(env, list.First);
        }

        // (println expr)
        private Obj PrimPrintln(Obj env, Obj list)
        {
            Print(Eval(env, list.First));
            Print("\n");
            return Nil;
        }

        // (if expr expr expr ...)
        private Obj PrimIf(Obj env, Obj list)
        {
            if (ListLength(list) < 2)
                Error("Malformed if");
            if (Eval(env, list.First) != Nil)
                return Eval(env, list.Rest.First);

            Obj els = list.Rest.Rest;
            return els == Nil ? Nil : Progn(env, els);
        }

        // (while cond expr ...)
        private Obj PrimWhile(Obj env, Obj list)
        {
            if (ListLength(list) < 2)
                Error("Malformed while");
            while (Eval(env, list.First) != Nil)
                EvalList(env, list.Rest);

            return Nil;
        }

        // (gensym)
        private Obj PrimGenSym(Obj env, Obj list)
        {
            return MakeSymbol("G_" + _nextSymbol++);
        }

        // (setcar <cell> expr) (setfirst <cell> expr)
        private Obj PrimSetFirst(Obj env, Obj list)
        {
            Obj args = EvalList(env, list);
            if (ListLength(args) != 2 || args.First.Type != Tcell)
                Error("Malformed setcar");
            args.First.First = args.Rest.First;
            return args.First;
        }

        // (= <integer> <integer>)
        private Obj PrimNumEq(Obj env, Obj list)
        {
            if (ListLength(list) != 2)
                Error("Malformed =");
            Obj values = EvalList(env, list);
            Obj x = values.First;
            Obj y = values.Rest.First;
            if (x.Type != Tint || y.Type != Tint)
                Error("= only takes numbers");
            return x.Value == y.Value ? True : Nil;
        }

        // (< <integer> <integer>)
        private Obj PrimNumLt(Obj env, Obj list)
        {
            Obj args = EvalList(env, list);
            if (ListLength(args) != 2)
                Error("malformed <");
            Obj x = args.First;
            Obj y = args.Rest.First;
            if (x.Type != Tint || y.Type != Tint)
                Error("< takes only numbers");
            return x.Value < y.Value ? True : Nil;
        }

        // (eq expr expr)
        private Obj PrimEq(Obj env, Obj list)
        {
            if (ListLength(list) != 2)
                Error("Malformed eq");
            Obj values = EvalList(env, list);
            return values.First == values.Rest.First ? True : Nil;
        }

        private void DefineConstants(Obj env)
        {
            AddVariable(env, Intern("t"), True);
        }

        private void DefinePrimitives(Obj env)
        {
            AddVariable(env, Intern("quote"), MakePrimitive(PrimQuote));
            AddVariable(env, Intern("setq"), MakePrimitive(PrimSetq));
            AddVariable(env, Intern("+"), MakePrimitive(PrimPlus));
            AddVariable(env, Intern("-"), MakePrimitive(PrimMinus));
            AddVariable(env, Intern("define"), MakePrimitive(PrimDefine));
            AddVariable(env, Intern("defun"), MakePrimitive(PrimDefun));
            AddVariable(env, Intern("defmacro"), MakePrimitive(PrimDefMacro));
            AddVariable(env, Intern("macroexpand"), MakePrimitive(PrimMacroExpand));
            AddVariable(env, Intern("lambda"), MakePrimitive(PrimLambda));
            AddVariable(env, Intern("if"), MakePrimitive(PrimIf));
            AddVariable(env, Intern("="), MakePrimitive(PrimNumEq));
            AddVariable(env, Intern("println"), MakePrimitive(PrimPrintln));
            AddVariable(env, Intern("cons"), MakePrimitive(PrimJoin));
            AddVariable(env, Intern("car"), MakePrimitive(PrimFirst));
            AddVariable(env, Intern("cdr"), MakePrimitive(PrimRest));
            AddVariable(env, Intern("setcar"), MakePrimitive(PrimSetFirst));
            AddVariable(env, Intern("while"), MakePrimitive(PrimWhile));
            AddVariable(env, Intern("gensym"), MakePrimitive(PrimGenSym));
            AddVariable(env, Intern("<"), MakePrimitive(PrimNumLt));
            AddVariable(env, Intern("eq"), MakePrimitive(PrimEq));
        }

        public void Repl(Obj env)
        {
            env = env ?? Rootenv;
            while (true)
            {
                try
                {
                    Console.Write(": ");
                    string input = Console.ReadLine();
                    while (OpenBrackets(input) > 0)
                    {
                        Console.Write("> ");
                        input += "\n" + Console.ReadLine();
                    }
                    Print(Eval(input, env));
                    Print("\n");
                }
                catch
                { }
            }
        }

        public Obj Eval(string s, Obj env)
        {
            env = env ?? Rootenv;
            _currentSource = s;
            _currentPos = 0;

            Obj expr = Nil;
            while (_currentPos < _currentSource.Length)
            {
                expr = Read();
                if (expr == null)
                    return null;
                if (expr == Cparen)
                    Error("Stray close parenthesis");
                if (expr == Dot)
                    Error("Stray dot");
                expr = Eval(env, expr);
            }
            return expr;
        }

        public LittleLisp()
        {
            Nil = MakeSpecial(Tnil);
            Dot = MakeSpecial(Tdot);
            Cparen = MakeSpecial(Tcparen);
            True = MakeSpecial(Ttrue);
            _symbols = Nil;
            Rootenv = MakeEnv(Nil, null);
            DefineConstants(Rootenv);
            DefinePrimitives(Rootenv);
        }

        static void Main(string[] args)
        {
            try
            {
                if (args.Length == 1 && File.Exists(args[0]))
                    new LittleLisp().Eval(File.ReadAllText(args[0]), null);
                else if (args.Length == 0)
                    new LittleLisp().Repl(null);
                else
                    Console.WriteLine("Usage: littlelisp [file]\n");
            }
            catch { }
        }
    }
}
