using System.Text.RegularExpressions;

namespace Nong.Inspect;

/// <summary>
/// 论文质量诊断器。包含证据链、数据需求、缺口等级、图表建议、语义诊断和质量诊断。
/// 移植自 SynthDataDesktop paper_diagnostics.py + paper_quality_diagnosis.py。
/// </summary>
public static class PaperDiagnostics
{
    static readonly Dictionary<string, GapGradeDef> GapGrades = new()
    {
        ["A"] = new("A 级：数据支撑充分，仅需规范表述", "数据来源、变量、样本、方法、结果解释基本闭环。", "是", "否", "可选，仅用于课堂演示", "低，仍需保留来源与处理记录。"),
        ["B"] = new("B 级：数据基本可用，但变量或分析方法需补强", "已有数据但变量操作化、诊断或稳健性不足。", "可以，但需先修补关键变量和模型诊断。", "视缺口而定", "可以用于演练补强流程", "中，不能用模拟结果替代真实结果。"),
        ["C"] = new("C 级：数据不足，需要补充问卷、访谈、文本编码或二手数据", "研究问题明确但现有数据不足以支撑结论。", "不建议直接进入正式分析", "需要补充采集", "可以用于方法预演", "中高，需明确训练数据边界。"),
        ["D"] = new("D 级：研究问题与数据严重不匹配，需要重构研究设计", "数据能回答的问题与论文结论不是同一个问题。", "否，先重构研究设计", "通常需要", "仅可用于比较不同设计方案", "高，禁止补表格伪装支撑。"),
        ["E"] = new("E 级：目前不具备实证分析基础，不能通过补表格或模拟数据解决", "缺少明确问题、变量、数据来源或方法逻辑。", "否", "需要先重写研究设计", "仅可作为教学示例，不服务于该论文结论", "高。"),
    };

    // ---- evidence chain ----

    public static List<EvidenceChainItem> DiagnoseEvidenceChain(string text, List<PaperSection> sections)
    {
        var canonical = new HashSet<string>(sections.Select(s => s.Canonical));
        var items = new (string item, bool ok, string suggestion)[]
        {
            ("研究问题是否明确", HasAny(text, "研究问题", "研究目的", "本文旨在", "探讨", "检验"), "在引言末尾明确 1-3 个可回答的问题，避免只写宏观意义。"),
            ("研究对象是否具体", HasAny(text, "研究对象", "样本", "受访者", "案例", "企业", "学生"), "说明对象边界、样本来源、纳入排除标准。"),
            ("核心概念是否清晰", canonical.Contains("theory") || HasAny(text, "概念界定", "核心概念", "理论框架"), "将核心概念拆成可观察维度，避免只做口号式定义。"),
            ("研究假设是否可检验", HasAny(text, "H1", "假设", "命题"), "把假设改写为变量之间可检验的方向关系。"),
            ("变量体系是否能回答研究问题", HasAny(text, "因变量", "自变量", "控制变量", "变量说明", "测量"), "补充变量操作化表，明确每个变量服务哪个问题。"),
            ("研究设计是否支持因果语言", SupportsCausalDesign(text) || !HasStrongCausalClaim(text), "若只有横截面相关数据，应避免「导致」「促进」等强因果表述。"),
            ("经验材料是否足以支撑结论", canonical.Contains("results") && (canonical.Contains("data") || HasAny(text, "样本", "访谈", "编码", "数据库")), "用证据链表连接研究问题、数据、模型、结果和结论。"),
            ("讨论部分是否过度外推", !HasAny(text, "必然", "根本解决", "普遍适用", "完全证明"), "降低外推强度，补充边界条件和替代解释。"),
            ("贡献表述是否夸大", !HasAny(text, "填补空白", "重大理论价值", "首次提出", "创新性提出"), "把贡献改为具体解释了什么机制、补充了什么证据。"),
            ("是否存在理论叙述无数据支撑", !(canonical.Contains("theory") && !canonical.Contains("data") && !canonical.Contains("results")), "理论论文可不做统计，但经验论文必须补足数据来源与证据。"),
        };
        return items.Select(t => new EvidenceChainItem
        {
            诊断项目 = t.item,
            当前论文表现 = t.ok ? "已出现相关线索" : "未识别到充分线索",
            是否充分 = t.ok ? "是" : "否",
            主要问题 = t.ok ? "" : "该环节可能导致研究问题、数据和结论脱节。",
            修改建议 = t.suggestion,
            优先级 = t.ok ? "中" : "高",
        }).ToList();
    }

