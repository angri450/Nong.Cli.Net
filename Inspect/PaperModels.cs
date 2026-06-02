namespace Nong.Inspect;

/// <summary>
/// 论文类型信息。
/// </summary>
public sealed class PaperTypeInfo
{
    public string 论文类型 { get; init; } = "";
    public string 判断依据 { get; init; } = "";
    public int 当前匹配度 { get; init; }
    public string 推荐数据 { get; init; } = "";
    public string 推荐方法 { get; init; } = "";
    public string 主要缺口 { get; init; } = "";
    public string 风险等级 { get; init; } = "";
    public string 模拟训练说明 { get; init; } = "可以，但必须标注 synthetic / simulated training data";
    public string 是否必须重新收集数据 { get; init; } = "若真实数据缺失或研究问题不匹配，则必须重新收集";
}

/// <summary>
/// 论文结构（对应 Python PaperStructure）。
/// </summary>
public sealed class PaperStructure
{
    public string Title { get; init; } = "";
    public List<string> Authors { get; init; } = new();
    public string Abstract { get; init; } = "";
    public List<string> Keywords { get; init; } = new();
    public List<PaperSection> Sections { get; init; } = new();
    public int? ReferenceStartLine { get; init; }
    public int? AppendixStartLine { get; init; }
}

public sealed class PaperSection
{
    public string Title { get; init; } = "";
    public int Level { get; init; }
    public int StartLine { get; init; }
    public int EndLine { get; init; }
    public string Text { get; init; } = "";
    public string Canonical { get; init; } = "other";
}

/// <summary>
/// 参考文献条目。
/// </summary>
public sealed class ReferenceEntry
{
    public int 序号 { get; init; }
    public string 编号 { get; init; } = "";
    public string 原文 { get; init; } = "";
    public string 年份 { get; init; } = "";
    public string 作者线索 { get; init; } = "";
    public string 题名线索 { get; init; } = "";
    public string DOI { get; init; } = "";
    public string 格式风险 { get; init; } = "";
}

/// <summary>
/// 参考文献风险。
/// </summary>
public sealed class ReferenceRisk
{
    public string 文献问题 { get; init; } = "";
    public string 位置 { get; init; } = "";
    public string 风险说明 { get; init; } = "";
    public string 修改建议 { get; init; } = "";
    public string 是否需要人工核查 { get; init; } = "是";
}

/// <summary>
/// 证据链诊断项。
/// </summary>
public sealed class EvidenceChainItem
{
    public string 诊断项目 { get; init; } = "";
    public string 当前论文表现 { get; init; } = "";
    public string 是否充分 { get; init; } = "";
    public string 主要问题 { get; init; } = "";
    public string 修改建议 { get; init; } = "";
    public string 优先级 { get; init; } = "";
}

/// <summary>
/// 数据需求诊断项。
/// </summary>
public sealed class DataRequirementItem
{
    public string 项目 { get; init; } = "";
    public string 当前论文情况 { get; init; } = "";
    public string 是否充分 { get; init; } = "";
    public string 缺口说明 { get; init; } = "";
    public string 最低补充要求 { get; init; } = "";
    public string 推荐补充方案 { get; init; } = "";
    public string 优先级 { get; init; } = "";
}

/// <summary>
/// 数据缺口等级。
/// </summary>
public sealed class GapGrade
{
    public string 等级 { get; init; } = "";
    public string 判断标准 { get; init; } = "";
    public string 是否可继续分析 { get; init; } = "";
    public string 是否需要重新收集数据 { get; init; } = "";
    public string 是否可以生成训练用模拟数据 { get; init; } = "";
    public string 科研伦理风险 { get; init; } = "";
    public int 缺口数量 { get; init; }
    public int 最高论文类型匹配度 { get; init; }
    public string 修改建议 { get; init; } = "";
}

/// <summary>
/// 论文质量诊断问题。
/// </summary>
public sealed class QualityIssue
{
    public string 类别 { get; init; } = "";
    public string 具体问题 { get; init; } = "";
    public string 证据位置 { get; init; } = "";
    public string 为什么构成问题 { get; init; } = "";
    public string 最低修改要求 { get; init; } = "";
    public string 是否需要真实数据 { get; init; } = "";
    public string 模拟数据替代说明 { get; init; } = "";
    public string 优先级 { get; init; } = "";
}

/// <summary>
/// 论文质量诊断结果。
/// </summary>
public sealed class QualityDiagnosis
{
    public string 总体判断 { get; init; } = "";
    public List<string> 核心问题 { get; init; } = new();
    public List<string> 修改优先级 { get; init; } = new();
    public List<QualityIssue> 问题表 { get; init; } = new();
}

/// <summary>
/// 变量操作化表行。
/// </summary>
public sealed class VariablePlanRow
{
    public string 变量名称 { get; init; } = "";
    public string 中文标签 { get; init; } = "";
    public string 变量角色 { get; init; } = "";
    public string 理论含义 { get; init; } = "";
    public string 操作化方式 { get; init; } = "";
    public string 数据类型 { get; init; } = "";
    public string 测量题项指标 { get; init; } = "";
    public string 取值范围 { get; init; } = "";
    public string 数据来源 { get; init; } = "";
    public string 是否必须 { get; init; } = "";
    public string 分析用途 { get; init; } = "";
    public string 缺失风险 { get; init; } = "";
}

/// <summary>
/// 图表与三线表建议。
/// </summary>
public sealed class ChartTableSuggestion
{
    public string 论文部分 { get; init; } = "";
    public string 推荐图表表格 { get; init; } = "";
    public string 所需数据 { get; init; } = "";
    public string 当前是否具备 { get; init; } = "";
    public string 缺失内容 { get; init; } = "";
    public string 生成建议 { get; init; } = "";
}

/// <summary>
/// 语义诊断项。
/// </summary>
public sealed class SemanticDiagnosisItem
{
    public string 语义诊断项 { get; init; } = "";
    public string 风险等级 { get; init; } = "";
    public string 证据线索 { get; init; } = "";
    public string 修改建议 { get; init; } = "";
    public string 是否需要人工复核 { get; init; } = "是";
}
