using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Math;

namespace DocxCore;

/// <summary>
/// LaTeX 公式 → OMML（Office Math Markup Language）渲染器。
/// 支持论文中常用的数学符号和结构。
///
/// 内联公式：RenderInline("E=mc^2") → OfficeMath
/// 独立公式：RenderDisplay("y = \\beta x + \\epsilon") → Paragraph
///
/// 支持的 LaTeX 语法：
///   上标 x^{2} 或 x^2 / 下标 x_{i} 或 x_i
///   分数 \frac{num}{den}
///   根号 \sqrt{x} / n 次根号 \sqrt[n]{x}
///   希腊字母 \alpha \beta \Gamma \Delta 等
///   函数名 \sin \cos \log \ln \lim \max \min
///   求和 \sum_{i=1}^{n} / 积分 \int_{a}^{b}
///   括号 \left( ... \right)
///   重音 \hat{x} \bar{x} \dot{x} \ddot{x} \vec{x}
///   矩阵 \begin{matrix} a&amp;b\\c&amp;d \end{matrix}
/// </summary>
public static class MathRenderer
{
    // ===== 希腊字母映射 =====

    static readonly Dictionary<string, char> GreekLower = new()
    {
        ["alpha"] = 'α', ["beta"] = 'β', ["gamma"] = 'γ',
        ["delta"] = 'δ', ["epsilon"] = 'ε', ["zeta"] = 'ζ',
        ["eta"] = 'η', ["theta"] = 'θ', ["iota"] = 'ι',
        ["kappa"] = 'κ', ["lambda"] = 'λ', ["mu"] = 'μ',
        ["nu"] = 'ν', ["xi"] = 'ξ', ["pi"] = 'π',
        ["rho"] = 'ρ', ["sigma"] = 'σ', ["tau"] = 'τ',
        ["upsilon"] = 'υ', ["phi"] = 'φ', ["chi"] = 'χ',
        ["psi"] = 'ψ', ["omega"] = 'ω',
        ["varepsilon"] = 'ε', ["vartheta"] = 'ϑ',
        ["varphi"] = 'φ', ["varrho"] = 'ϱ',
    };

    static readonly Dictionary<string, char> GreekUpper = new()
    {
        ["Gamma"] = 'Γ', ["Delta"] = 'Δ', ["Theta"] = 'Θ',
        ["Lambda"] = 'Λ', ["Xi"] = 'Ξ', ["Pi"] = 'Π',
        ["Sigma"] = 'Σ', ["Upsilon"] = 'Υ', ["Phi"] = 'Φ',
        ["Psi"] = 'Ψ', ["Omega"] = 'Ω',
    };

    static readonly HashSet<string> FunctionNames = new()
    {
        "sin", "cos", "tan", "cot", "sec", "csc",
        "arcsin", "arccos", "arctan",
        "sinh", "cosh", "tanh",
        "log", "ln", "lg", "exp",
        "lim", "max", "min", "sup", "inf",
        "det", "dim", "ker", "deg", "gcd", "hom",
        "Pr", "arg",
    };

    // ===== 公共 API =====

    /// <summary>渲染内联公式。</summary>
    public static OfficeMath RenderInline(string latex)
    {
        var om = new OfficeMath();
        om.Append(BuildArgs(latex));
        return om;
    }

    /// <summary>渲染独立公式（居中段落）。</summary>
    public static Paragraph RenderDisplay(string latex)
    {
        var para = new Paragraph(new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties(
            new DocumentFormat.OpenXml.Wordprocessing.Justification { Val = DocumentFormat.OpenXml.Wordprocessing.JustificationValues.Center }));
        var om = new OfficeMath();
        om.Append(BuildArgs(latex));
        para.Append(new DocumentFormat.OpenXml.Wordprocessing.Run(om));
        return para;
    }

    // ===== 公式内容构建 =====

    static List<OpenXmlElement> BuildArgs(string latex)
    {
        var tokens = Tokenize(latex);
        return Parse(tokens, 0, out _);
    }

