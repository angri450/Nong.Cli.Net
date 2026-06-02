using System.Text.RegularExpressions;

namespace DocxCore;

/// <summary>
/// 变量操作化表和数据采集方案生成器。
/// 移植自 SynthDataDesktop paper_data_planner.py。
/// </summary>
public static class VariablePlanGenerator
{
    static readonly string[] VariableColumns =
    {
        "变量名称", "中文标签", "变量角色", "理论含义", "操作化方式", "数据类型",
        "测量题项/指标", "取值范围", "数据来源", "是否必须", "分析用途", "缺失风险",
    };

    /// <summary>变量操作化表的列名（用于 Table 方法）。</summary>
    public static string[] Columns => VariableColumns;

    static readonly Dictionary<string, List<VariablePlanRow>> DefaultVariables = new()
    {
        ["问卷调查型论文"] = new()
        {
            V("outcome_score", "因变量", "待解释的态度、行为或绩效结果", "多个 Likert 题项求均值", "连续/量表", "Y1-Y4", "1-5", "正式问卷采集", "是", "描述、相关、回归"),
            V("main_predictor", "自变量", "核心解释变量", "量表维度均值或指标值", "连续/量表", "X1-X4", "1-5", "正式问卷采集", "是", "相关、回归、中介/调节"),
            V("gender", "控制变量", "人口学背景", "分类编码", "分类", "性别", "0/1 或多分类", "正式问卷采集", "否", "控制变量"),
        },
        ["访谈研究型论文"] = new()
        {
            V("participant_id", "个案 ID", "受访者匿名编号", "不含真实姓名的编码", "文本", "P001-P999", "匿名 ID", "访谈记录", "是", "材料追踪"),
            V("theme_power", "访谈主题变量", "与核心机制相关的主题", "开放编码和主轴编码", "文本/分类", "主题出现与证据强度", "0/1/强度等级", "访谈转录", "是", "主题分析"),
        },
        ["田野调查型论文"] = new()
        {
            V("scene_id", "个案 ID", "观察场景编号", "地点-时间-事件编码", "文本", "S001", "匿名场景 ID", "田野日志", "是", "观察矩阵"),
            V("observed_behavior", "文本编码变量", "观察到的关键行为", "场景-行为-解释记录", "文本", "行为描述", "文本", "现场记录", "是", "过程解释"),
        },
        ["内容分析型论文"] = new()
        {
            V("document_id", "个案 ID", "文本样本编号", "平台-时间-序号", "文本", "D001", "匿名 ID", "文本样本", "是", "样本追踪"),
            V("frame_category", "文本编码变量", "文本框架或主题类别", "人工编码或双人编码", "分类", "编码类目", "类别 A/B/C", "内容分析编码表", "是", "频次和交叉分析"),
        },
        ["准实验 / 政策评估型论文"] = new()
        {
            V("unit_id", "个案 ID", "个体、学校、地区或企业编号", "匿名编号", "文本", "ID", "唯一 ID", "面板数据", "是", "固定效应"),
            V("post", "时间变量", "政策后时期", "政策发生后取 1", "二元", "时间", "0/1", "面板数据", "是", "DID"),
            V("treatment", "处理组变量", "受政策影响对象", "处理组取 1", "二元", "处理组", "0/1", "政策资料和数据匹配", "是", "DID"),
        },
    };

    /// <summary>按论文类型生成变量操作化表。</summary>
    public static List<VariablePlanRow> GenerateVariablePlan(string text, string? paperType = null)
    {
        var kind = paperType ?? PaperTypeClassifier.TopType(text);
        var extracted = ExtractVariableCandidates(text);
        if (extracted.Count == 0)
            return NormalizeVariableRows(DefaultVariables.GetValueOrDefault(kind, DefaultVariables["问卷调查型论文"]));
        return NormalizeVariableRows(extracted);
    }

    /// <summary>生成数据采集方案。</summary>
    public static Dictionary<string, object> GenerateDataCollectionPlan(string paperType, List<VariablePlanRow> variables)
    {
        if (paperType.Contains("问卷")) return new() { ["问卷方案"] = BuildQuestionnairePlan(variables) };
        if (paperType.Contains("访谈")) return new() { ["访谈方案"] = BuildInterviewPlan(variables) };
        if (paperType.Contains("田野")) return new() { ["田野方案"] = BuildFieldworkPlan() };
        if (paperType.Contains("内容分析")) return new() { ["内容分析方案"] = BuildContentAnalysisPlan(variables) };
        if (paperType.Contains("政策评估") || paperType.Contains("DID") || paperType.Contains("准实验"))
            return new() { ["DID方案"] = BuildDidPlan() };
        if (paperType.Contains("PSM")) return new() { ["PSM方案"] = BuildPsmPlan() };
        return new()
        {
            ["通用数据采集方案"] = new Dictionary<string, object>
            {
                ["最低要求"] = new[] { "数据来源", "样本选择", "变量操作化", "伦理说明", "清洗规则" },
                ["建议"] = "先确认研究类型，再生成更具体的问卷、访谈、田野或内容分析方案。",
            },
        };
    }

