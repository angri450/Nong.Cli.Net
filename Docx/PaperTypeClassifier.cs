using System.Text.RegularExpressions;

namespace DocxCore;

/// <summary>
/// 论文类型分类器。基于 16 种论文类型的关键词匹配进行识别。
/// 移植自 SynthDataDesktop paper_diagnostics.py 的 PAPER_TYPE_RULES。
/// </summary>
public static class PaperTypeClassifier
{
    static readonly Dictionary<string, PaperTypeRule> Rules = new()
    {
        ["问卷调查型论文"] = new("问卷,量表,Likert,信度,效度,调查",
            "问卷原始数据、题项、变量标签、缺失值规则、样本背景信息",
            "信度、效度、描述统计、差异检验、相关、回归、中介/调节",
            "完整题项、样本来源、样本量、变量编码和清洗规则"),
        ["实验研究型论文"] = new("实验,实验组,控制组,随机,前测,后测",
            "实验组/控制组、前后测指标、随机分配记录、实验操作记录",
            "t 检验、ANOVA、ANCOVA、配对检验、效应量和置信区间",
            "分组变量、结果变量、实验程序、样本流失说明"),
        ["准实验 / 政策评估型论文"] = new("DID,双重差分,政策冲击,处理组,对照组,平行趋势,事件研究",
            "处理组、对照组、时间变量、政策冲击点、结果变量和控制变量",
            "DID、事件研究、平行趋势检验、稳健性和安慰剂检验",
            "政策前后多期数据、可解释处理组与对照组定义"),
        ["访谈研究型论文"] = new("访谈,半结构,受访者,编码,扎根理论",
            "访谈对象构成、访谈提纲、转录文本、编码表、知情同意记录",
            "主题分析、开放编码、主轴编码、负案例分析",
            "样本选择逻辑、访谈数量、转录和编码过程说明"),
        ["田野调查型论文"] = new("田野,参与观察,观察记录,田野日志,场景",
            "田野地点、观察对象、观察时段、田野日志、研究者位置说明",
            "场景-行为-解释矩阵、负案例记录、过程叙事",
            "进入现场方式、观察周期、伦理风险说明"),
        ["内容分析型论文"] = new("内容分析,文本,编码表,编码员,一致性,语料",
            "文本来源、抽样规则、编码单位、编码表、编码员一致性记录",
            "频次统计、交叉表、编码一致性、主题分布可视化",
            "可复核语料范围、编码类目和一致性检验"),
        ["案例研究型论文"] = new("案例,个案,过程追踪,案例选择",
            "案例选择依据、事件过程、文档材料、访谈或观察材料",
            "过程追踪、机制证据表、替代解释排除",
            "案例边界、材料来源、反例或替代解释处理"),
        ["混合方法研究"] = new("混合方法,问卷,访谈,量化,质性",
            "量化数据与质性材料需要围绕同一研究问题相互解释",
            "顺序解释、并行三角互证、机制解释",
            "说明两类证据如何回答同一问题，而不是机械拼接"),
        ["二手数据库实证研究"] = new("数据库,面板数据,年鉴,公开数据,二手数据",
            "数据库名称、变量口径、时间范围、样本筛选、缺失处理",
            "OLS、Logit、固定效应、稳健标准误、稳健性检验",
            "可追溯数据源、变量定义、样本筛选流程"),
        ["文献综述型论文"] = new("文献综述,研究进展,系统综述,综述",
            "检索数据库、检索式、纳入排除标准、文献编码表",
            "主题综述、系统综述流程图、证据矩阵",
            "可复核检索策略和文献筛选记录"),
        ["理论阐释型论文"] = new("理论阐释,理论建构,概念框架,理论模型",
            "理论文本、经典文献、概念比较表；不宜强行补统计数据",
            "概念辨析、理论命题、适用边界说明",
            "理论问题、概念边界和与既有理论的差异"),
        ["本科毕业论文"] = new("本科,毕业论文",
            "课程可复核训练数据或真实采集数据",
            "描述统计、相关、回归或规范质性分析",
            "数据来源和变量说明完整"),
        ["硕士论文"] = new("硕士,学位论文",
            "足以支撑机制论证的数据",
            "模型诊断、稳健性、异质性或机制分析",
            "研究设计可回答核心问题"),
        ["博士论文"] = new("博士,博士学位",
            "多源证据和可解释识别策略",
            "理论贡献、机制证据、稳健性和边界分析",
            "数据与理论问题高度匹配"),
        ["期刊投稿论文"] = new("投稿,期刊,审稿",
            "符合目标期刊方法门槛的数据与证据链",
            "严谨识别、稳健性、替代解释和贡献边界",
            "可复核数据来源和清晰方法贡献"),
        ["课题申报书或结项报告"] = new("课题,申报,结项,项目",
            "研究计划、阶段数据、成果证据和应用场景",
            "需求论证、过程评估、效果评估",
            "目标、样本、数据采集和成果指标清楚"),
    };

    /// <summary>对文本进行 16 种论文类型匹配度排序。</summary>
    public static List<PaperTypeInfo> Classify(string text)
    {
        var lower = text.ToLowerInvariant();
        var rows = new List<PaperTypeInfo>();
        foreach (var (paperType, rule) in Rules)
        {
            var hits = rule.Keywords.Where(kw => lower.Contains(kw.ToLowerInvariant())).ToList();
            var score = Math.Min(100, (int)Math.Round((double)hits.Count / Math.Max(rule.Keywords.Count, 1) * 100));
            rows.Add(new PaperTypeInfo
            {
                论文类型 = paperType,
                判断依据 = hits.Count > 0 ? string.Join("、", hits) : "未明显命中关键词，需要人工确认",
                当前匹配度 = score,
                推荐数据 = rule.RecommendedData,
                推荐方法 = rule.RecommendedMethods,
                主要缺口 = "按最低数据要求核对：" + rule.MinimumData,
                风险等级 = score == 0 ? "高" : (score < 50 ? "中" : "低"),
            });
        }
        rows.Sort((a, b) => b.当前匹配度.CompareTo(a.当前匹配度));
        return rows;
    }

    /// <summary>获取排名第一的论文类型名称。</summary>
    public static string TopType(string text) => Classify(text).FirstOrDefault()?.论文类型 ?? "问卷调查型论文";
}

internal sealed record PaperTypeRule(
    List<string> Keywords, string RecommendedData, string RecommendedMethods, string MinimumData)
{
    public PaperTypeRule(string keywords, string recommendedData, string recommendedMethods, string minimumData)
        : this(keywords.Split(',').Select(k => k.Trim()).Where(k => k.Length > 0).ToList(),
              recommendedData, recommendedMethods, minimumData) { }
}