    // ===== Tokenizer =====

    enum TokType { Text, Cmd, Sup, Sub, BraceOpen, BraceClose, Amp, Newline, EOF }

    struct Token
    {
        public TokType Type;
        public string Value;
    }

    static List<Token> Tokenize(string input)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < input.Length)
        {
            char c = input[i];
            if (c == '\\')
            {
                i++;
                int start = i;
                while (i < input.Length && char.IsLetter(input[i])) i++;
                if (i > start) tokens.Add(new Token { Type = TokType.Cmd, Value = input[start..i] });
                else if (i < input.Length) { tokens.Add(new Token { Type = TokType.Text, Value = input[i].ToString() }); i++; }
            }
            else if (c == '^') { tokens.Add(new Token { Type = TokType.Sup, Value = "^" }); i++; }
            else if (c == '_') { tokens.Add(new Token { Type = TokType.Sub, Value = "_" }); i++; }
            else if (c == '{') { tokens.Add(new Token { Type = TokType.BraceOpen, Value = "{" }); i++; }
            else if (c == '}') { tokens.Add(new Token { Type = TokType.BraceClose, Value = "}" }); i++; }
            else if (c == '&') { tokens.Add(new Token { Type = TokType.Amp, Value = "&" }); i++; }
            else if (c == '\\' && i + 1 < input.Length && input[i + 1] == '\\')
            {
                tokens.Add(new Token { Type = TokType.Newline, Value = "\\\\" }); i += 2;
            }
            else
            {
                int start = i;
                while (i < input.Length && !"\\^_{}&".Contains(input[i])) i++;
                if (i > start) tokens.Add(new Token { Type = TokType.Text, Value = input[start..i] });
            }
        }
        tokens.Add(new Token { Type = TokType.EOF });
        return tokens;
    }

    // ===== Parser → OMML builder =====

    static List<OpenXmlElement> Parse(List<Token> tokens, int pos, out int next)
    {
        var elements = new List<OpenXmlElement>();
        while (pos < tokens.Count)
        {
            var t = tokens[pos];
            if (t.Type == TokType.EOF || t.Type == TokType.BraceClose || t.Type == TokType.Amp || t.Type == TokType.Newline)
            {
                next = pos;
                return elements;
            }

            if (t.Type == TokType.Cmd)
            {
                pos = ParseCommand(tokens, pos, elements);
            }
            else if (t.Type == TokType.Sup)
            {
                pos++;
                var supArgs = ParseArg(tokens, pos, out pos);
                var sup = new Superscript(new SuperscriptProperties(), new Base());
                foreach (var a in supArgs) sup.Append(a);
                elements.Add(sup);
            }
            else if (t.Type == TokType.Sub)
            {
                pos++;
                var subArgs = ParseArg(tokens, pos, out pos);
                var sub = new Subscript(new SubscriptProperties(), new Base());
                foreach (var a in subArgs) sub.Append(a);
                elements.Add(sub);
            }
            else if (t.Type == TokType.Text)
            {
                elements.Add(MakeRun(t.Value));
                pos++;
            }
            else
            {
                pos++;
            }
        }
        next = pos;
        return elements;
    }

    static int ParseCommand(List<Token> tokens, int pos, List<OpenXmlElement> elements)
    {
        var cmd = tokens[pos].Value;
        pos++;

        if (cmd == "frac")
        {
            var numArgs = ParseArg(tokens, pos, out pos);
            var denArgs = ParseArg(tokens, pos, out pos);
            var frac = new Fraction(new FractionProperties(new FractionType { Val = FractionTypeValues.Bar }));
            var num = new Numerator(); foreach (var a in numArgs) num.Append(a);
            var den = new Denominator(); foreach (var a in denArgs) den.Append(a);
            frac.Append(num); frac.Append(den);
            elements.Add(frac);
            return pos;
        }

        if (cmd == "sqrt")
        {
            var degreeArgs = new List<OpenXmlElement>();
            // Check for optional [n]
            if (pos < tokens.Count && tokens[pos].Type == TokType.BraceOpen)
            {
                var peek = tokens[pos];
            }
            // \sqrt[n]{x}
            if (pos < tokens.Count && tokens[pos].Type == TokType.Text && tokens[pos].Value.StartsWith('['))
            {
                var nText = tokens[pos].Value.Trim('[', ']');
                degreeArgs.Add(MakeRun(nText));
                pos++;
            }
            var bodyArgs = ParseArg(tokens, pos, out pos);
            var rad = new Radical(new RadicalProperties());
            if (degreeArgs.Count > 0) { var deg = new Degree(); foreach (var a in degreeArgs) deg.Append(a); rad.Append(deg); }
            var radBase = new Base(); foreach (var a in bodyArgs) radBase.Append(a);
            rad.Append(radBase);
            elements.Add(rad);
            return pos;
        }

        if (cmd == "sum" || cmd == "prod" || cmd == "int" || cmd == "oint" || cmd == "iint" || cmd == "iiint")
        {
            var nary = new Nary(new NaryProperties());
            // Check for limits
            var naryBase = new Base();
            var supArgs = new List<OpenXmlElement>();
            var subArgs = new List<OpenXmlElement>();

            while (pos < tokens.Count)
            {
                var t = tokens[pos];
                if (t.Type == TokType.Sub)
                {
                    pos++;
                    subArgs = ParseArg(tokens, pos, out pos);
                }
                else if (t.Type == TokType.Sup)
                {
                    pos++;
                    supArgs = ParseArg(tokens, pos, out pos);
                }
                else break;
            }
            // Operator symbol
            var opText = cmd switch
            {
                "sum" => "∑", "prod" => "∏", "int" => "∫",
                "oint" => "∮", "iint" => "∬", "iiint" => "∭", _ => cmd
            };
            naryBase.Append(MakeRun(opText));
            if (subArgs.Count > 0) { var sl = new Subscript(); foreach (var a in subArgs) sl.Append(a); naryBase.Append(sl); }
            if (supArgs.Count > 0) { var sp = new Superscript(); foreach (var a in supArgs) sp.Append(a); naryBase.Append(sp); }
            nary.Append(naryBase);
            elements.Add(nary);
            return pos;
        }

        if (FunctionNames.Contains(cmd))
        {
            var func = new FunctionName();
            func.Append(MakeRun(cmd));
            elements.Add(func);
            return pos;
        }

        if (cmd == "left")
        {
            // \left( ... \right) — simplified: skip \left, handle delimiter, skip \right
            var delimChar = pos < tokens.Count && tokens[pos].Type == TokType.Text ? tokens[pos].Value : "(";
            pos++;
            var innerArgs = new List<OpenXmlElement>();
            // Parse until \right
            while (pos < tokens.Count)
            {
                if (tokens[pos].Type == TokType.Cmd && tokens[pos].Value == "right")
                {
                    pos += 2; // skip \right and the delimiter
                    break;
                }
                if (tokens[pos].Type == TokType.BraceOpen) { pos++; continue; }
                if (tokens[pos].Type == TokType.BraceClose) { pos++; continue; }
                var sub = Parse(tokens, pos, out pos);
                innerArgs.AddRange(sub);
                if (pos >= tokens.Count || tokens[pos].Type == TokType.EOF) break;
            }
            var delim = new Delimiter(new DelimiterProperties());
            var beginChar = delimChar switch { "(" => "(", ")" => ")", "[" => "[", "]" => "]", "{" => "{", "}" => "}", "|" => "|", _ => "(" };
            foreach (var a in innerArgs) delim.Append(a);
            elements.Add(delim);
            return pos;
        }

        if (cmd == "hat" || cmd == "bar" || cmd == "dot" || cmd == "ddot" || cmd == "vec" || cmd == "tilde")
        {
            var accentArgs = ParseArg(tokens, pos, out pos);
            var accentChar = cmd switch { "hat" => "̂", "bar" => "̄", "dot" => "̇", "ddot" => "̈", "vec" => "⃗", "tilde" => "̃", _ => "̂" };
            var acc = new Accent(new AccentProperties(new AccentChar { Val = accentChar }));
            var accBase = new Base(); foreach (var a in accentArgs) accBase.Append(a);
            acc.Append(accBase);
            elements.Add(acc);
            return pos;
        }

        if (cmd == "begin")
        {
            // \begin{matrix} ... \end{matrix}
            var envName = "";
            if (pos < tokens.Count && tokens[pos].Type == TokType.BraceOpen) pos++;
            if (pos < tokens.Count && tokens[pos].Type == TokType.Text) { envName = tokens[pos].Value; pos++; }
            if (pos < tokens.Count && tokens[pos].Type == TokType.BraceClose) pos++;

            var rows = new List<List<List<OpenXmlElement>>>();
            var currentRow = new List<List<OpenXmlElement>>();
            var currentCell = new List<OpenXmlElement>();

            while (pos < tokens.Count)
            {
                if (tokens[pos].Type == TokType.Cmd && tokens[pos].Value == "end") { pos += 2; break; }
                if (tokens[pos].Type == TokType.Amp) { currentRow.Add(currentCell); currentCell = new(); pos++; continue; }
                if (tokens[pos].Type == TokType.Newline) { currentRow.Add(currentCell); rows.Add(currentRow); currentRow = new(); currentCell = new(); pos++; continue; }
                if (tokens[pos].Type == TokType.BraceOpen) { pos++; continue; }
                if (tokens[pos].Type == TokType.BraceClose) { pos++; continue; }
                var sub = Parse(tokens, pos, out pos);
                currentCell.AddRange(sub);
            }
            currentRow.Add(currentCell);
            if (currentRow.Count > 0) rows.Add(currentRow);

            var mtx = new Matrix(new MatrixProperties(new MatrixColumns(new MatrixColumnCount { Val = rows.Max(r => r.Count) })));
            foreach (var row in rows)
            {
                var mr = new MatrixRow();
                foreach (var cell in row)
                {
                    var mc = new MatrixColumn();
                    foreach (var el in cell) mc.Append(el.CloneNode(true));
                    mr.Append(mc);
                }
                mtx.Append(mr);
            }
            elements.Add(mtx);
            return pos;
        }

        // Check Greek letters
        if (GreekLower.TryGetValue(cmd, out var gl)) { elements.Add(MakeRun(gl.ToString())); return pos; }
        if (GreekUpper.TryGetValue(cmd, out var gu)) { elements.Add(MakeRun(gu.ToString())); return pos; }

        // Unknown command — output as text
        elements.Add(MakeRun("\\" + cmd));
        return pos;
    }

    static List<OpenXmlElement> ParseArg(List<Token> tokens, int pos, out int next)
    {
        if (pos >= tokens.Count) { next = pos; return new(); }

        if (tokens[pos].Type == TokType.BraceOpen)
        {
            pos++; // skip {
            var args = Parse(tokens, pos, out pos);
            if (pos < tokens.Count && tokens[pos].Type == TokType.BraceClose) pos++; // skip }
            next = pos;
            return args;
        }

        // Single token argument (like x_i for subscripts)
        if (tokens[pos].Type == TokType.Text || tokens[pos].Type == TokType.Cmd)
        {
            var result = new List<OpenXmlElement>();
            pos = ParseCommandOrText(tokens, pos, result);
            next = pos;
            return result;
        }

        next = pos;
        return new();
    }

    static int ParseCommandOrText(List<Token> tokens, int pos, List<OpenXmlElement> elements)
    {
        if (pos >= tokens.Count) return pos;
        if (tokens[pos].Type == TokType.Cmd) return ParseCommand(tokens, pos, elements);
        if (tokens[pos].Type == TokType.Text) { elements.Add(MakeRun(tokens[pos].Value)); return pos + 1; }
        return pos;
    }

    static Run MakeRun(string text)
    {
        var rpr = new RunProperties(new Literal());
        return new Run(rpr, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }
}