    // ---- internal helpers ----

    static List<VariablePlanRow> ExtractVariableCandidates(string text)
    {
        var structure = PaperStructureExtractor.BuildPaperStructure(text);
        var title = structure.Title;
        var abstract_ = structure.Abstract;
        var keywords = string.Join("、", structure.Keywords);
        var scan = $"{title}\n{abstract_}\n{keywords}\n{text}";

        var candidates = new List<(string label, string role)>();

        var rolePatterns = new Dictionary<string, string>
        {
            ["因变量"] = @"因变量(?:为|是|包括|：|:)\s*(?<body>[^。；;\n]{1,80})",
            ["自变量"] = @"自变量(?:为|是|包括|：|:)\s*(?<body>[^。；;\n]{1,80})",
            ["中介变量"] = @"中介变量(?:为|是|包括|：|:)\s*(?<body>[^。；;\n]{1,80})",
            ["调节变量"] = @"调节变量(?:为|是|包括|：|:)\s*(?<body>[^。；;\n]{1,80})",
            ["控制变量"] = @"控制变量(?:为|是|包括|：|:)\s*(?<body>[^。；;\n]{1,100})",
            ["处理组变量"] = @"(?:处理变量|处理组变量)(?:为|是|包括|：|:)\s*(?<body>[^。；;\n]{1,80})",
            ["时间变量"] = @"时间变量(?:为|是|包括|：|:)\s*(?<body>[^。；;\n]{1,80})",
        };

        foreach (var (role, pattern) in rolePatterns)
            foreach (Match m in Regex.Matches(scan, pattern))
                foreach (var label in SplitVariableLabels(m.Groups["body"].Value))
                    candidates.Add((label, role));

        foreach (Match m in Regex.Matches(scan, @"([一-龥A-Za-z0-9_]{2,30})对([一-龥A-Za-z0-9_]{2,30})的(?:影响|作用|效应)"))
        {
            candidates.Add((m.Groups[1].Value, "自变量"));
            candidates.Add((m.Groups[2].Value, "因变量"));
        }
        foreach (Match m in Regex.Matches(scan, @"([一-龥A-Za-z0-9_]{2,30})(?:是否|如何)?(?:影响|促进|抑制)([一-龥A-Za-z0-9_]{2,30})"))
        {
            candidates.Add((m.Groups[1].Value, "自变量"));
            candidates.Add((m.Groups[2].Value, "因变量"));
        }

        if ((scan.Contains("DID") || scan.Contains("双重差分")) && !candidates.Any(c => c.role == "处理组变量"))
        {
            candidates.Add(("处理组", "处理组变量"));
            candidates.Add(("政策后时期", "时间变量"));
            candidates.Add(("政策冲击", "政策冲击变量"));
        }

        var seen = new HashSet<(string, string)>();
        var result = new List<VariablePlanRow>();
        foreach (var (label, role) in candidates)
        {
            var cleaned = CleanLabel(label);
            if (string.IsNullOrEmpty(cleaned) || new[] { "变量", "指标", "模型", "数据", "研究", "影响" }.Contains(cleaned))
                continue;
            var key = (cleaned, role);
            if (seen.Add(key))
            {
                var index = result.Count + 1;
                result.Add(new VariablePlanRow
                {
                    变量名称 = SlugifyVariable(cleaned, role, index),
                    中文标签 = cleaned,
                    变量角色 = role,
                    理论含义 = TheoreticalMeaning(cleaned, role),
                    操作化方式 = Operationalization(cleaned, role, "通用"),
                    数据类型 = DataTypeForRole(role, "通用"),
                    测量题项指标 = MeasureHint(cleaned, role, "通用"),
                    取值范围 = ValueRangeForRole(role, "通用"),
                    数据来源 = SourceForRole(role, "通用"),
                    是否必须 = "是",
                    分析用途 = AnalysisUseForRole(role, "通用"),
                    缺失风险 = MissingRisk(role),
                });
            }
        }
        return result.Take(24).ToList();
    }

