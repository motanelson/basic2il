using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// BASIC -> .NET CIL compiler and console simulator.
// Usage: dotnet run -- input.bas output.il
// If output.il is omitted, Program.il is created in the current directory.

internal static class Program
{
    private static readonly (Regex Pattern, string Category)[] Forbidden =
    {
        (new Regex(@"\b(SCREEN|PSET|PRESET|LINE|CIRCLE|PAINT|DRAW|COLOR|PALETTE|VIEW|WINDOW|CLS|LOCATE)\b", RegexOptions.IgnoreCase), "Graphics"),
        (new Regex(@"\b(SIN|COS|TAN|ATN|ASIN|ACOS|SEC|CSC|COT)\b", RegexOptions.IgnoreCase), "Trigonometry"),
        (new Regex(@"\b(DATE\$|TIME\$|TIMER|NOW|DATE|TIME)\b", RegexOptions.IgnoreCase), "Date/Time"),
        (new Regex(@"\b(RND|RANDOMIZE)\b", RegexOptions.IgnoreCase), "Random")
    };

    private static readonly Dictionary<string, string> Presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["primes"] = """
            ' Prime Number Finder in Label-Based BASIC
            DIM maxLimit AS INTEGER
            DIM candidate AS INTEGER
            DIM divisor AS INTEGER
            DIM isPrime AS INTEGER
            PRINT "=== Prime Numbers Generator ==="
            maxLimit = 30
            PRINT "Searching primes up to "; maxLimit
            FOR candidate = 2 TO maxLimit
                isPrime = 1
                FOR divisor = 2 TO candidate - 1
                    IF (candidate MOD divisor) = 0 THEN
                        isPrime = 0
                        GOTO EXIT_INNER
                    END IF
                NEXT divisor
            EXIT_INNER:
                IF isPrime = 1 THEN
                    PRINT candidate; " is PRIME"
                END IF
            NEXT candidate
            PRINT "Prime search complete."
            END
            """,
        ["factorial"] = """
            ' Factorial & Subroutine Routine using Labels
            DIM num AS INTEGER
            DIM result AS INTEGER
            DIM counter AS INTEGER
            PRINT "--- Factorial Calculation ---"
            num = 6
            result = 1
            counter = 1
            GOSUB COMPUTE_FACT
            PRINT "Factorial of "; num; " is "; result
            GOTO DONE
            COMPUTE_FACT:
            FACT_LOOP:
                IF counter > num THEN
                    RETURN
                END IF
                result = result * counter
                counter = counter + 1
                GOTO FACT_LOOP
            DONE:
            PRINT "Execution finished normally."
            END
            """,
        ["strings"] = """
            ' String Manipulation & Palindrome Inspector
            DIM text AS STRING
            DIM reversed AS STRING
            DIM n AS INTEGER
            DIM i AS INTEGER
            DIM ch AS STRING
            text = "RADAR"
            n = LEN(text)
            reversed = ""
            PRINT "Inspecting string: "; text
            FOR i = n TO 1 STEP -1
                ch = MID$(text, i, 1)
                reversed = reversed + ch
            NEXT i
            PRINT "Reversed: "; reversed
            IF text = reversed THEN
                PRINT "Result: The text IS a palindrome."
            ELSE
                PRINT "Result: The text is NOT a palindrome."
            END IF
            END
            """,
        ["forbidden_test"] = """
            ' Intentional compliance errors
            10 PRINT "Line number test"
            DIM r AS DOUBLE
            r = RND(1)
            DIM s AS DOUBLE
            s = SIN(1.57)
            DIM curDate AS STRING
            curDate = DATE$
            CIRCLE (100, 100), 50
            """
    };

    private static int Main(string[] args)
    {
        Console.BackgroundColor = ConsoleColor.White;
        Console.ForegroundColor= ConsoleColor.Black;
        Console.Clear();
        try
        {
            if (args.Length == 0)
            {
                Console.WriteLine("BASIC -> .NET CIL Compiler");
                Console.WriteLine("Usage: dotnet run -- <input.bas|preset:primes|factorial|strings|forbidden_test> [output.il]");
                Console.WriteLine("Example: dotnet run -- program.bas Program.il");
                return 1;
            }

            string source;
            string inputName = args[0];
            if (inputName.StartsWith("preset:", StringComparison.OrdinalIgnoreCase))
            {
                string key = inputName.Substring(7);
                if (!Presets.TryGetValue(key, out source!))
                    throw new ArgumentException($"Unknown preset '{key}'. Available: {string.Join(", ", Presets.Keys)}");
                Console.WriteLine($"Loaded preset: {key}");
            }
            else
            {
                if (!File.Exists(inputName))
                    throw new FileNotFoundException("Input BASIC file was not found.", inputName);
                source = File.ReadAllText(inputName);
            }

            string outputPath = args.Length > 1 ? args[1] : "Program.il";
            CompilationResult result = BasicCompiler.Compile(source);

            Console.WriteLine();
            Console.WriteLine("=== BASIC -> .NET CIL COMPILER ===");
            Console.WriteLine($"Input : {inputName}");
            Console.WriteLine($"Output: {Path.GetFullPath(outputPath)}");
            Console.WriteLine($"Lines : {source.Replace("\r\n", "\n").Split('\n').Length}");
            Console.WriteLine();

            if (result.Errors.Count > 0)
            {
                //Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Compilation failed: {result.Errors.Count} error(s).");
                //Console.ResetColor();
                foreach (var error in result.Errors)
                    Console.WriteLine($"[Line {error.Line}] {error.Message}");
                Console.WriteLine("No .il file was generated.");
                return 2;
            }

            File.WriteAllText(outputPath, result.IlText, new UTF8Encoding(false));
            //Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Compilation successful: 0 errors, 0 warnings.");
            //Console.ResetColor();
            Console.WriteLine($"CIL file saved: {Path.GetFullPath(outputPath)}");
            Console.WriteLine($"OpCodes emitted: {result.OpCount}");

            Console.WriteLine();
            Console.WriteLine("=== CLR LOCAL SYMBOL TABLE ===");
            if (result.Variables.Count == 0) Console.WriteLine("No local variables.");
            foreach (var pair in result.Variables.OrderBy(p => p.Value.Index))
                Console.WriteLine($"[{pair.Value.Index}] {pair.Key} -> {pair.Value.IlType}");
            Console.WriteLine();
            Console.WriteLine("=== USER LABELS ===");
            if (result.Labels.Count == 0) Console.WriteLine("No user labels found.");
            foreach (string label in result.Labels.OrderBy(x => x))
                Console.WriteLine(label);

            Console.WriteLine();
            Console.WriteLine("=== GENERATED .NET CIL ===");
            Console.WriteLine(result.IlText);

            Console.WriteLine();
            Console.WriteLine("=== RUNTIME SIMULATION ===");
            Console.WriteLine("The simulator executes the BASIC source (not the generated assembly).");
            try
            {
                var vm = new BasicInterpreter(source, Console.In, Console.Out);
                vm.Run();
                Console.WriteLine("Process exited with code 0 (0x0).");
            }
            catch (Exception ex)
            {
                //Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Runtime Exception] {ex.Message}");
                //Console.ResetColor();
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error: " + ex.Message);
            return 1;
        }
    }

    private sealed class CompilationResult
    {
        public string IlText { get; init; } = "";
        public Dictionary<string, VariableInfo> Variables { get; init; } = new();
        public HashSet<string> Labels { get; init; } = new();
        public List<CompileError> Errors { get; init; } = new();
        public int OpCount { get; init; }
    }

    private sealed record CompileError(int Line, string Message);
    private sealed record VariableInfo(string IlType, int Index);

    private static class BasicCompiler
    {
        public static CompilationResult Compile(string source)
        {
            var errors = Validate(source);
            if (errors.Count != 0)
                return new CompilationResult { Errors = errors };

            var variables = new Dictionary<string, VariableInfo>(StringComparer.OrdinalIgnoreCase);
            var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            // First pass: declarations and labels.
            foreach (string original in lines)
            {
                string line = Clean(original).Trim();
                var label = Regex.Match(line, @"^([A-Za-z_][A-Za-z0-9_]*)\s*:$");
                if (label.Success) labels.Add(label.Groups[1].Value.ToUpperInvariant());

                var dim = Regex.Match(line, @"^DIM\s+([A-Za-z_][A-Za-z0-9_$#]*)(?:\s+AS\s+(INTEGER|DOUBLE|STRING))?$", RegexOptions.IgnoreCase);
                if (dim.Success)
                {
                    string name = dim.Groups[1].Value.ToUpperInvariant();
                    string type = dim.Groups[2].Success ? dim.Groups[2].Value.ToUpperInvariant() :
                        name.EndsWith("$") ? "STRING" : name.EndsWith("#") ? "DOUBLE" : "INTEGER";
                    AddVariable(variables, name, type);
                }
            }

            var body = new List<string>();
            var forStack = new Stack<(string Var, string Step, string Start, string Exit)>();
            var ifStack = new Stack<(string FalseLabel, string EndLabel, bool HasElse)>();
            int unique = 0;
            int gosubReturnId = 0;

            for (int lineNo = 0; lineNo < lines.Length; lineNo++)
            {
                string text = Clean(lines[lineNo]).Trim();
                if (text.Length == 0 || Regex.IsMatch(text, @"^DIM\b", RegexOptions.IgnoreCase)) continue;

                var label = Regex.Match(text, @"^([A-Za-z_][A-Za-z0-9_]*)\s*:$");
                if (label.Success) { body.Add(label.Groups[1].Value.ToUpperInvariant() + ":"); continue; }

                if (Regex.IsMatch(text, @"^PRINT\b", RegexOptions.IgnoreCase))
                {
                    string rest = text.Length > 5 ? text.Substring(5).Trim() : "";
                    if (rest.Length == 0) { body.Add("call void [mscorlib]System.Console::WriteLine()"); continue; }
                    var parts = SplitTopLevel(rest, ';', ',');
                    for (int i = 0; i < parts.Count; i++)
                    {
                        string expr = parts[i].Text.Trim();
                        if (expr.Length == 0) continue;
                        EmitExpression(expr, body, variables);
                        bool newline = i == parts.Count - 1 && !rest.TrimEnd().EndsWith(";") && !rest.TrimEnd().EndsWith(",");
                        string type = InferType(expr, variables);
                        body.Add($"call void [mscorlib]System.Console::{(newline ? "WriteLine" : "Write")}({type})");
                    }
                    continue;
                }

                if (Regex.IsMatch(text, @"^INPUT\b", RegexOptions.IgnoreCase))
                {
                    string rest = text.Substring(5).Trim();
                    string prompt = "? ";
                    string target = rest;
                    var m = Regex.Match(rest, "^\"([^\"]*)\"\\s*[,;]\\s*(.+)$");
                    if (m.Success) { prompt = m.Groups[1].Value; target = m.Groups[2].Value.Trim(); }
                    var v = EnsureVariable(variables, target, null);
                    body.Add($"ldstr {IlString(prompt)}");
                    body.Add("call void [mscorlib]System.Console::Write(string)");
                    body.Add("call string [mscorlib]System.Console::ReadLine()");
                    if (v.IlType == "int32") body.Add("call int32 [mscorlib]System.Int32::Parse(string)");
                    else if (v.IlType == "float64") body.Add("call float64 [mscorlib]System.Double::Parse(string)");
                    body.Add($"stloc {v.Index} // {target.ToUpperInvariant()}");
                    continue;
                }

                var go = Regex.Match(text, @"^GOTO\s+([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.IgnoreCase);
                if (go.Success) { body.Add("br " + go.Groups[1].Value.ToUpperInvariant()); continue; }

                var gosub = Regex.Match(text, @"^GOSUB\s+([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.IgnoreCase);
                if (gosub.Success)
                {
                    // GOSUB pushes a continuation ID and branches to the BASIC label.
                    int returnId = gosubReturnId++;
                    body.Add($"GOSUB_PUSH {returnId}");
                    body.Add("br " + gosub.Groups[1].Value.ToUpperInvariant());
                    body.Add($"RET_{returnId}:");
                    continue;
                }

                if (Regex.IsMatch(text, @"^RETURN$", RegexOptions.IgnoreCase))
                {
                    body.Add("GOSUB_RETURN");
                    continue;
                }
                if (Regex.IsMatch(text, @"^END$", RegexOptions.IgnoreCase)) { body.Add("ret"); continue; }

                var fm = Regex.Match(text, @"^FOR\s+([A-Za-z_][A-Za-z0-9_$#]*)\s*=\s*(.+?)\s+TO\s+(.+?)(?:\s+STEP\s+(.+))?$", RegexOptions.IgnoreCase);
                if (fm.Success)
                {
                    string name = fm.Groups[1].Value;
                    string start = fm.Groups[2].Value.Trim();
                    string end = fm.Groups[3].Value.Trim();
                    string step = fm.Groups[4].Success ? fm.Groups[4].Value.Trim() : "1";
                    var v = EnsureVariable(variables, name, start);
                    EmitExpression(start, body, variables);
                    body.Add($"stloc {v.Index} // {name.ToUpperInvariant()}");
                    string startLabel = "IL_FOR_" + unique++;
                    string exitLabel = "IL_FOREXIT_" + unique++;
                    body.Add(startLabel + ":");
                    body.Add($"ldloc {v.Index}");
                    EmitExpression(end, body, variables);
                    body.Add(step.StartsWith("-", StringComparison.Ordinal) ? $"blt {exitLabel}" : $"bgt {exitLabel}");
                    forStack.Push((name, step, startLabel, exitLabel));
                    continue;
                }

                var nm = Regex.Match(text, @"^NEXT(?:\s+([A-Za-z_][A-Za-z0-9_$#]*))?$", RegexOptions.IgnoreCase);
                if (nm.Success)
                {
                    if (forStack.Count == 0) { errors.Add(new CompileError(lineNo + 1, "NEXT without matching FOR.")); continue; }
                    var loop = forStack.Pop();
                    var v = EnsureVariable(variables, loop.Var, null);
                    body.Add($"ldloc {v.Index}");
                    EmitExpression(loop.Step, body, variables);
                    body.Add("add");
                    body.Add($"stloc {v.Index}");
                    body.Add("br " + loop.Start);
                    body.Add(loop.Exit + ":");
                    continue;
                }

                var blockIf = Regex.Match(text, @"^IF\s+(.+?)\s+THEN\s*$", RegexOptions.IgnoreCase);
                if (blockIf.Success)
                {
                    string falseLabel = "IL_ELSE_" + unique++;
                    string endLabel = "IL_ENDIF_" + unique++;
                    EmitCondition(blockIf.Groups[1].Value.Trim(), falseLabel, body, variables);
                    ifStack.Push((falseLabel, endLabel, false));
                    continue;
                }
                if (Regex.IsMatch(text, @"^ELSE$", RegexOptions.IgnoreCase))
                {
                    if (ifStack.Count == 0) { errors.Add(new CompileError(lineNo + 1, "ELSE without matching IF.")); continue; }
                    var frame = ifStack.Pop();
                    body.Add("br " + frame.EndLabel);
                    body.Add(frame.FalseLabel + ":");
                    ifStack.Push((frame.FalseLabel, frame.EndLabel, true));
                    continue;
                }
                if (Regex.IsMatch(text, @"^END\s*IF$", RegexOptions.IgnoreCase))
                {
                    if (ifStack.Count == 0) { errors.Add(new CompileError(lineNo + 1, "END IF without matching IF.")); continue; }
                    var frame = ifStack.Pop();
                    body.Add((frame.HasElse ? frame.EndLabel : frame.FalseLabel) + ":");
                    continue;
                }

                var ifm = Regex.Match(text, @"^IF\s+(.+?)\s+THEN\s*(.*)$", RegexOptions.IgnoreCase);
                if (ifm.Success && ifm.Groups[2].Value.Trim().Length > 0)
                {
                    string condition = ifm.Groups[1].Value.Trim();
                    string rest = ifm.Groups[2].Value.Trim();
                    string[] branches = Regex.Split(rest, @"\s+ELSE\s+", RegexOptions.IgnoreCase);
                    string falseLabel = "IL_ELSE_" + unique++;
                    string endLabel = "IL_ENDIF_" + unique++;
                    EmitCondition(condition, falseLabel, body, variables);
                    EmitSingleStatement(branches[0], body, variables);
                    if (branches.Length > 1)
                    {
                        body.Add("br " + endLabel);
                        body.Add(falseLabel + ":");
                        EmitSingleStatement(branches[1], body, variables);
                        body.Add(endLabel + ":");
                    }
                    else body.Add(falseLabel + ":");
                    continue;
                }

                string assignment = Regex.Replace(text, @"^LET\s+", "", RegexOptions.IgnoreCase);
                int eq = FindTopLevel(assignment, '=');
                if (eq >= 0)
                {
                    string name = assignment.Substring(0, eq).Trim();
                    string expr = assignment.Substring(eq + 1).Trim();
                    var v = EnsureVariable(variables, name, expr);
                    EmitExpression(expr, body, variables);
                    body.Add($"stloc {v.Index} // {name.ToUpperInvariant()}");
                    continue;
                }

                errors.Add(new CompileError(lineNo + 1, $"Unrecognized syntax: \"{text}\""));
            }

            if (forStack.Count > 0) errors.Add(new CompileError(lines.Length, "FOR without matching NEXT."));
            if (ifStack.Count > 0) errors.Add(new CompileError(lines.Length, "IF without matching END IF."));
            foreach (string target in ReferencedLabels(source))
                if (!labels.Contains(target)) errors.Add(new CompileError(0, $"Undefined label: {target}"));
            if (errors.Count > 0) return new CompilationResult { Errors = errors };

            body.Add("ret");
            string il = RenderAssembly(body, variables);
            return new CompilationResult
            {
                IlText = il,
                Variables = variables,
                Labels = labels,
                OpCount = body.Count(x => !x.EndsWith(":"))
            };
        }

        private static List<CompileError> Validate(string source)
        {
            var errors = new List<CompileError>();
            string[] lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string code = Clean(lines[i]);
                if (Regex.IsMatch(code, @"^\s*\d+(\s+.*|$)"))
                {
                    string number = Regex.Match(code, @"^\s*\d+").Value.Trim();
                    errors.Add(new CompileError(i + 1, $"Line numbers are not permitted (\"{number}\"). Use labels instead (e.g. 'MY_LABEL:')."));
                }
                foreach (var rule in Forbidden)
                {
                    var match = rule.Pattern.Match(code);
                    if (match.Success)
                        errors.Add(new CompileError(i + 1, $"Forbidden syntax detected: {rule.Category} operation/function \"{match.Value}\" is prohibited by compiler policy."));
                }
            }
            return errors;
        }

        private static string Clean(string line)
        {
            int quote = -1;
            bool inString = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"') inString = !inString;
                if (!inString && line[i] == '\'') { quote = i; break; }
            }
            string result = quote >= 0 ? line.Substring(0, quote) : line;
            return Regex.Replace(result, @"\bREM\b.*$", "", RegexOptions.IgnoreCase);
        }

        private static void AddVariable(Dictionary<string, VariableInfo> vars, string name, string type)
        {
            if (vars.ContainsKey(name)) return;
            string il = type == "STRING" ? "string" : type == "DOUBLE" ? "float64" : "int32";
            vars[name] = new VariableInfo(il, vars.Count);
        }

        private static VariableInfo EnsureVariable(Dictionary<string, VariableInfo> vars, string name, string? inferred)
        {
            string key = name.Trim().ToUpperInvariant();
            if (vars.TryGetValue(key, out var v)) return v;
            string type = key.EndsWith("$") || (inferred != null && inferred.Trim().StartsWith("\"")) ? "STRING" :
                key.EndsWith("#") || (inferred != null && Regex.IsMatch(inferred, @"\d+\.\d+")) ? "DOUBLE" : "INTEGER";
            AddVariable(vars, key, type);
            return vars[key];
        }

        private static string InferType(string expr, Dictionary<string, VariableInfo> vars)
        {
            expr = expr.Trim();
            if (expr.StartsWith("\"") || expr.Contains("\"") || Regex.IsMatch(expr, @"^(MID\$|LEFT\$|RIGHT\$|UCASE\$|LCASE\$)", RegexOptions.IgnoreCase)) return "string";
            if (Regex.IsMatch(expr, @"^LEN\s*\(", RegexOptions.IgnoreCase)) return "int32";
            if (vars.TryGetValue(expr.ToUpperInvariant(), out var v)) return v.IlType;
            return Regex.IsMatch(expr, @"\d+\.\d+") ? "float64" : "int32";
        }

        private static void EmitExpression(string expression, List<string> output, Dictionary<string, VariableInfo> vars)
        {
            var p = new ExpressionParser(expression);
            ExprNode node = p.Parse();
            EmitNode(node, output, vars);
        }

        private abstract record ExprNode;
        private sealed record NumberNode(string Value) : ExprNode;
        private sealed record StringNode(string Value) : ExprNode;
        private sealed record VariableNode(string Name) : ExprNode;
        private sealed record BinaryNode(string Op, ExprNode Left, ExprNode Right) : ExprNode;
        private sealed record FunctionNode(string Name, List<ExprNode> Args) : ExprNode;

        private sealed class ExpressionParser
        {
            private readonly List<string> tokens;
            private int pos;
            public ExpressionParser(string source) { tokens = Tokenize(source); }
            public ExprNode Parse()
            {
                ExprNode node = ParseAdd();
                if (pos < tokens.Count) throw new FormatException($"Unexpected token '{tokens[pos]}' in expression.");
                return node;
            }
            private ExprNode ParseAdd()
            {
                ExprNode n = ParseMul();
                while (Peek("+") || Peek("-"))
                {
                    string op = Take();
                    n = new BinaryNode(op, n, ParseMul());
                }
                return n;
            }
            private ExprNode ParseMul()
            {
                ExprNode n = ParseUnary();
                while (Peek("*") || Peek("/") || PeekWord("MOD"))
                {
                    string op = Take().ToUpperInvariant();
                    n = new BinaryNode(op, n, ParseUnary());
                }
                return n;
            }
            private ExprNode ParseUnary()
            {
                if (Peek("-")) { Take(); return new BinaryNode("-", new NumberNode("0"), ParseUnary()); }
                return ParsePrimary();
            }
            private ExprNode ParsePrimary()
            {
                if (Peek("("))
                {
                    Take(); ExprNode n = ParseAdd(); Expect(")"); return n;
                }
                if (pos >= tokens.Count) throw new FormatException("Incomplete expression.");
                string t = Take();
                if (t.StartsWith("\"")) return new StringNode(t.Substring(1, t.Length - 2));
                if (Regex.IsMatch(t, @"^\d+(\.\d+)?$")) return new NumberNode(t);
                if (Peek("("))
                {
                    Take();
                    var args = new List<ExprNode>();
                    if (!Peek(")"))
                    {
                        do { args.Add(ParseAdd()); if (!Peek(",")) break; Take(); } while (true);
                    }
                    Expect(")");
                    return new FunctionNode(t.ToUpperInvariant(), args);
                }
                return new VariableNode(t);
            }
            private bool Peek(string s) => pos < tokens.Count && tokens[pos] == s;
            private bool PeekWord(string s) => pos < tokens.Count && tokens[pos].Equals(s, StringComparison.OrdinalIgnoreCase);
            private string Take() => tokens[pos++];
            private void Expect(string s) { if (!Peek(s)) throw new FormatException($"Expected '{s}'."); Take(); }
            private static List<string> Tokenize(string s)
            {
                var list = new List<string>();
                for (int i = 0; i < s.Length;)
                {
                    if (char.IsWhiteSpace(s[i])) { i++; continue; }
                    if (s[i] == '"')
                    {
                        int start = i++;
                        while (i < s.Length && s[i] != '"') i++;
                        if (i >= s.Length) throw new FormatException("Unterminated string literal.");
                        i++; list.Add(s.Substring(start, i - start)); continue;
                    }
                    if (char.IsLetter(s[i]) || s[i] == '_' || s[i] == '$' || s[i] == '#')
                    {
                        int start = i++;
                        while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_' || s[i] == '$' || s[i] == '#')) i++;
                        list.Add(s.Substring(start, i - start)); continue;
                    }
                    if (char.IsDigit(s[i]) || s[i] == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1]))
                    {
                        int start = i++;
                        while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                        list.Add(s.Substring(start, i - start)); continue;
                    }
                    if ("()+-*/, ".Contains(s[i]) && s[i] != ' ') { list.Add(s[i++].ToString()); continue; }
                    throw new FormatException($"Invalid character '{s[i]}' in expression.");
                }
                return list;
            }
        }

        private static void EmitNode(ExprNode node, List<string> output, Dictionary<string, VariableInfo> vars)
        {
            switch (node)
            {
                case NumberNode n:
                    if (n.Value.Contains('.')) output.Add("ldc.r8 " + n.Value);
                    else
                    {
                        int value = int.Parse(n.Value, CultureInfo.InvariantCulture);
                        output.Add(value >= 0 && value <= 8 ? "ldc.i4." + value : "ldc.i4 " + value);
                    }
                    break;
                case StringNode s: output.Add("ldstr " + IlString(s.Value)); break;
                case VariableNode v:
                    var info = EnsureVariable(vars, v.Name, null);
                    output.Add($"ldloc {info.Index} // {v.Name.ToUpperInvariant()}");
                    break;
                case BinaryNode b:
                    if (b.Op == "+" && IsStringNode(b.Left, vars) && IsStringNode(b.Right, vars))
                    {
                        EmitNode(b.Left, output, vars); EmitNode(b.Right, output, vars);
                        output.Add("call string [mscorlib]System.String::Concat(string, string)");
                    }
                    else
                    {
                        EmitNode(b.Left, output, vars); EmitNode(b.Right, output, vars);
                        output.Add(b.Op.ToUpperInvariant() switch { "+" => "add", "-" => "sub", "*" => "mul", "/" => "div", "MOD" => "rem", _ => throw new FormatException("Unsupported operator.") });
                    }
                    break;
                case FunctionNode f:
                    if (f.Name == "LEN" && f.Args.Count == 1)
                    {
                        EmitNode(f.Args[0], output, vars);
                        output.Add("callvirt instance int32 [mscorlib]System.String::get_Length()");
                    }
                    else if (f.Name == "MID$" && f.Args.Count is 2 or 3)
                    {
                        EmitNode(f.Args[0], output, vars);
                        EmitNode(f.Args[1], output, vars);
                        output.Add("ldc.i4.1"); output.Add("sub // convert 1-based start to 0-based");
                        if (f.Args.Count == 3)
                        {
                            EmitNode(f.Args[2], output, vars);
                            output.Add("callvirt instance string [mscorlib]System.String::Substring(int32, int32)");
                        }
                        else output.Add("callvirt instance string [mscorlib]System.String::Substring(int32)");
                    }
                    else throw new FormatException($"Unsupported function or argument count: {f.Name}.");
                    break;
            }
        }

        private static bool IsStringNode(ExprNode node, Dictionary<string, VariableInfo> vars) =>
            node is StringNode || node is VariableNode v && vars.TryGetValue(v.Name.ToUpperInvariant(), out var info) && info.IlType == "string";

        private static void EmitCondition(string condition, string falseLabel, List<string> body, Dictionary<string, VariableInfo> vars)
        {
            var m = Regex.Match(condition, @"(<=|>=|<>|<|>|=)");
            if (!m.Success)
            {
                EmitExpression(condition, body, vars); body.Add("brfalse " + falseLabel); return;
            }
            string left = condition.Substring(0, m.Index).Trim();
            string right = condition.Substring(m.Index + m.Length).Trim();
            EmitExpression(left, body, vars); EmitExpression(right, body, vars);
            string op = m.Value switch
            {
                "=" => "bne.un", "<>" => "beq", "<" => "bge", "<=" => "bgt", ">" => "ble", ">=" => "blt", _ => "bne.un"
            };
            body.Add(op + " " + falseLabel);
        }

        private static void EmitSingleStatement(string stmt, List<string> body, Dictionary<string, VariableInfo> vars)
        {
            stmt = stmt.Trim();
            var go = Regex.Match(stmt, @"^GOTO\s+([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.IgnoreCase);
            if (go.Success) { body.Add("br " + go.Groups[1].Value.ToUpperInvariant()); return; }
            if (Regex.IsMatch(stmt, @"^PRINT\b", RegexOptions.IgnoreCase))
            {
                string expr = stmt.Substring(5).Trim();
                EmitExpression(expr, body, vars);
                body.Add($"call void [mscorlib]System.Console::WriteLine({InferType(expr, vars)})");
                return;
            }
            int eq = FindTopLevel(stmt, '=');
            if (eq >= 0)
            {
                string name = stmt.Substring(0, eq).Trim(), expr = stmt.Substring(eq + 1).Trim();
                var v = EnsureVariable(vars, name, expr);
                EmitExpression(expr, body, vars); body.Add($"stloc {v.Index} // {name.ToUpperInvariant()}");
                return;
            }
            throw new FormatException($"Unsupported single-line IF statement: {stmt}");
        }

        private static string RenderAssembly(List<string> body, Dictionary<string, VariableInfo> vars)
        {
            var b = new StringBuilder();
            b.AppendLine("// ====================================================================");
            b.AppendLine("// Microsoft .NET CIL Assembly Representation");
            b.AppendLine("// Generated from Label-Based BASIC Source Document");
            b.AppendLine("// GOSUB/RETURN use a local integer array as a return-address stack");
            b.AppendLine("// ====================================================================");
            b.AppendLine();
            b.AppendLine(".assembly extern mscorlib");
            b.AppendLine("{");
            b.AppendLine("  .publickeytoken = (B7 7A 5C 56 19 34 E0 89)");
            b.AppendLine("  .ver 4:0:0:0");
            b.AppendLine("}");
            b.AppendLine(".assembly BasicApplication");
            b.AppendLine("{");
            b.AppendLine("  .ver 1:0:0:0");
            b.AppendLine("}");
            b.AppendLine(".module BasicApplication.exe");
            b.AppendLine(".subsystem 0x0003  // WINDOWS_CUI (Console)");
            b.AppendLine(".corflags 0x00000001  // ILONLY");
            b.AppendLine();
            b.AppendLine(".class public auto ansi beforefieldinit Program");
            b.AppendLine("       extends [mscorlib]System.Object");
            b.AppendLine("{");
            b.AppendLine("  .method public static void Main() cil managed");
            b.AppendLine("  {");
            b.AppendLine("    .entrypoint");
            b.AppendLine("    .maxstack 16");

            int[] returnIds = body
                .Where(x => x.StartsWith("GOSUB_PUSH ", StringComparison.Ordinal))
                .Select(x => int.Parse(x.Substring("GOSUB_PUSH ".Length), CultureInfo.InvariantCulture))
                .Distinct().OrderBy(x => x).ToArray();
            bool hasGosub = returnIds.Length > 0;
            int stackLocal = vars.Count;
            int stackPointerLocal = vars.Count + 1;
            if (vars.Count > 0 || hasGosub)
            {
                b.AppendLine("    .locals init (");
                var localDeclarations = new List<string>();
                foreach (var pair in vars.OrderBy(p => p.Value.Index))
                    localDeclarations.Add($"      [{pair.Value.Index}] {pair.Value.IlType} {pair.Key.ToLowerInvariant()}");
                if (hasGosub)
                {
                    // Avoid System.Collections.Generic.Stack`1 for older CLR implementations.
                    localDeclarations.Add($"      [{stackLocal}] int32[] gosubReturns");
                    localDeclarations.Add($"      [{stackPointerLocal}] int32 gosubReturnTop");
                }
                for (int i = 0; i < localDeclarations.Count; i++)
                    b.AppendLine(localDeclarations[i] + (i + 1 < localDeclarations.Count ? "," : ""));
                b.AppendLine("    )");
            }
            if (hasGosub)
            {
                // Allocate a fixed return stack (1024 nested GOSUB calls).
                b.AppendLine("    ldc.i4 1024");
                b.AppendLine("    newarr [mscorlib]System.Int32");
                b.AppendLine($"    stloc {stackLocal}");
                b.AppendLine("    ldc.i4.0");
                b.AppendLine($"    stloc {stackPointerLocal}");
            }

            foreach (string line in body)
            {
                if (line.StartsWith("GOSUB_PUSH ", StringComparison.Ordinal))
                {
                    string id = line.Substring("GOSUB_PUSH ".Length);
                    // Push the continuation ID into the local integer array.
                    b.AppendLine($"    ldloc {stackLocal}");
                    b.AppendLine($"    ldloc {stackPointerLocal}");
                    b.AppendLine($"    ldc.i4 {id}");
                    b.AppendLine("    stelem.i4");
                    b.AppendLine($"    ldloc {stackPointerLocal}");
                    b.AppendLine("    ldc.i4.1");
                    b.AppendLine("    add");
                    b.AppendLine($"    stloc {stackPointerLocal}");
                }
                else if (line == "GOSUB_RETURN")
                {
                    // Pop the last continuation ID and dispatch to its RET_n label.
                    b.AppendLine($"    ldloc {stackPointerLocal}");
                    b.AppendLine("    brfalse GOSUB_INVALID_RETURN");
                    b.AppendLine($"    ldloc {stackPointerLocal}");
                    b.AppendLine("    ldc.i4.1");
                    b.AppendLine("    sub");
                    b.AppendLine($"    stloc {stackPointerLocal}");
                    b.AppendLine($"    ldloc {stackLocal}");
                    b.AppendLine($"    ldloc {stackPointerLocal}");
                    b.AppendLine("    ldelem.i4");
                    b.AppendLine("    switch (" + string.Join(", ", returnIds.Select(x => "RET_" + x)) + ")");
                    b.AppendLine("    br GOSUB_INVALID_RETURN");
                }
                else b.AppendLine(line.EndsWith(":") ? "  " + line : "    " + line);
            }
            if (hasGosub)
            {
                b.AppendLine("  GOSUB_INVALID_RETURN:");
                b.AppendLine("    newobj instance void [mscorlib]System.InvalidOperationException::.ctor()");
                b.AppendLine("    throw");
            }
            b.AppendLine("  } // end of method Program::Main");
            b.AppendLine("}");
            return b.ToString();
        }

        private static string IlString(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        private static List<(string Text, char? Delimiter)> SplitTopLevel(string text, params char[] separators)
        {
            var result = new List<(string, char?)>();
            bool quote = false; int depth = 0, start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '"') quote = !quote;
                else if (!quote && text[i] == '(') depth++;
                else if (!quote && text[i] == ')') depth--;
                else if (!quote && depth == 0 && separators.Contains(text[i]))
                {
                    result.Add((text.Substring(start, i - start), text[i])); start = i + 1;
                }
            }
            if (start < text.Length) result.Add((text.Substring(start), null));
            return result;
        }

        private static int FindTopLevel(string text, char target)
        {
            bool quote = false; int depth = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '"') quote = !quote;
                else if (!quote && text[i] == '(') depth++;
                else if (!quote && text[i] == ')') depth--;
                else if (!quote && depth == 0 && text[i] == target) return i;
            }
            return -1;
        }

        private static IEnumerable<string> ReferencedLabels(string source)
        {
            foreach (Match m in Regex.Matches(source, @"\b(?:GOTO|GOSUB)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase))
                yield return m.Groups[1].Value.ToUpperInvariant();
        }
    }

    private sealed class BasicInterpreter
    {
        private readonly string[] lines;
        private readonly TextReader input;
        private readonly TextWriter output;
        private readonly Dictionary<string, object?> vars = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> labels = new(StringComparer.OrdinalIgnoreCase);
        private readonly Stack<int> returns = new();
        private readonly Stack<ForFrame> loops = new();
        private sealed record ForFrame(string Name, double End, double Step, int StartLine);

        public BasicInterpreter(string source, TextReader input, TextWriter output)
        {
            lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            this.input = input; this.output = output;
            for (int i = 0; i < lines.Length; i++)
            {
                string t = Clean(lines[i]).Trim();
                var m = Regex.Match(t, @"^([A-Za-z_][A-Za-z0-9_]*)\s*:$");
                if (m.Success) labels[m.Groups[1].Value] = i;
            }
        }

        public void Run()
        {
            int ip = 0, steps = 0;
            while (ip < lines.Length)
            {
                if (++steps > 200000) throw new InvalidOperationException("Infinite loop limit reached.");
                string line = Clean(lines[ip]).Trim();
                if (line.Length == 0 || Regex.IsMatch(line, @"^(DIM\b|[A-Za-z_][A-Za-z0-9_]*\s*:)", RegexOptions.IgnoreCase)) { ip++; continue; }
                if (Regex.IsMatch(line, @"^END$", RegexOptions.IgnoreCase)) break;

                if (Regex.IsMatch(line, @"^PRINT\b", RegexOptions.IgnoreCase))
                {
                    string rest = line.Length > 5 ? line.Substring(5).Trim() : "";
                    if (rest.Length == 0) output.WriteLine();
                    else
                    {
                        var parts = SplitPrint(rest);
                        foreach (var part in parts) output.Write(Eval(part));
                        if (!rest.EndsWith(";") && !rest.EndsWith(",")) output.WriteLine();
                    }
                    ip++; continue;
                }

                var inputMatch = Regex.Match(line, "^INPUT\\s+(?:(?:\"([^\"]*)\")\\s*[,;]\\s*)?([A-Za-z_][A-Za-z0-9_$#]*)$", RegexOptions.IgnoreCase);
                if (inputMatch.Success)
                {
                    string prompt = inputMatch.Groups[1].Success ? inputMatch.Groups[1].Value : "? ";
                    string name = inputMatch.Groups[2].Value;
                    output.Write(prompt);
                    string value = input.ReadLine() ?? "";
                    vars[name] = name.EndsWith("$") ? value :
                        double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double d) ? d : 0d;
                    ip++; continue;
                }

                var fm = Regex.Match(line, @"^FOR\s+([A-Za-z_][A-Za-z0-9_$#]*)\s*=\s*(.+?)\s+TO\s+(.+?)(?:\s+STEP\s+(.+))?$", RegexOptions.IgnoreCase);
                if (fm.Success)
                {
                    string name = fm.Groups[1].Value;
                    double start = Num(Eval(fm.Groups[2].Value));
                    double end = Num(Eval(fm.Groups[3].Value));
                    double step = fm.Groups[4].Success ? Num(Eval(fm.Groups[4].Value)) : 1;
                    vars[name] = start;
                    if (step == 0) throw new InvalidOperationException("FOR STEP cannot be zero.");
                    if ((step > 0 && start > end) || (step < 0 && start < end))
                    {
                        int next = FindMatchingNext(ip + 1);
                        ip = next + 1; continue;
                    }
                    loops.Push(new ForFrame(name, end, step, ip + 1));
                    ip++; continue;
                }

                if (Regex.IsMatch(line, @"^NEXT\b", RegexOptions.IgnoreCase))
                {
                    if (loops.Count == 0) throw new InvalidOperationException("NEXT without matching FOR.");
                    var frame = loops.Peek();
                    double value = Num(vars.GetValueOrDefault(frame.Name)) + frame.Step;
                    vars[frame.Name] = value;
                    if ((frame.Step > 0 && value <= frame.End) || (frame.Step < 0 && value >= frame.End))
                    { ip = frame.StartLine; continue; }
                    loops.Pop(); ip++; continue;
                }

                var go = Regex.Match(line, @"^GOTO\s+([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.IgnoreCase);
                if (go.Success) { ip = Jump(go.Groups[1].Value); continue; }

                var gosub = Regex.Match(line, @"^GOSUB\s+([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.IgnoreCase);
                if (gosub.Success) { returns.Push(ip + 1); ip = Jump(gosub.Groups[1].Value); continue; }

                if (Regex.IsMatch(line, @"^RETURN$", RegexOptions.IgnoreCase))
                {
                    if (returns.Count == 0) throw new InvalidOperationException("RETURN without GOSUB.");
                    ip = returns.Pop(); continue;
                }

                var ifm = Regex.Match(line, @"^IF\s+(.+?)\s+THEN\s*(.*)$", RegexOptions.IgnoreCase);
                if (ifm.Success)
                {
                    bool condition = Condition(ifm.Groups[1].Value);
                    string rest = ifm.Groups[2].Value.Trim();
                    if (rest.Length == 0)
                    {
                        // Multi-line IF ... THEN block; locate ELSE and END IF.
                        int elseLine = -1, endLine = -1;
                        for (int j = ip + 1; j < lines.Length; j++)
                        {
                            string t = Clean(lines[j]).Trim();
                            if (Regex.IsMatch(t, @"^ELSE$", RegexOptions.IgnoreCase)) elseLine = j;
                            if (Regex.IsMatch(t, @"^END\s*IF$", RegexOptions.IgnoreCase)) { endLine = j; break; }
                        }
                        if (endLine < 0) throw new InvalidOperationException("IF without END IF.");
                        if (!condition) ip = elseLine >= 0 ? elseLine + 1 : endLine + 1;
                        else ip++;
                        continue;
                    }
                    var split = Regex.Split(rest, @"\s+ELSE\s+", RegexOptions.IgnoreCase);
                    if (condition) ExecuteInline(split[0], ref ip);
                    else if (split.Length > 1) ExecuteInline(split[1], ref ip);
                    else ip++;
                    continue;
                }

                if (Regex.IsMatch(line, @"^ELSE$", RegexOptions.IgnoreCase))
                {
                    while (ip < lines.Length && !Regex.IsMatch(Clean(lines[ip]).Trim(), @"^END\s*IF$", RegexOptions.IgnoreCase)) ip++;
                    ip++; continue;
                }
                if (Regex.IsMatch(line, @"^END\s*IF$", RegexOptions.IgnoreCase)) { ip++; continue; }

                string assignment = Regex.Replace(line, @"^LET\s+", "", RegexOptions.IgnoreCase);
                int eq = FindEquals(assignment);
                if (eq >= 0)
                {
                    string name = assignment.Substring(0, eq).Trim();
                    vars[name] = Eval(assignment.Substring(eq + 1).Trim());
                    ip++; continue;
                }
                throw new InvalidOperationException($"Unsupported statement at BASIC line {ip + 1}: {line}");
            }
        }

        private void ExecuteInline(string statement, ref int ip)
        {
            var go = Regex.Match(statement, @"^GOTO\s+([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.IgnoreCase);
            if (go.Success) { ip = Jump(go.Groups[1].Value); return; }
            if (Regex.IsMatch(statement, @"^PRINT\b", RegexOptions.IgnoreCase))
            {
                string rest = statement.Substring(5).Trim();
                foreach (var part in SplitPrint(rest)) output.Write(Eval(part));
                if (!rest.EndsWith(";") && !rest.EndsWith(",")) output.WriteLine();
                ip++; return;
            }
            int eq = FindEquals(statement);
            if (eq >= 0)
            {
                vars[statement.Substring(0, eq).Trim()] = Eval(statement.Substring(eq + 1).Trim());
                ip++; return;
            }
            throw new InvalidOperationException("Unsupported inline IF statement: " + statement);
        }

        private int Jump(string name) => labels.TryGetValue(name, out int line) ? line : throw new InvalidOperationException("Undefined label: " + name);
        private int FindMatchingNext(int from)
        {
            int depth = 0;
            for (int i = from; i < lines.Length; i++)
            {
                string t = Clean(lines[i]).Trim();
                if (Regex.IsMatch(t, @"^FOR\b", RegexOptions.IgnoreCase)) depth++;
                if (Regex.IsMatch(t, @"^NEXT\b", RegexOptions.IgnoreCase))
                {
                    if (depth == 0) return i;
                    depth--;
                }
            }
            throw new InvalidOperationException("FOR without matching NEXT.");
        }

        private object Eval(string expr)
        {
            expr = expr.Trim();
            if (expr.StartsWith("\"") && expr.EndsWith("\"")) return expr.Substring(1, expr.Length - 2);
            if (double.TryParse(expr, NumberStyles.Any, CultureInfo.InvariantCulture, out double number)) return number;
            var len = Regex.Match(expr, @"^LEN\((.*)\)$", RegexOptions.IgnoreCase);
            if (len.Success) return Convert.ToString(Eval(len.Groups[1].Value), CultureInfo.InvariantCulture)!.Length;
            var mid = Regex.Match(expr, @"^MID\$\((.*?),\s*(.*?)(?:,\s*(.*))?\)$", RegexOptions.IgnoreCase);
            if (mid.Success)
            {
                string s = Convert.ToString(Eval(mid.Groups[1].Value), CultureInfo.InvariantCulture) ?? "";
                int start = Math.Max(0, (int)Num(Eval(mid.Groups[2].Value)) - 1);
                int length = mid.Groups[3].Success ? Math.Max(0, (int)Num(Eval(mid.Groups[3].Value))) : s.Length - start;
                return start >= s.Length ? "" : s.Substring(start, Math.Min(length, s.Length - start));
            }
            var tokens = TokenizeExpr(expr);
            return new RuntimeExpression(tokens, vars).Parse();
        }

        private bool Condition(string condition)
        {
            var m = Regex.Match(condition, @"(<=|>=|<>|<|>|=)");
            if (!m.Success) return Num(Eval(condition)) != 0;
            object a = Eval(condition.Substring(0, m.Index));
            object b = Eval(condition.Substring(m.Index + m.Length));
            int cmp = a is string || b is string
                ? string.Compare(Convert.ToString(a, CultureInfo.InvariantCulture), Convert.ToString(b, CultureInfo.InvariantCulture), StringComparison.Ordinal)
                : Num(a).CompareTo(Num(b));
            return m.Value switch { "=" => cmp == 0, "<>" => cmp != 0, "<" => cmp < 0, "<=" => cmp <= 0, ">" => cmp > 0, ">=" => cmp >= 0, _ => false };
        }

        private static double Num(object? value) => value is double d ? d : double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out double n) ? n : 0;
        private static int FindEquals(string text) { bool q = false; int depth = 0; for (int i=0;i<text.Length;i++) { if(text[i]=='"') q=!q; else if(!q&&text[i]=='(') depth++; else if(!q&&text[i]==')') depth--; else if(!q&&depth==0&&text[i]=='=') return i; } return -1; }
        private static string Clean(string line) { bool q=false; for(int i=0;i<line.Length;i++){ if(line[i]=='"') q=!q; else if(!q&&line[i]=='\'') return Regex.Replace(line.Substring(0,i), @"\bREM\b.*$", "", RegexOptions.IgnoreCase); } return Regex.Replace(line, @"\bREM\b.*$", "", RegexOptions.IgnoreCase); }
        private static List<string> SplitPrint(string s) { var result=new List<string>(); bool q=false; int d=0,start=0; for(int i=0;i<s.Length;i++){ if(s[i]=='"')q=!q; else if(!q&&s[i]=='(')d++; else if(!q&&s[i]==')')d--; else if(!q&&d==0&&(s[i]==';'||s[i]==',')){ if(i>start)result.Add(s.Substring(start,i-start).Trim()); start=i+1; } } if(start<s.Length)result.Add(s.Substring(start).Trim()); return result.Where(x=>x.Length>0).ToList(); }
        private static List<string> TokenizeExpr(string s)
        {
            var t=new List<string>();
            for(int i=0;i<s.Length;)
            {
                if(char.IsWhiteSpace(s[i])){i++;continue;}
                if(s[i]=='"'){int st=i++;while(i<s.Length&&s[i]!='"')i++;if(i>=s.Length)throw new FormatException("Unterminated string.");i++;t.Add(s.Substring(st,i-st));continue;}
                if(char.IsLetterOrDigit(s[i])||s[i]=='_'||s[i]=='$'||s[i]=='#'||s[i]=='.'){int st=i++;while(i<s.Length&&(char.IsLetterOrDigit(s[i])||s[i]=='_'||s[i]=='$'||s[i]=='#'||s[i]=='.'))i++;t.Add(s.Substring(st,i-st));continue;}
                if("()+-*/".Contains(s[i])){t.Add(s[i++].ToString());continue;}
                if(i+3<=s.Length&&s.Substring(i,3).Equals("MOD",StringComparison.OrdinalIgnoreCase)){t.Add("MOD");i+=3;continue;}
                throw new FormatException("Invalid expression near: "+s.Substring(i));
            }
            return t;
        }

        private sealed class RuntimeExpression
        {
            private readonly List<string> t; private readonly Dictionary<string,object?> vars; private int p;
            public RuntimeExpression(List<string> tokens, Dictionary<string,object?> variables){t=tokens;vars=variables;}
            public object Parse(){object x=Add();if(p<t.Count)throw new FormatException("Unexpected expression token "+t[p]);return x;}
            private object Add(){object x=Mul();while(Peek("+")||Peek("-")){string op=t[p++];object y=Mul();x=op=="+"&&(x is string||y is string)?Convert.ToString(x,CultureInfo.InvariantCulture)+Convert.ToString(y,CultureInfo.InvariantCulture):op=="+"?N(x)+N(y):N(x)-N(y);}return x;}
            private object Mul(){object x=Primary();while(Peek("*")||Peek("/")||Peek("MOD")){string op=t[p++].ToUpperInvariant();object y=Primary();x=op switch{"*"=>N(x)*N(y),"/"=>N(x)/N(y),"MOD"=>N(x)%N(y),_=>x};}return x;}
            private object Primary(){if(Peek("-")){p++;return -N(Primary());}if(Peek("(")){p++;object x=Add();Need(")");return x;}if(p>=t.Count)throw new FormatException("Incomplete expression.");string v=t[p++];if(v.StartsWith("\""))return v.Substring(1,v.Length-2);if(double.TryParse(v,NumberStyles.Any,CultureInfo.InvariantCulture,out double n))return n;if(Peek("(")){p++;var args=new List<object>();if(!Peek(")")){args.Add(Add());while(Peek(",")){p++;args.Add(Add());}}Need(")");if(v.Equals("LEN",StringComparison.OrdinalIgnoreCase))return Convert.ToString(args[0],CultureInfo.InvariantCulture)!.Length;if(v.Equals("MID$",StringComparison.OrdinalIgnoreCase)){string s=Convert.ToString(args[0],CultureInfo.InvariantCulture)??"";int st=Math.Max(0,(int)N(args[1])-1);int len=args.Count>2?(int)N(args[2]):s.Length-st;return st>=s.Length?"":s.Substring(st,Math.Min(Math.Max(0,len),s.Length-st));}throw new FormatException("Unsupported function "+v);}return vars.TryGetValue(v,out object? value)?value??0d:0d;}
            private bool Peek(string x)=>p<t.Count&&t[p].Equals(x,StringComparison.OrdinalIgnoreCase);
            private void Need(string x){if(!Peek(x))throw new FormatException("Expected "+x);p++;}
            private static double N(object x)=>double.TryParse(Convert.ToString(x,CultureInfo.InvariantCulture),NumberStyles.Any,CultureInfo.InvariantCulture,out double n)?n:0;
        }
    }
}
