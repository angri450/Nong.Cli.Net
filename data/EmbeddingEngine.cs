using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Angri450.Nong.Data;

/// <summary>
/// Local text embedding engine: jina-embeddings-v5-nano ONNX model,
/// BpeTokenizer tokenization, ONNX Runtime CPU inference.
/// Output: L2-normalized float[768] using last-token pooling.
/// </summary>
public sealed class EmbeddingEngine : IDisposable
{
    readonly InferenceSession _session;
    readonly BpeTokenizer _tokenizer;
    readonly int _hiddenSize;

    const string PassagePrefix = "Document: ";
    const string QueryPrefix = "";

    public int Dimension => _hiddenSize;

    public EmbeddingEngine(string modelDir)
    {
        var onnxPath = Path.Combine(modelDir, "model.onnx");
        var tokPath = Path.Combine(modelDir, "tokenizer.json");
        if (!File.Exists(onnxPath))
            throw new FileNotFoundException($"ONNX model not found: {onnxPath}");
        if (!File.Exists(tokPath))
            throw new FileNotFoundException($"Tokenizer not found: {tokPath}");

        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
        };
        var threads = Math.Max(1, Environment.ProcessorCount / 2);
        options.IntraOpNumThreads = threads;
        options.InterOpNumThreads = 1;

        _session = new InferenceSession(onnxPath, options);
        _tokenizer = new BpeTokenizer(tokPath);

        foreach (var name in _session.OutputMetadata.Keys)
            if (_session.OutputMetadata[name].Dimensions is [1, _, >= 384])
                { _hiddenSize = _session.OutputMetadata[name].Dimensions[2]; break; }
        if (_hiddenSize == 0) _hiddenSize = 768;
    }

    public float[] EmbedPassage(string text) => Embed(PassagePrefix + text);
    public float[] EmbedQuery(string text) => Embed(QueryPrefix + text);

    float[] Embed(string text)
    {
        var ids = _tokenizer.Encode(text);
        if (ids.Count > 8192) ids = ids.Take(8192).ToList();

        var idArr = ids.Select(i => (long)i).ToArray();
        var inputIds = new DenseTensor<long>(idArr, new[] { 1, ids.Count });
        var attentionMask = new DenseTensor<long>(_tokenizer.AttentionMask(ids.Count), new[] { 1, ids.Count });

        using var results = _session.Run(new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
        });

        var outputList = results.ToList();
        if (outputList.Count == 0)
            throw new InvalidOperationException("ONNX model returned no outputs.");

        var tensor = outputList[0].AsTensor<float>();
        if (tensor == null)
            throw new InvalidOperationException("Cannot read ONNX output as float32.");

        return LastTokenPoolAndNormalize(tensor, ids.Count);
    }

    public static float Cosine(float[] a, float[] b)
    {
        float dot = 0;
        for (int i = 0; i < a.Length; i++) dot += a[i] * b[i];
        return dot;
    }

    float[] LastTokenPoolAndNormalize(Tensor<float> h, int seqLen)
    {
        var hidden = h.Dimensions[2];
        var vec = new float[hidden];
        int last = Math.Max(0, seqLen - 1);
        for (int i = 0; i < hidden; i++) vec[i] = h[0, last, i];

        float norm = 0;
        for (int i = 0; i < hidden; i++) norm += vec[i] * vec[i];
        norm = MathF.Sqrt(norm);
        if (norm > 1e-8f) for (int i = 0; i < hidden; i++) vec[i] /= norm;

        return vec;
    }

    public void Dispose() => _session?.Dispose();
}