    static List<string> SplitVariableLabels(string chunk)
    {
        return Regex.Split(chunk, @"[、，,及和与\s]+")
            .Select(CleanLabel).Where(l => l.Length > 0).ToList();
    }

    static string CleanLabel(string label)
    {
        label = Regex.Replace(label.Trim(), @"[""'""（）()]", "");
        label = Regex.Replace(label, @"^(本文|本研究|主要|核心)", "");
        return label.Trim(' ', '：', ':', '，', ',', '。', '；', ';').Truncate(30);
    }

    // -- variable generation helpers --

    static readonly Dictionary<string, string> TermSlugs = new()
    {
        ["学习投入"] = "learning_engagement", ["教师支持"] = "teacher_support",
        ["学业成绩"] = "academic_performance", ["满意度"] = "satisfaction",
        ["自我效能"] = "self_efficacy", ["政策冲击"] = "policy_shock",
        ["处理组"] = "treatment_group", ["政策后时期"] = "post_period",
        ["是否接受处理"] = "treatment", ["处理前协变量"] = "pre_treatment_covariate",
        ["结果变量"] = "outcome",
    };

    static string SlugifyVariable(string label, string role, int index)
    {
        if (TermSlugs.TryGetValue(label, out var slug)) return slug;
        var ascii = Regex.Replace(label, @"[^A-Za-z0-9_]+", "_").Trim('_').ToLowerInvariant();
        if (ascii.Length > 0) return ascii.Truncate(40);
        var prefix = role switch
        {
            "因变量" => "outcome", "自变量" => "predictor", "中介变量" => "mediator",
            "调节变量" => "moderator", "控制变量" => "control", "分组变量" => "group",
            "时间变量" => "time", "处理组变量" => "treatment", "政策冲击变量" => "policy",
            "文本编码变量" => "code", "访谈主题变量" => "theme", _ => "var",
        };
        return $"{prefix}_{index}";
    }

    static string TheoreticalMeaning(string label, string role) => role switch
    {
        "因变量" => $"论文试图解释或预测的核心结果：{label}",
        "自变量" => $"论文用于解释结果差异的核心因素：{label}",
        "中介变量" => $"连接自变量与因变量的机制变量：{label}",
        "调节变量" => $"改变核心关系强弱或方向的边界条件：{label}",
        "控制变量" => $"可能影响结果、需要在模型中控制的背景因素：{label}",
        _ => $"与论文研究问题相关的变量或编码维度：{label}",
    };

    static string Operationalization(string label, string role, string paperType) => role switch
    {
        "因变量" or "自变量" or "中介变量" or "调节变量" => "设计 3-4 个 Likert 题项或使用可复核量表/指标，正式研究需报告来源和信效度。",
        "处理组变量" or "政策冲击变量" => "根据真实政策、项目或处理发生规则编码，不能事后按显著性选择。",
        "时间变量" => "按真实日期、年份、期次或政策前后时期编码。",
        _ when paperType.Contains("内容分析") || role.Contains("编码") => "制定编码手册，由至少两名编码员独立编码并检查一致性。",
        _ when paperType.Contains("访谈") || paperType.Contains("田野") => "通过访谈提纲、观察日志或主题编码形成可追踪材料。",
        _ => "补充测量题项、指标口径或编码规则。",
    };

    static string DataTypeForRole(string role, string paperType) => role switch
    {
        "处理组变量" or "政策冲击变量" => "二元",
        "时间变量" => "时间/期次",
        "分组变量" or "控制变量" => "分类/连续",
        _ when role.Contains("访谈") || role.Contains("文本") => "文本/分类",
        _ => "连续/量表",
    };

    static string MeasureHint(string label, string role, string paperType) => role switch
    {
        "因变量" or "自变量" or "中介变量" or "调节变量" => $"{label}_1 至 {label}_4，或来自正式数据库的同义指标",
        "处理组变量" => "0=对照组，1=处理组",
        "时间变量" => "0=政策前/前测，1=政策后/后测，或真实年份",
        _ when role.Contains("编码") => "编码类目、编码定义、示例和一致性结果",
        _ => "待按研究设计补充",
    };

    static string ValueRangeForRole(string role, string paperType) => role switch
    {
        "处理组变量" or "政策冲击变量" => "0/1",
        "时间变量" => "年份、期次或 0/1",
        "因变量" or "自变量" or "中介变量" or "调节变量" => "Likert 1-5 或指标实际范围",
        _ => "按编码规则确定",
    };

