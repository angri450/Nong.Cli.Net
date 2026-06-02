namespace Nong.Inspect;

/// <summary>
/// 参考文献条目数据模型。
/// </summary>
public class RefEntry
{
    /// <summary>引用键（如 smith2024），正文中用 [@smith2024] 引用。</summary>
    public string Key { get; init; } = "";

    /// <summary>文献类型：article / book / thesis / conference / patent / report / standard / online</summary>
    public string Type { get; init; } = "article";

    /// <summary>作者列表。</summary>
    public List<string> Author { get; init; } = new();

    /// <summary>题名。</summary>
    public string Title { get; init; } = "";

    /// <summary>期刊/出版社。</summary>
    public string Journal { get; init; } = "";

    /// <summary>年份。</summary>
    public string Year { get; init; } = "";

    /// <summary>卷。</summary>
    public string? Volume { get; init; }

    /// <summary>期。</summary>
    public string? Issue { get; init; }

    /// <summary>页码。</summary>
    public string? Pages { get; init; }

    /// <summary>DOI。</summary>
    public string? Doi { get; init; }

    /// <summary>出版地（书籍/报告用）。</summary>
    public string? Publisher { get; init; }

    /// <summary>学位类型（thesis 用）。</summary>
    public string? Degree { get; init; }

    /// <summary>URL（在线资源用）。</summary>
    public string? Url { get; init; }

    /// <summary>访问日期（在线资源用）。</summary>
    public string? AccessDate { get; init; }

    /// <summary>标准编号（standard/patent 用）。</summary>
    public string? Number { get; init; }

    /// <summary>自定义格式化字符串（覆盖自动格式化）。</summary>
    public string? Raw { get; init; }
}
