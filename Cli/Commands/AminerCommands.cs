using System.CommandLine;
using System.Text.Json;
using Angri450.Nong.Aminer;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

public static class AminerCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("aminer", "AMiner — based on real API docs (open.aminer.cn)");
        // FREE
        cmd.AddCommand(CreateScholar(jsonOpt));
        cmd.AddCommand(CreatePaper(jsonOpt));
        cmd.AddCommand(CreatePaperRec(jsonOpt));
        cmd.AddCommand(CreatePatent(jsonOpt));
        cmd.AddCommand(CreateOrg(jsonOpt));
        cmd.AddCommand(CreateVenue(jsonOpt));
        cmd.AddCommand(CreatePaperInfo(jsonOpt));
        cmd.AddCommand(CreatePatentInfo(jsonOpt));
        // PAID — 论文
        cmd.AddCommand(CreatePaperPro(jsonOpt));
        cmd.AddCommand(CreatePaperDetail(jsonOpt));
        cmd.AddCommand(CreatePaperCitations(jsonOpt));
        cmd.AddCommand(CreatePaperQa(jsonOpt));
        cmd.AddCommand(CreateDeepResearch(jsonOpt));
        // PAID — 学者
        cmd.AddCommand(CreateScholarDetail(jsonOpt));
        cmd.AddCommand(CreateScholarFigure(jsonOpt));
        cmd.AddCommand(CreateScholarStat(jsonOpt));
        cmd.AddCommand(CreateScholarPapers(jsonOpt));
        cmd.AddCommand(CreateScholarPatents(jsonOpt));
        cmd.AddCommand(CreateScholarProjects(jsonOpt));
        // PAID — 机构/专利
        cmd.AddCommand(CreateOrgDetail(jsonOpt));
        cmd.AddCommand(CreateOrgPatents(jsonOpt));
        cmd.AddCommand(CreatePatentDetail(jsonOpt));
        return cmd;
    }

    // ── FREE: aminer scholar ───────────────────────────
    static Command CreateScholar(Option<bool> jo)
    {
        var name = new Option<string?>("--name", "Scholar name");
        var org = new Option<string?>("--org", "Institution");
        var orgIds = new Option<string[]?>("--org-ids", "Institution IDs");
        var off = new Option<int>("--offset", () => 0, "Offset");
        var size = new Option<int>("--size", () => 10, "Max 10");
        var cmd = new Command("scholar", "[FREE] Search scholars") { name, org, orgIds, off, size };
        cmd.SetHandler(async (string? n, string? o, string[]? oids, int offv, int sz, bool j) => {
            var r = await new AminerClient().SearchScholarsAsync(n, o, oids, offv, sz);
            W(jo, "scholar", $"{r.Total} scholars", r, r.Items.Select(s => (object)new {
                s.Id, s.Name, s.NameZh, display = s.DisplayName, s.CitationCount,
                s.Interests, s.Org, s.OrgId, s.OrgZh, s.Nation, profileUrl = s.ProfileUrl
            }));
        }, name, org, orgIds, off, size, jo); return cmd;
    }

    // ── FREE: aminer paper ─────────────────────────────
    static Command CreatePaper(Option<bool> jo)
    {
        var q = new Option<string>("--title", "Paper title") { IsRequired = true };
        var pg = new Option<int>("--page", () => 1, "Page (starts at 1)");
        var sz = new Option<int>("--size", () => 10, "Max 20");
        var cmd = new Command("paper", "[FREE] Search papers by title") { q, pg, sz };
        cmd.SetHandler(async (string t, int p, int s, bool j) => {
            var r = await new AminerClient().SearchPapersAsync(t, p, s);
            W(jo, "paper", $"{r.Total} papers", r, r.Items.Select(P));
        }, q, pg, sz, jo); return cmd;
    }

    // ── FREE: aminer rec ───────────────────────────────
    static Command CreatePaperRec(Option<bool> jo)
    {
        var author = new Option<string?>("--author", "Scholar name");
        var authorOrg = new Option<string?>("--author-org", "Scholar institution");
        var topics = new Option<string[]?>("--topics", "Interest topics");
        var authorId = new Option<string?>("--author-id", "AMiner author ID");
        var startYr = new Option<int?>("--start-year", "Start year filter");
        var endYr = new Option<int?>("--end-year", "End year filter");
        var lang = new Option<string?>("--lang", "zh or en");
        var sz = new Option<int>("--size", () => 5, "Max 20");
        var cmd = new Command("rec", "[FREE] Recommend papers by scholar/topics") { author, authorOrg, topics, authorId, startYr, endYr, lang, sz };
        cmd.SetHandler(async (context) => {
            var cv = context.ParseResult;
            var r = await new AminerClient().RecommendPapersAsync(
                cv.GetValueForOption(author), cv.GetValueForOption(authorOrg), cv.GetValueForOption(topics),
                cv.GetValueForOption(authorId), cv.GetValueForOption(startYr), cv.GetValueForOption(endYr),
                cv.GetValueForOption(lang), cv.GetValueForOption(sz));
            var items = r.Items.SelectMany(rec => rec.Papers.Select(P)).ToList();
            var o = JsonOutput.Ok("aminer rec", $"{r.Total} papers", new { r.Success, r.ErrorCode, r.ErrorMessage, total = r.Total, items });
            o.Metrics["items"] = items.Count;
            Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
        }); return cmd;
    }

    // ── FREE: aminer patent ────────────────────────────
    static Command CreatePatent(Option<bool> jo)
    {
        var q = new Option<string>("--query", "Patent keyword") { IsRequired = true };
        var pg = new Option<int>("--page", () => 0, "Page");
        var sz = new Option<int>("--size", () => 20, "Max 20");
        var cmd = new Command("patent", "[FREE] Search patents") { q, pg, sz };
        cmd.SetHandler(async (string qv, int p, int s, bool j) => {
            var r = await new AminerClient().SearchPatentsAsync(qv, p, s);
            W(jo, "patent", $"{r.Total} patents", r, r.Items.Select(Pat));
        }, q, pg, sz, jo); return cmd;
    }

    // ── FREE: aminer org ───────────────────────────────
    static Command CreateOrg(Option<bool> jo)
    {
        var q = new Option<string[]>("--orgs", "Organization names") { AllowMultipleArgumentsPerToken = true, IsRequired = true };
        var cmd = new Command("org", "[FREE] Search orgs by name") { q };
        cmd.SetHandler(async (string[] qv, bool j) => {
            var r = await new AminerClient().SearchOrgsAsync(qv);
            W(jo, "org", $"{r.Total} orgs", r, r.Items.Select(o => (object)new { o.Id, o.Name, display = o.DisplayName, o.Aliases }));
        }, q, jo); return cmd;
    }

    // ── FREE: aminer venue ─────────────────────────────
    static Command CreateVenue(Option<bool> jo)
    {
        var q = new Option<string>("--name", "Journal name") { IsRequired = true };
        var cmd = new Command("venue", "[FREE] Search venues by name") { q };
        cmd.SetHandler(async (string qv, bool j) => {
            var r = await new AminerClient().SearchVenuesAsync(qv);
            W(jo, "venue", $"{r.Total} venues", r, r.Items.Select(v => (object)new { v.Id, v.Name, v.NameZh, v.VenueType, v.Aliases }));
        }, q, jo); return cmd;
    }

    // ── FREE: aminer paper-info ────────────────────────
    static Command CreatePaperInfo(Option<bool> jo)
    {
        var ids = new Option<string[]>("--ids", "Paper IDs (max 100)") { AllowMultipleArgumentsPerToken = true, IsRequired = true };
        var cmd = new Command("paper-info", "[FREE] Get paper details by IDs") { ids };
        cmd.SetHandler(async (string[] iv, bool j) => {
            var r = await new AminerClient().GetPaperInfoAsync(iv);
            W(jo, "paper-info", $"{r.Total} papers", r, r.Items.Select(P));
        }, ids, jo); return cmd;
    }

    // ── FREE: aminer patent-info ───────────────────────
    static Command CreatePatentInfo(Option<bool> jo)
    {
        var id = new Option<string>("--id", "Patent ID") { IsRequired = true };
        var cmd = new Command("patent-info", "[FREE] Get patent info by ID") { id };
        cmd.SetHandler(async (string iv, bool j) => {
            var r = await new AminerClient().GetPatentInfoAsync(iv);
            W(jo, "patent-info", "1 patent", r, r.Items.Select(Pat));
        }, id, jo); return cmd;
    }

    // ── PAID: paper pro ────────────────────────────────
    static Command CreatePaperPro(Option<bool> jo)
    {
        var title = new Option<string?>("--title"); var kw = new Option<string?>("--keyword");
        var abs = new Option<string?>("--abstract"); var au = new Option<string?>("--author");
        var org = new Option<string?>("--org"); var vn = new Option<string?>("--venue");
        var ord = new Option<string?>("--order"); var pg = new Option<int>("--page", () => 0);
        var sz = new Option<int>("--size", () => 5); var cmd = new Command("paper-pro", "[¥0.01] Multi-field search") { title, kw, abs, au, org, vn, ord, pg, sz };
        cmd.SetHandler(async (context) => {
            var cv = context.ParseResult;
            var r = await new AminerClient().SearchPapersProAsync(cv.GetValueForOption(title), cv.GetValueForOption(kw),
                cv.GetValueForOption(abs), cv.GetValueForOption(au), cv.GetValueForOption(org),
                cv.GetValueForOption(vn), cv.GetValueForOption(ord), cv.GetValueForOption(pg), cv.GetValueForOption(sz));
            W(jo, "paper-pro", $"{r.Total} papers", r, r.Items.Select(P));
        }); return cmd;
    }

    // ── PAID: paper detail ─────────────────────────────
    static Command CreatePaperDetail(Option<bool> jo)
    { var id = new Option<string>("--id", "Paper ID") { IsRequired = true }; var cmd = new Command("paper-detail", "[¥0.01] Full paper details") { id };
        cmd.SetHandler(async (string iv, bool j) => { var r = await new AminerClient().GetPaperDetailAsync(iv); W(jo, "paper-detail", "1 paper", r, r.Items.Select(P)); }, id, jo); return cmd; }

    // ── PAID: paper citations ──────────────────────────
    static Command CreatePaperCitations(Option<bool> jo)
    { var id = new Option<string>("--id", "Paper ID") { IsRequired = true }; var cmd = new Command("paper-citations", "[¥0.10] Paper citations") { id };
        cmd.SetHandler(async (string iv, bool j) => { var r = await new AminerClient().GetPaperCitationsAsync(iv); W(jo, "paper-citations", $"{r.Total} citations", r, r.Items.Select(P)); }, id, jo); return cmd; }

    // ── PAID: paper QA ─────────────────────────────────
    static Command CreatePaperQa(Option<bool> jo)
    {
        var q = new Option<string>("--query", "Natural language or keywords") { IsRequired = true };
        var topicHigh = new Option<string?>("--topic-high", "Must-appear keywords (JSON array)"); var sci = new Option<bool>("--sci", () => false);
        var sz = new Option<int>("--size", () => 10); var cmd = new Command("paper-qa", "[¥0.05] Semantic paper search") { q, topicHigh, sci, sz };
        cmd.SetHandler(async (string qv, string? th, bool sc, int s, bool j) => {
            var r = await new AminerClient().SearchPapersQaAsync(query: qv, topicHigh: th, sciOnly: sc ? true : null, size: s);
            W(jo, "paper-qa", $"{r.Total} papers", r, r.Items.Select(P));
        }, q, topicHigh, sci, sz, jo); return cmd;
    }

    // ── PAID: deep research ────────────────────────────
    static Command CreateDeepResearch(Option<bool> jo)
    {
        var q = new Option<string>("--query", "Research question") { IsRequired = true };
        var type = new Option<int>("--type", () => 1, "1=full lib, 2=preprint, 3=medical");
        var web = new Option<bool>("--web", () => false, "Search web too");
        var cmd = new Command("deep-research", "[¥0.80] AMiner Deep Research (SSE)") { q, type, web };
        cmd.SetHandler(async (string qv, int tp, bool wb, bool j) => {
            var answer = new System.Text.StringBuilder();
            await new AminerClient().DeepResearchAsync(qv, tp, wb, chunk => { Console.Write(chunk); answer.Append(chunk); return Task.CompletedTask; });
            Console.Error.WriteLine($"\n[DONE — {answer.Length} chars]");
            if (j) { var o = JsonOutput.Ok("aminer deep-research", $"{answer.Length} chars", new { answer = answer.ToString() }); Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts)); }
        }, q, type, web, jo); return cmd;
    }

    // ── PAID: scholar detail ───────────────────────────
    static Command CreateScholarDetail(Option<bool> jo)
    { var id = new Option<string>("--id", "Scholar ID") { IsRequired = true }; var cmd = new Command("scholar-detail", "[¥1.00] Full scholar profile") { id };
        cmd.SetHandler(async (string iv, bool j) => {
            var r = await new AminerClient().GetScholarDetailAsync(iv);
            W(jo, "scholar-detail", "1 scholar", r, r.Items.Select(s => (object)new {
                s.Id, s.Name, s.NameZh, s.Bio, s.BioZh, s.Education, s.EducationZh,
                s.Position, s.PositionZh, s.Orgs, s.OrgZhs,
                honors = s.Honors?.Select(h => new { h.Award, h.Year, h.Reason })
            }));
        }, id, jo); return cmd; }

    // ── PAID: scholar figure ───────────────────────────
    static Command CreateScholarFigure(Option<bool> jo)
    { var id = new Option<string>("--id", "Scholar ID") { IsRequired = true }; var cmd = new Command("scholar-figure", "[¥0.50] Research portrait") { id };
        cmd.SetHandler(async (string iv, bool j) => {
            var r = await new AminerClient().GetScholarPortraitAsync(iv);
            W(jo, "scholar-figure", "1 portrait", r, r.Items.Select(s => (object)new {
                s.Id, domains = (object?)s.AiDomains, interests = (object?)s.AiInterests, education = (object?)s.Educations, work = (object?)s.Works
            }));
        }, id, jo); return cmd; }

    // ── PAID: scholar stat ─────────────────────────────
    static Command CreateScholarStat(Option<bool> jo)
    { var id = new Option<string>("--id", "Scholar ID") { IsRequired = true }; var cmd = new Command("scholar-stat", "[¥0.50] Scholar statistics") { id };
        cmd.SetHandler(async (string iv, bool j) => {
            var r = await new AminerClient().GetScholarStatsAsync(iv);
            W(jo, "scholar-stat", "1 stat", r, r.Items.Select(s => (object)new {
                s.Id, s.PubNum, s.CitationCount, s.HIndex, s.GIndex, s.Activity, s.Diversity, s.Sociability
            }));
        }, id, jo); return cmd; }

    // ── PAID: scholar papers ───────────────────────────
    static Command CreateScholarPapers(Option<bool> jo)
    { var id = new Option<string>("--id", "Scholar ID") { IsRequired = true }; var cmd = new Command("scholar-papers", "[¥1.50] Scholar's papers") { id };
        cmd.SetHandler(async (string iv, bool j) => { var r = await new AminerClient().GetScholarPapersAsync(iv); W(jo, "scholar-papers", $"{r.Total} papers", r, r.Items.Select(P)); }, id, jo); return cmd; }

    // ── PAID: scholar patents ──────────────────────────
    static Command CreateScholarPatents(Option<bool> jo)
    { var id = new Option<string>("--id", "Scholar ID") { IsRequired = true }; var cmd = new Command("scholar-patents", "[¥1.50] Scholar's patents") { id };
        cmd.SetHandler(async (string iv, bool j) => { var r = await new AminerClient().GetScholarPatentsAsync(iv); W(jo, "scholar-patents", $"{r.Total} patents", r, r.Items.Select(Pat)); }, id, jo); return cmd; }

    // ── PAID: scholar projects ─────────────────────────
    static Command CreateScholarProjects(Option<bool> jo)
    { var id = new Option<string>("--id", "Scholar ID") { IsRequired = true }; var cmd = new Command("scholar-projects", "[¥1.50] Research projects") { id };
        cmd.SetHandler(async (string iv, bool j) => {
            var r = await new AminerClient().GetScholarProjectsAsync(iv);
            W(jo, "scholar-projects", $"{r.Total} projects", r, r.Items.Select(p => (object)new {
                p.Id, p.Country, p.FundAmount, p.FundCurrency, p.ProjectSource, p.Titles, p.StartDate, p.EndDate
            }));
        }, id, jo); return cmd; }

    // ── PAID: org detail ───────────────────────────────
    static Command CreateOrgDetail(Option<bool> jo)
    { var ids = new Option<string[]>("--ids", "Org IDs") { AllowMultipleArgumentsPerToken = true, IsRequired = true }; var cmd = new Command("org-detail", "[¥0.01] Full org details") { ids };
        cmd.SetHandler(async (string[] iv, bool j) => {
            var r = await new AminerClient().GetOrgDetailAsync(iv);
            W(jo, "org-detail", $"{r.Total} orgs", r, r.Items.Select(o => (object)new {
                o.Id, o.Name, o.NameEn, o.NameZh, o.Type, o.Aliases, o.Acronyms
            }));
        }, ids, jo); return cmd; }

    // ── PAID: org patents ──────────────────────────────
    static Command CreateOrgPatents(Option<bool> jo)
    { var id = new Option<string>("--id", "Org ID") { IsRequired = true }; var pg = new Option<int>("--page", () => 1); var psz = new Option<int>("--page-size", () => 100); var src = new Option<string?>("--source", () => "ass");
        var cmd = new Command("org-patents", "[¥0.10] Org patent portfolio") { id, pg, psz, src };
        cmd.SetHandler(async (string iv, int p, int ps, string? sr, bool j) => {
            var r = await new AminerClient().GetOrgPatentsAsync(iv, p, ps, sr); W(jo, "org-patents", $"{r.Total} patents", r, r.Items.Select(Pat));
        }, id, pg, psz, src, jo); return cmd; }

    // ── PAID: patent detail ────────────────────────────
    static Command CreatePatentDetail(Option<bool> jo)
    { var id = new Option<string>("--id", "Patent ID") { IsRequired = true }; var cmd = new Command("patent-detail", "[¥0.01] Full patent details") { id };
        cmd.SetHandler(async (string iv, bool j) => {
            var r = await new AminerClient().GetPatentDetailAsync(iv);
            W(jo, "patent-detail", "1 patent", r, r.Items.Select(p => (object)new {
                p.Id, p.Title, p.Abstract, p.AppNum, p.AppDate, p.PubNum, p.PubDate, p.PubKind,
                p.Country, p.Inventors, p.Assignees, p.Ipc, description = p.Description
            }));
        }, id, jo); return cmd; }

    // ── helpers ────────────────────────────────────────
    static void W<T>(Option<bool> jo, string cmd, string sum, AminerResult<T> r, IEnumerable<object> items)
    { var o = JsonOutput.Ok(cmd, sum, new { r.Success, r.Total, r.ErrorCode, r.ErrorMessage, items }); o.Metrics["items"] = r.Items.Count; Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts)); }

    static object P(AminerPaper p) => new {
        p.Id, p.Title, p.TitleZh, displayTitle = p.DisplayTitle, p.Doi, p.Year,
        p.CitationBucket, p.CitationCount, p.FirstAuthor, p.VenueName,
        p.AbstractSlice, p.Authors, p.AuthorOrgs, p.Keywords, p.KeywordsZh,
        p.Issn, p.Issue, p.Volume, p.VenueRaw, p.VenueId,
        p.Summary, p.PdfLink, p.PaperUrl, pubUrl = p.PubUrl
    };

    static object Pat(AminerPatent p) => new {
        p.Id, p.Title, p.TitleZh, p.PubYear, p.AppYear, p.InventorName,
        p.Country, p.AppNum, p.PubNum, p.PubKind, p.AppDate, p.PubDate,
        p.Inventors, p.Assignees, p.Ipc, patentUrl = p.PatentUrl
    };
}