    static string SourceForRole(string role, string paperType) => role switch
    {
        _ when paperType.Contains("问卷") => "正式问卷采集",
        _ when paperType.Contains("内容分析") => "文本样本与编码表",
        _ when paperType.Contains("访谈") => "真实访谈材料与编码表",
        _ when paperType.Contains("田野") => "田野日志与观察记录",
        "处理组变量" or "政策冲击变量" or "时间变量" => "政策资料、项目记录或可复核二手数据",
        _ => "真实采集或可追溯二手数据",
    };

    static string AnalysisUseForRole(string role, string paperType) => role switch
    {
        "因变量" => "描述统计、差异检验、回归或因果模型结果变量",
        "自变量" => "相关、回归、机制分析或政策解释变量",
        "中介变量" => "中介效应与机制检验",
        "调节变量" => "交互项、简单斜率或分组分析",
        "控制变量" => "模型控制、PSM 匹配或稳健性检验",
        "时间变量" => "DID、事件研究、前后测或面板模型",
        "处理组变量" => "DID、PSM、实验/准实验分组",
        "政策冲击变量" => "DID 交互项或事件研究",
        "文本编码变量" => "频次统计、交叉分析、主题分布",
        "访谈主题变量" => "主题分析、机制解释、负案例分析",
        _ => "与研究问题和分析模型对应",
    };

    static string MissingRisk(string role) => role switch
    {
        "因变量" or "自变量" => "缺失则无法回答核心研究问题。",
        "处理组变量" or "时间变量" or "政策冲击变量" => "缺失则无法运行 DID、PSM 或实验/准实验设计。",
        "中介变量" or "调节变量" => "缺失则无法支撑机制或边界条件论证。",
        _ => "缺失会削弱模型控制、材料追踪或解释完整性。",
    };