    // ---- data requirements ----

    public static List<DataRequirementItem> DiagnoseDataRequirements(string text)
    {
        var checks = new (string item, bool ok, string minimum)[]
        {
            ("当前论文是否已有数据支撑", HasAny(text, "数据来源", "样本", "受访者", "问卷", "数据库", "访谈", "观察", "编码"), "至少说明数据来源、采集方式、样本范围。"),
            ("数据来源是否清楚", HasAny(text, "数据来源", "来源于", "采集自", "数据库"), "写明数据从哪里来、何时采集、如何获取。"),
            ("样本量是否足够", Regex.IsMatch(text, @"(样本量|N\s*=|n\s*=|共\d+份|\d+名|\d+个样本)", RegexOptions.IgnoreCase), "给出样本量、有效样本数和剔除规则。"),
            ("抽样方式是否合理", HasAny(text, "随机抽样", "分层抽样", "便利抽样", "目的抽样", "理论抽样"), "说明抽样逻辑及其局限。"),
            ("变量是否可操作化", HasAny(text, "变量定义", "变量说明", "测量", "指标", "题项"), "补充变量操作化表。"),
            ("测量工具是否清楚", HasAny(text, "量表", "题项", "指标体系", "编码表", "访谈提纲"), "列出题项、指标或编码类目。"),
            ("统计方法是否与数据类型匹配", HasAny(text, "回归", "Logistic", "t检验", "ANOVA", "中介", "调节", "DID", "PSM", "固定效应"), "把变量类型、模型公式和诊断检查对应起来。"),
            ("是否需要稳健性或异质性分析", HasAny(text, "稳健性", "异质性", "安慰剂", "机制检验"), "高阶实证论文通常需要补充稳健性、机制或异质性检验。"),
            ("是否需要质性补充材料", !HasAny(text, "访谈", "田野", "负案例") && HasAny(text, "机制", "过程", "解释"), "若主张机制过程，考虑补充访谈、田野或文档证据。"),
        };
        return checks.Select(c => new DataRequirementItem
        {
            项目 = c.item,
            当前论文情况 = c.ok ? "已识别到相关说明" : "未识别到充分说明",
            是否充分 = c.ok ? "是" : "否",
            缺口说明 = c.ok ? "" : "该缺口会削弱论文的数据可复核性或方法匹配度。",
            最低补充要求 = c.minimum,
            推荐补充方案 = RecommendPlanForItem(c.item),
            优先级 = c.ok ? "中" : "高",
        }).ToList();
    }

    // ---- gap grade ----

    public static GapGrade DiagnoseGapGrade(List<EvidenceChainItem> evidence, List<DataRequirementItem> data, List<PaperTypeInfo> types)
    {
        var insufficient = evidence.Count(e => e.是否充分 == "否") + data.Count(d => d.是否充分 == "否");
        var topMatch = types.Count > 0 ? types[0].当前匹配度 : 0;
        string gradeKey = (insufficient, topMatch) switch
        {
            ( <= 2, >= 50) => "A",
            ( <= 4, _) => "B",
            ( <= 7, _) => "C",
            (_, < 20) => "D",
            _ => "E",
        };
        var def = GapGrades[gradeKey];
        return new GapGrade
        {
            等级 = def.等级, 判断标准 = def.判断标准,
            是否可继续分析 = def.是否可继续分析, 是否需要重新收集数据 = def.是否需要重新收集数据,
            是否可以生成训练用模拟数据 = def.是否可以生成训练用模拟数据, 科研伦理风险 = def.科研伦理风险,
            缺口数量 = insufficient, 最高论文类型匹配度 = topMatch,
            修改建议 = GradeSuggestion(gradeKey),
        };
    }

