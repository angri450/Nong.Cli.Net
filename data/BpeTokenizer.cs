namespace Angri450.Nong.Data;

/// <summary>
/// Pure C# BPE tokenizer for jina-embeddings-v5 models.
/// Reads standard HuggingFace tokenizer.json (GPT-2 byte-level BPE format).
/// Zero external tokenizer library dependency.
/// </summary>
public sealed class BpeTokenizer
{
    readonly Dictionary<string, int> _vocab;
    readonly Dictionary<int, string> _vocabReverse;
    readonly Dictionary<(string, string), int> _merges; // (a,b) -> rank (lower = higher priority)
    readonly System.Text.RegularExpressions.Regex _preTokenizer;
    readonly char[] _byteToChar;
    readonly Dictionary<char, byte> _charToByte;

    // GPT-2 pre-tokenizer regex (standard for byte-level BPE)
    const string Gpt2Regex = @"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?\p{L}+|\p{N}{1,3}| ?[^\s\p{L}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+";

    public int VocabSize => _vocab.Count;
    public int BOS { get; } = 128000; // <|begin_of_text|>
    public int EOS { get; } = 128001; // <|end_of_text|>
    public int PAD { get; } = 128004; // <|pad|>

    public BpeTokenizer(string tokenizerJsonPath)
    {
        using var json = System.Text.Json.JsonDocument.Parse(File.OpenRead(tokenizerJsonPath));
        var model = json.RootElement.GetProperty("model");

        // ── 1. Vocab ──
        _vocab = new Dictionary<string, int>();
        _vocabReverse = new Dictionary<int, string>();
        foreach (var entry in model.GetProperty("vocab").EnumerateObject())
        {
            int id = entry.Value.GetInt32();
            _vocab[entry.Name] = id;
            _vocabReverse[id] = entry.Name;
        }

        // ── 2. Merges ──
        _merges = new Dictionary<(string, string), int>();
        int mergeIdx = 0;
        foreach (var merge in model.GetProperty("merges").EnumerateArray())
        {
            var pair = merge.EnumerateArray().ToArray();
            if (pair.Length == 2)
                _merges[(pair[0].GetString()!, pair[1].GetString()!)] = mergeIdx++;
        }

        // ── 3. GPT-2 bytes-to-unicode mapping ──
        _byteToChar = new char[256];
        _charToByte = new Dictionary<char, byte>();
        var directBytes = new HashSet<int>();
        // Printable ASCII: '!' (33) to '~' (126)
        for (int b = 33; b <= 126; b++) directBytes.Add(b);
        // Latin-1 supplement: ¡ (161) to ¬ (172), ® (174) to ÿ (255)
        for (int b = 161; b <= 172; b++) directBytes.Add(b);
        for (int b = 174; b <= 255; b++) directBytes.Add(b);

        int offset = 0;
        for (int b = 0; b < 256; b++)
        {
            if (directBytes.Contains(b))
                _byteToChar[b] = (char)b;
            else
                _byteToChar[b] = (char)(256 + offset++);
        }
        for (int b = 0; b < 256; b++)
        {
            var c = _byteToChar[b];
            if (!_charToByte.ContainsKey(c))
                _charToByte[c] = (byte)b;
        }

        // ── 4. Pre-tokenizer ──
        _preTokenizer = new System.Text.RegularExpressions.Regex(
            Gpt2Regex,
            System.Text.RegularExpressions.RegexOptions.Compiled);
    }

    /// <summary>
    /// Encode text to token IDs (without BOS/EOS special tokens).
    /// </summary>
    public IReadOnlyList<int> Encode(string text)
    {
        var ids = new List<int>();
        var matches = _preTokenizer.Matches(text);

        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            var word = m.Value;
            var wordBytes = System.Text.Encoding.UTF8.GetBytes(word);
            if (wordBytes.Length == 0) continue;

            // Byte-level encode: each byte -> Unicode char
            var symbols = new List<string>(wordBytes.Length);
            foreach (byte b in wordBytes)
                symbols.Add(_byteToChar[b].ToString());

            // Apply BPE merges (priority-queue: always merge the lowest-ranked pair first)
            while (symbols.Count > 1)
            {
                int bestRank = int.MaxValue;
                int bestPos = -1;
                for (int i = 0; i < symbols.Count - 1; i++)
                {
                    if (_merges.TryGetValue((symbols[i], symbols[i + 1]), out int rank)
                        && rank < bestRank)
                    {
                        bestRank = rank;
                        bestPos = i;
                    }
                }
                if (bestPos < 0) break;

                symbols[bestPos] = symbols[bestPos] + symbols[bestPos + 1];
                symbols.RemoveAt(bestPos + 1);
            }

            // Lookup final symbols in vocab
            foreach (var sym in symbols)
            {
                if (_vocab.TryGetValue(sym, out int id))
                    ids.Add(id);
                else
                    ids.Add(0); // fallback to '!' (vocab id 0)
            }
        }

        return ids;
    }

    /// <summary>
    /// Create attention mask (all 1s for the given token count).
    /// </summary>
    public long[] AttentionMask(int tokenCount)
    {
        var mask = new long[tokenCount];
        Array.Fill(mask, 1L);
        return mask;
    }
}