    static List<VariablePlanRow> NormalizeVariableRows(List<VariablePlanRow> rows)
    {
        var result = new List<VariablePlanRow>();
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var role = string.IsNullOrEmpty(row.变量角色) ? "待确认角色" : row.变量角色;
            var label = !string.IsNullOrEmpty(row.中文标签) ? row.中文标签 : (!string.IsNullOrEmpty(row.变量名称) ? row.变量名称 : $"变量{i + 1}");
            result.Add(new VariablePlanRow
            {
                变量名称 = !string.IsNullOrEmpty(row.变量名称) ? row.变量名称 : SlugifyVariable(label, role, i + 1),
                中文标签 = label,
                变量角色 = role,
                理论含义 = !string.IsNullOrEmpty(row.理论含义) ? row.理论含义 : TheoreticalMeaning(label, role),
                操作化方式 = !string.IsNullOrEmpty(row.操作化方式) ? row.操作化方式 : Operationalization(label, role, "通用"),
                数据类型 = !string.IsNullOrEmpty(row.数据类型) ? row.数据类型 : DataTypeForRole(role, "通用"),
                测量题项指标 = !string.IsNullOrEmpty(row.测量题项指标) ? row.测量题项指标 : MeasureHint(label, role, "通用"),
                取值范围 = !string.IsNullOrEmpty(row.取值范围) ? row.取值范围 : ValueRangeForRole(role, "通用"),
                数据来源 = !string.IsNullOrEmpty(row.数据来源) ? row.数据来源 : SourceForRole(role, "通用"),
                是否必须 = !string.IsNullOrEmpty(row.是否必须) ? row.是否必须 : "是",
                分析用途 = !string.IsNullOrEmpty(row.分析用途) ? row.分析用途 : AnalysisUseForRole(role, "通用"),
                缺失风险 = !string.IsNullOrEmpty(row.缺失风险) ? row.缺失风险 : MissingRisk(role),
            });
        }
        return result;
    }

    static VariablePlanRow V(string name, string role, string meaning, string op, string dtype, string measure, string range, string source, string required, string use) => new()
    {
        变量名称 = name, 中文标签 = name, 变量角色 = role, 理论含义 = meaning, 操作化方式 = op,
        数据类型 = dtype, 测量题项指标 = measure, 取值范围 = range, 数据来源 = source,
        是否必须 = required, 分析用途 = use, 缺失风险 = MissingRisk(role),
    };

    static Dictionary<string, object> BuildQuestionnairePlan(List<VariablePlanRow> variables)
    {
        var items = new List<Dictionary<string, string>>();
        foreach (var row in variables)
        {
            if (new[] { "因变量", "自变量", "中介变量", "调节变量", "量表维度变量" }.Contains(row.变量角色))
            {
                var base_ = row.变量名称;
                for (int i = 1; i <= 3; i++)
                    items.Add(new()
                    {
                        ["维度"] = base_, ["题项编号"] = $"{base_}_{i}",
                        ["题项草稿"] = $"请根据 {base_} 的理论含义设计第 {i} 个 Likert 题项。",
                        ["量表"] = "1=非常不同意 至 5=非常同意", ["是否反向题"] = "建议人工确认",
                    });
            }
        }
        return new()
        {
            ["问卷结构"] = new[] { "知情同意", "筛选题", "核心量表", "背景信息", "结束语" },
            ["Likert题项"] = items.Count > 0 ? items.ToArray() : new[] { new Dictionary<string, string> { ["维度"] = "待确认变量", ["题项编号"] = "Q1", ["题项草稿"] = "请先完成变量操作化。", ["量表"] = "1-5", ["是否反向题"] = "否" } },
            ["样本量建议"] = "教学训练可用 100-300；正式论文需按模型复杂度、抽样方式和效应大小估算。",
            ["分析方案"] = new[] { "信度分析", "效度分析", "描述统计", "差异检验", "相关分析", "回归", "中介/调节" },
            ["伦理提示"] = "正式问卷需知情同意、匿名化和数据保存说明。",
        };
    }

    static Dictionary<string, object> BuildInterviewPlan(List<VariablePlanRow> variables)
    {
        var topics = variables.Take(6).Select(v => v.变量名称).ToList();
        if (topics.Count == 0) topics = new() { "研究经历", "关键机制", "边界条件" };
        return new()
        {
            ["访谈对象类型"] = new[] { "核心经历者", "管理者或教师", "普通参与者", "反例或负案例对象" },
            ["样本数量建议"] = "方法训练可 6-10 人；正式研究通常需说明饱和度和样本构成。",
            ["半结构式访谈提纲"] = topics.Select(t => $"请描述与「{t}」相关的具体经历、证据和反例。").ToArray(),
            ["知情同意说明"] = "说明研究目的、匿名处理、退出权利和资料用途。",
            ["编码主题"] = topics.ToArray(),
            ["负案例补充建议"] = "主动寻找与主假设不一致的案例。",
        };
    }

    static Dictionary<string, object> BuildFieldworkPlan() => new()
    {
        ["田野地点选择说明"] = "说明进入现场的理由、可观察性和伦理风险。",
        ["观察对象"] = new[] { "人物", "互动", "制度规则", "场景物品", "关键事件" },
        ["观察时间"] = "记录日期、时段、持续时间和特殊事件。",
        ["田野日志模板"] = new[] { "时间", "地点", "观察对象", "行为描述", "研究者解释", "反例", "伦理备注" },
        ["研究者位置说明"] = "说明研究者身份、进入关系和可能偏差。",
    };

    static Dictionary<string, object> BuildContentAnalysisPlan(List<VariablePlanRow> variables)
    {
        var categories = variables.Where(v => v.变量角色.Contains("编码")).Select(v => v.变量名称).ToList();
        if (categories.Count == 0) categories = new() { "主题类别", "情感倾向", "行动主体", "政策工具" };
        return new()
        {
            ["文本来源"] = "需明确平台、时间范围、关键词和抓取/下载方式。",
            ["抽样方式"] = new[] { "全样本", "分层抽样", "系统抽样", "关键词检索后人工筛选" },
            ["编码单位"] = "句子、段落、帖子、新闻或政策条款。",
            ["编码表"] = categories.Select(c => new Dictionary<string, string> { ["编码类目"] = c, ["定义"] = "需补充清晰定义", ["取值"] = "0/1 或分类", ["例子"] = "待人工补充" }).ToArray(),
            ["一致性检验"] = "建议至少两名编码员，报告 Cohen's kappa 或一致率。",
        };
    }

    static Dictionary<string, object> BuildDidPlan() => new()
    {
        ["处理组和对照组定义"] = "必须可解释、可复核，不能事后按显著性挑选。",
        ["时间变量"] = "至少包含政策前后多个时期。",
        ["政策冲击点"] = "明确政策实施时间和影响范围。",
        ["检验要求"] = new[] { "平行趋势", "稳健性", "安慰剂", "异质性", "事件研究" },
        ["禁止事项"] = "不能用模拟数据替代政策评估的真实面板数据。",
    };

    static Dictionary<string, object> BuildPsmPlan() => new()
    {
        ["处理变量"] = "接受处理/政策/项目的二元变量。",
        ["匹配变量"] = "处理前协变量，不能包含处理后结果。",
        ["匹配方法"] = new[] { "最近邻", "半径", "核匹配" },
        ["诊断"] = new[] { "平衡性检验", "common support", "匹配前后分布图" },
        ["稳健性"] = "更换匹配方法和卡尺，报告样本损失。",
    };
}