    // ---- chart & table suggestions ----

    public static List<ChartTableSuggestion> RecommendChartsAndTables(string text, string paperType)
    {
        var items = new (string part, string table, string needed, bool ok, string suggestion)[]
        {
            ("方法与数据", "变量操作化三线表", "变量名、角色、测量方式、来源", HasAny(text, "变量", "测量"), "先生成变量操作化表"),
            ("样本说明", "描述统计表", "样本背景变量、核心变量", HasAny(text, "样本", "变量"), "导出 GB/T 或 APA 表格"),
            ("结果部分", "回归模型三线表", "因变量、自变量、控制变量", HasAny(text, "回归", "模型"), "按模型递进排列"),
            ("机制分析", "中介/调节效应表与路径图", "中介变量、调节变量、Bootstrap 结果", HasAny(text, "中介", "调节"), "仅在模型真实运行后生成结果段落"),
            ("政策评估", "DID 平行趋势图", "处理组、对照组、时间变量、政策冲击点", text.Contains("DID") || text.Contains("双重差分"), "缺少多期数据时不能生成正式结论"),
            ("质性材料", "访谈编码表 / 田野观察矩阵", "转录文本、编码类目、场景记录", HasAny(text, "访谈", "田野", "编码"), "保留负案例和伦理说明"),
            ("文献综述", "证据矩阵", "核心文献、理论观点、方法、结论", HasAny(text, "文献综述", "研究综述"), "不自动补造文献"),
        };
        return items.Select(i => new ChartTableSuggestion
        {
            论文部分 = i.part, 推荐图表表格 = i.table, 所需数据 = i.needed,
            当前是否具备 = i.ok ? "是" : "否", 缺失内容 = i.ok ? "" : i.needed, 生成建议 = i.suggestion,
        }).ToList();
    }

    // ---- semantic diagnosis ----

    public static List<SemanticDiagnosisItem> DiagnoseResearchDesignSemantics(string text, string paperType)
    {
        var rows = new List<SemanticDiagnosisItem>();
        void Add(string item, string risk, string evidence, string suggestion)
            => rows.Add(new() { 语义诊断项 = item, 风险等级 = risk, 证据线索 = evidence, 修改建议 = suggestion });

        if (HasStrongCausalClaim(text) && !SupportsCausalDesign(text))
            Add("因果语言与研究设计不匹配", "高",
                "存在导致、决定、因果等强表述，但未识别到实验、DID、PSM、工具变量或固定效应等设计线索。",
                "将强因果表述降级为相关、关联或可能机制；若坚持因果命题，需重构识别策略。");

        if (text.Contains("相关") && HasStrongCausalClaim(text))
            Add("相关分析被写成因果解释", "高",
                "同时出现相关和强因果措辞。", "相关分析只能说明统计关联，不能写成导致或决定。");

        if (text.Contains("机制") && !HasAny(text, "中介", "访谈", "过程追踪", "田野", "机制检验"))
            Add("机制主张缺少机制证据", "中",
                "出现机制表述，但缺少中介模型、访谈、过程追踪或田野材料线索。", "补充机制检验或质性材料；否则改写为可能解释。");

        if (paperType.Contains("混合方法") && !(text.Contains("问卷") && (text.Contains("访谈") || text.Contains("田野") || text.Contains("编码"))))
            Add("混合方法证据链可能只是拼接", "中",
                "未识别到量化与质性材料围绕同一问题相互解释。", "说明两类证据如何互补、解释或验证同一机制。");

        if ((text.Contains("DID") || text.Contains("双重差分")) && !HasAny(text, "平行趋势", "政策前", "政策后", "事件研究"))
            Add("DID 识别条件不足", "高",
                "识别到 DID，但未识别到平行趋势或政策前后多期说明。", "补充平行趋势检验、政策冲击点、处理组/对照组定义和稳健性。");

        if ((text.Contains("PSM") || text.Contains("倾向得分")) && !HasAny(text, "平衡性", "common support", "匹配前后", "卡尺"))
            Add("PSM 诊断不足", "中",
                "识别到 PSM，但未识别到平衡性或共同支撑检查。", "补充倾向得分模型、匹配方法、平衡性检验和 common support 图。");

        if (HasAny(text, "填补空白", "重大理论价值", "首次提出", "创新性提出") && !HasAny(text, "不同于", "机制", "边界条件", "解释"))
            Add("贡献表述可能空泛", "中",
                "出现高强度贡献表述，但缺少具体理论差异、机制或边界条件。", "把贡献改写为具体解释对象、机制、适用范围和相对已有研究的差异。");

        if (rows.Count == 0)
            Add("未发现明显高风险语义问题", "低",
                "规则未命中强因果、机制缺口或贡献夸大线索。", "仍需导师和方法顾问人工复核。");

        return rows;
    }

