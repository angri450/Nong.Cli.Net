using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Angri450.Nong.Data;

/// <summary>
/// Local text embedding engine: loads jina-embeddings-v5-nano ONNX model,
/// tokenizes text via BpeTokenizer, runs ONNX inference on CPU.
/// Output: L2-normalized float[768] vector.
/// </summary>
public sealed class EmbeddingEngine : IDisposable
{
    readonly InferenceSession _session;
    readonly BpeTokenizer _tokenizer;
    readonly int _hiddenSize;

    public int Dimension => _hiddenSize;

    public EmbeddingEngine(string modelDir)
    {
        var onnxPath = Path.Combine(modelDir, "model_int8.onnx");
        var tokPath = Path.Combine(modelDir, "tokenizer.json");

        if (!File.Exists(onnxPath))
            throw new FileNotFoundException($"ONNX model not found: {onnxPath}. Run 'nong nongcli install-embedding' first.");
        if (!File.Exists(tokPath))
            throw new FileNotFoundException($"Tokenizer not found: {tokPath}");

        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
        };
        options.AppendExecutionProvider_CPU(0);
        // Use half the logical cores for intra-op parallelism
        var threads = Math.Max(1, Environment.ProcessorCount / 2);
        options.IntraOpNumThreads = threads;
        options.InterOpNumThreads = 1;

        _session = new InferenceSession(onnxPath, options);
        _tokenizer = new BpeTokenizer(tokPath);

        // Detect hidden size from output metadata
        _hiddenSize = DetectHiddenSize();
    }

    int DetectHiddenSize()
    {
        foreach (var name in _session.OutputMetadata.Keys)
        {
            var info = _session.OutputMetadata[name];
            if (info.Dimensions is [1, _, >= 384])
                return info.Dimensions[2];
        }
        return 768; // jina-v5-nano default
    }

    /// <summary>
    /// Generate embedding vector for a single text.
    /// Returns L2-normalized float[_hiddenSize].
    /// </summary>
    public float[] Embed(string text)
    {
        var ids = _tokenizer.Encode(text);
        // Truncate to 8192 tokens (model max context)
        if (ids.Count > 8192)
            ids = ids.Take(8192).ToList();

        var inputIds = new DenseTensor<long>(ids.Select(i => (long)i).ToArray(), new[] { 1, ids.Count });
        var attentionMask = new DenseTensor<long>(_tokenizer.AttentionMask(ids.Count), new[] { 1, ids.Count });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
        };

        using var results = _session.Run(inputs);
        var output = results.First().AsTensor<float>();

        return MeanPoolAndNormalize(output, ids.Count);
    }

    /// <summary>
    /// Batch embed multiple texts (efficient reuse of the loaded session).
    /// </summary>
    public List<float[]> EmbedBatch(IEnumerable<string> texts)
    {
        return texts.Select(Embed).ToList();
    }

    /// <summary>
    /// Cosine similarity between two normalized vectors.
    /// </summary>
    public static float Cosine(float[] a, float[] b)
    {
        float dot = 0;
        for (int i = 0; i < a.Length; i++)
            dot += a[i] * b[i];
        return dot; // Vectors are already L2-normalized, dot product = cosine
    }

    /// <summary>
    /// Top-K search by cosine similarity, returns (index, score) sorted descending.
    /// </summary>
    public static List<(int Index, float Score)> Search(float[] query, IReadOnlyList<float[]> corpus, int topK)
    {
        var results = new List<(int Index, float Score)>(corpus.Count);
        for (int i = 0; i < corpus.Count; i++)
            results.Add((i, Cosine(query, corpus[i])));
        results.Sort((a, b) => b.Score.CompareTo(a.Score));
        return results.Take(topK).ToList();
    }

    float[] MeanPoolAndNormalize(Tensor<float> lastHiddenState, int seqLen)
    {
        var hidden = _hiddenSize;
        var vec = new float[hidden];

        // Mean pooling over non-padding tokens (exclude EOS if present as last token)
        int effectiveLen = seqLen;
        if (effectiveLen > 1) effectiveLen--; // exclude last token (typically EOS)

        effectiveLen = Math.Max(1, effectiveLen);

        for (int i = 0; i < hidden; i++)
        {
            float sum = 0;
            for (int t = 0; t < effectiveLen; t++)
                sum += lastHiddenState[0, t, i];
            vec[i] = sum / effectiveLen;
        }

        // L2 normalization
        float norm = 0;
        for (int i = 0; i < hidden; i++)
            norm += vec[i] * vec[i];
        norm = MathF.Sqrt(norm);
        if (norm > 1e-8f)
        {
            for (int i = 0; i < hidden; i++)
                vec[i] /= norm;
        }

        return vec;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