    // ---- paper quality diagnosis ----

    public static QualityDiagnosis DiagnosePaperQuality(
        string text,
        List<EvidenceChainItem> evidenceRows,
        List<DataRequirementItem> dataRows,
        GapGrade gapGrade,
        List<PaperSection> sections)
    {
        var fatal = new List<QualityIssue>();
        var structural = new List<QualityIssue>();
        var surface = new List<QualityIssue>();

        foreach (var row in evidenceRows)
        {
            if (row.是否充分 != "否") continue;
            var category = row.诊断项目 is "研究设计是否支持因果语言" or "经验材料是否足以支撑结论"
                ? "一类：不可逆致命伤" : "二类：可修复结构性问题";
            var target = category.StartsWith("一类") ? fatal : structural;
            target.Add(Issue(text, category, row.诊断项目, row.修改建议, "高",
                "该问题会削弱研究问题、证据材料、方法设计和结论之间的闭环关系。",
                row.修改建议, "视具体缺口而定", "可以用于方法演示，但不能替代真实证据。"));
        }

        foreach (var row in dataRows)
        {
            if (row.是否充分 != "否") continue;
            structural.Add(Issue(text, "二类：可修复结构性问题", row.项目, row.推荐补充方案 ?? "", row.优先级,
                "数据来源、样本、变量或方法缺口会使结果不可复核或无法回答研究问题。",
                row.最低补充要求 ?? "", row.优先级 == "高" ? "是" : "可能需要",
                "可以生成训练数据演练分析流程，但正式论文必须使用真实、合规、可追溯数据。"));
        }

        if (HasAny(text, "具有重要意义", "填补空白", "重大理论价值", "创新性提出"))
            surface.Add(Issue(text, "三类：表面表达问题", "贡献表述可能空泛或过度", "改为具体说明解释了什么机制、补充了什么证据和适用边界。",
                "中", "贡献语言高于证据会触发审稿人对理论贡献的质疑。", "删除或降级空泛贡献语句。", "否", "不适用"));

        if (!sections.Any(s => s.Canonical == "results") || !Regex.IsMatch(text, @"(表|Table)\s*\d"))
            surface.Add(Issue(text, "三类：表面表达问题", "图表支撑不足", "补充变量操作化表、描述统计表和核心模型表。",
                "中", "缺少图表会降低变量、样本和模型结果的可审查性。", "至少补充变量操作化表和描述统计表。", "视分析需要而定",
                "可以用训练数据演示表格格式。"));

        var gradeKey = gapGrade.等级.Length > 0 ? gapGrade.等级[0].ToString() : "C";
        var overall = gradeKey switch
        {
            "D" or "E" => "不建议进入正式实证分析，需先重构研究设计或重新收集数据。",
            "C" => "需要重大修改，优先补真实数据来源、变量操作化和方法匹配。",
            "B" => "基本可进入补强阶段，但正式提交前需完成模型诊断和数据说明。",
            _ => "数据与方法闭环较完整，主要风险在表述规范和人工复核。",
        };

        var allIssues = fatal.Concat(structural).ToList();
        return new QualityDiagnosis
        {
            总体判断 = overall,
            核心问题 = allIssues.Take(5).Select(i => i.具体问题).ToList(),
            修改优先级 = new() { "先修复致命问题", "再补变量与数据", "最后统一图表、语言和格式" },
            问题表 = fatal.Concat(structural).Concat(surface).ToList(),
        };
    }

    // ---- helpers ----

    static bool HasAny(string text, params string[] keywords)
        => keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    static bool SupportsCausalDesign(string text)
        => HasAny(text, "随机实验", "实验组", "DID", "双重差分", "PSM", "工具变量", "断点回归", "固定效应");

    static bool HasStrongCausalClaim(string text)
        => HasAny(text, "导致", "决定", "显著促进", "抑制了", "因果");

    static string RecommendPlanForItem(string item)
    {
        if (item.Contains("问卷") || item.Contains("测量") || item.Contains("变量"))
            return "生成问卷题项、变量字典和数据录入模板，再由用户真实采集。";
        if (item.Contains("质性")) return "生成访谈提纲、田野日志或编码表，正式材料需真实采集并伦理合规。";
        if (item.Contains("稳健性")) return "补充模型诊断、替代变量、分组回归或稳健标准误。";
        return "补充可复核文字说明和数据处理记录。";
    }

    static string GradeSuggestion(string grade) => grade switch
    {
        "A" => "规范变量表、图表和结果表达即可。",
        "B" => "优先补变量操作化、模型诊断和稳健性检验。",
        "C" => "先补充真实问卷、访谈、文本编码或二手数据，再做正式分析。",
        "D" => "重写研究问题与研究设计，避免用不匹配数据支撑结论。",
        "E" => "先完成研究问题、理论框架和数据采集设计，暂不进入实证结果写作。",
        _ => "",
    };

    static QualityIssue Issue(string text, string category, string problem, string suggestion, string priority,
        string why, string minimum, string needReal, string simulatedNote)
    {
        var evidence = FindEvidenceLocation(text, problem);
        return new QualityIssue
        {
            类别 = category, 具体问题 = problem, 证据位置 = evidence,
            为什么构成问题 = why, 最低修改要求 = minimum, 是否需要真实数据 = needReal,
            模拟数据替代说明 = simulatedNote, 优先级 = priority,
        };
    }

    static string FindEvidenceLocation(string text, string problem)
    {
        var keywords = problem switch
        {
            "研究设计是否支持因果语言" => new[] { "导致", "促进", "影响", "因果" },
            "经验材料是否足以支撑结论" => new[] { "结论", "conclusion", "结果" },
            "贡献表述可能空泛或过度" => new[] { "填补", "创新", "贡献", "意义" },
            "图表支撑不足" => new[] { "表", "图", "Table", "Figure" },
            _ => Array.Empty<string>(),
        };
        foreach (var kw in keywords)
        {
            var idx = text.IndexOf(kw, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) return $"...{text[Math.Max(0, idx - 20)..Math.Min(text.Length, idx + 40)]}...";
        }
        return "全文搜索";
    }
}

internal sealed record GapGradeDef(
    string 等级, string 判断标准, string 是否可继续分析, string 是否需要重新收集数据,
    string 是否可以生成训练用模拟数据, string 科研伦理风险);
