using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OMML = DocumentFormat.OpenXml.Math;

namespace DocxCore;

// ============================================================================
// WordSlice — NongMark v1 "One Cut, Three Streams" slicing engine
// ============================================================================

/// <summary>
/// NongMark v1 "One Cut, Three Streams" slicing engine.
/// Opens a .docx, extracts all content as nongmark/v1 blocks with stable IDs,
/// and writes seven output files to the output directory.
/// </summary>
public static class WordSlice
{
    // JSON serialization: camelCase naming policy
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions JsonlOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // Chem equation regex
    private static readonly Regex ChemEquationRegex = new(
        @"([A-Z][a-z]?\d*(?:\([^)]*\))?\d*(?:·\d*[A-Z][a-z]?\d*[Oo])?(?:\^[+\-\d]+)?(?:\s*\([sglqacd]{1,3}\))?)" +
        @"\s*\+\s*" +
        @"([A-Z][a-z]?\d*(?:\([^)]*\))?\d*(?:·\d*[A-Z][a-z]?\d*[Oo])?(?:\^[+\-\d]+)?(?:\s*\([sglqacd]{1,3}\))?)" +
        @"\s*(->|→|←|⇌|→|<-|<=>|⇀|⇁|=|⇄|⇆)\s*" +
        @"([A-Z][a-z]?\d*(?:\([^)]*\))?\d*(?:·\d*[A-Z][a-z]?\d*[Oo])?(?:\^[+\-\d]+)?(?:\s*\([sglqacd]{1,3}\))?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HasChemArrow = new(
        @"\s*(->|→|←|⇌|→|<-|<=>|⇀|⇁|⇄|⇆)\s*",
        RegexOptions.Compiled);

    // ========================================================================
    // Main entry point
    // ========================================================================

    /// <summary>
    /// Slice a .docx file into nongmark/v1 streams.
    /// </summary>
    public static WordSliceResult Slice(string docxPath, string outputDir,
        IImageAnalyzer? imageAnalyzer = null)
    {
        Directory.CreateDirectory(outputDir);
        var assetsDir = Path.Combine(outputDir, "assets");
        Directory.CreateDirectory(assetsDir);

        var warnings = new List<string>();
        var blocks = new List<NongBlock>();

        // Stable ID allocator (Agent A's WordBlockIdMap)
        var idMap = new WordBlockIdMap();

        var imageAssets = new List<NongAssetEntry>();
        var imageParts = new List<(string rId, ImagePart part)>();

        using var doc = WordprocessingDocument.Open(docxPath, false);
        var mainPart = doc.MainDocumentPart;
        var body = mainPart?.Document?.Body;
        var sourcePath = Path.GetFullPath(docxPath);

        if (body == null)
        {
            warnings.Add("Document body is empty or missing.");
            return WriteOutputAndReturn(outputDir, blocks, warnings, sourcePath);
        }

        // Build image part lookup
        if (mainPart != null)
        {
            foreach (var ip in mainPart.ImageParts)
            {
                var rId = mainPart.GetIdOfPart(ip);
                imageParts.Add((rId, ip));
            }
        }

        // Build hyperlink relationship lookup (rId -> Uri)
        var hyperlinkUris = new Dictionary<string, Uri>();
        if (mainPart != null)
        {
            foreach (var hr in mainPart.HyperlinkRelationships)
            {
                if (!string.IsNullOrEmpty(hr.Id))
                    hyperlinkUris[hr.Id] = hr.Uri;
            }
        }

        // ====================================================================
        // Phase 1: Extract body-level blocks
        // ====================================================================
        int position = 0;
        foreach (var element in body.Elements())
        {
            if (element is Paragraph para)
            {
                var paraBlocks = ProcessParagraph(para, mainPart, imageParts,
                    hyperlinkUris, idMap, position, warnings);
                foreach (var b in paraBlocks) blocks.Add(b);
                position += paraBlocks.Count;
            }
            else if (element is Table table)
            {
                var tbl = ProcessTable(table, idMap, position, warnings);
                blocks.Add(tbl);
                position++;
            }
            else if (element is SdtBlock sdt)
            {
                var inner = sdt.SdtContentBlock;
                if (inner is SdtContentBlock contentBlock)
                {
                    foreach (var child in contentBlock.Elements())
                    {
                        if (child is Paragraph childPara)
                        {
                            var paraBlocks = ProcessParagraph(childPara, mainPart, imageParts,
                                hyperlinkUris, idMap, position, warnings);
                            foreach (var b in paraBlocks) blocks.Add(b);
                            position += paraBlocks.Count;
                        }
                        else if (child is Table childTable)
                        {
                            var tbl = ProcessTable(childTable, idMap, position, warnings);
                            blocks.Add(tbl);
                            position++;
                        }
                    }
                }
            }
            else if (element is SectionProperties)
            {
                // Skip — captured in format extraction
            }
            else
            {
                // Unknown element → RawOpenXmlRefBlock (metadata only, no raw XML)
                blocks.Add(new RawOpenXmlRefBlock
                {
                    Id = idMap.AllocateId("raw"),
                    Kind = "rawOpenXmlRef",
                    Element = element.LocalName,
                    Reason = "Unrecognized body-level element",
                });
                position++;
            }
        }

        // ====================================================================
        // Phase 2: Extract footnotes
        // ====================================================================
        var fnPart = mainPart?.FootnotesPart;
        if (fnPart?.Footnotes != null)
        {
            int fnNum = 0;
            foreach (var fn in fnPart.Footnotes.Elements<Footnote>())
            {
                fnNum++;
                if (fn.Id?.Value == 0 || fn.Id?.Value == -1) continue;
                var runs = ExtractRuns(fn, idMap);
                blocks.Add(new FootnoteBlock
                {
                    Id = idMap.AllocateId("f"),
                    Kind = "footnote",
                    Text = fn.InnerText,
                    Number = fnNum,
                    Runs = runs,
                });
                position++;
            }
        }

        // ====================================================================
        // Phase 3: Extract endnotes
        // ====================================================================
        var enPart = mainPart?.EndnotesPart;
        if (enPart?.Endnotes != null)
        {
            int enNum = 0;
            foreach (var en in enPart.Endnotes.Elements<Endnote>())
            {
                enNum++;
                if (en.Id?.Value == 0 || en.Id?.Value == -1) continue;
                var runs = ExtractRuns(en, idMap);
                blocks.Add(new EndnoteBlock
                {
                    Id = idMap.AllocateId("e"),
                    Kind = "endnote",
                    Text = en.InnerText,
                    Number = enNum,
                    Runs = runs,
                });
                position++;
            }
        }

        // ====================================================================
        // Phase 4: Extract revisions (track changes) — actually extract snippets
        // ====================================================================
        if (body != null)
        {
            // Inserted runs: w:ins containing <w:r> children
            foreach (var ins in body.Descendants<Inserted>())
            {
                var text = ins.InnerText.Trim();
                if (text.Length == 0) continue;

                var author = ins.Author?.Value;
                var date = ins.Date?.Value;
                var dateStr = date.HasValue ? date.Value.ToString("o") : null;

                blocks.Add(new RevisionBlock
                {
                    Id = idMap.AllocateId("rev"),
                    Kind = "revision",
                    Type = "insertion",
                    Author = author,
                    Date = dateStr,
                    Text = text,
                });
                position++;
            }

            // Deleted runs: w:del containing <w:r> children
            foreach (var del in body.Descendants<Deleted>())
            {
                var text = del.InnerText.Trim();
                if (text.Length == 0) continue;

                var author = del.Author?.Value;
                var date = del.Date?.Value;
                var dateStr = date.HasValue ? date.Value.ToString("o") : null;

                blocks.Add(new RevisionBlock
                {
                    Id = idMap.AllocateId("rev"),
                    Kind = "revision",
                    Type = "deletion",
                    Author = author,
                    Date = dateStr,
                    Text = text,
                });
                position++;
            }

            // Move runs: check RunProperties for MoveFrom / MoveTo markers
            foreach (var run in body.Descendants<Run>())
            {
                var rp = run.RunProperties;
                if (rp == null) continue;

                var moveFrom = rp.GetFirstChild<MoveFrom>();
                var moveTo = rp.GetFirstChild<MoveTo>();

                string? revType = null;
                string? revAuthor = null;
                DateTime? revDate = null;

                if (moveFrom != null)
                {
                    revType = "moveFrom";
                    revAuthor = moveFrom.Author?.Value;
                    revDate = moveFrom.Date?.Value;
                }
                else if (moveTo != null)
                {
                    revType = "moveTo";
                    revAuthor = moveTo.Author?.Value;
                    revDate = moveTo.Date?.Value;
                }

                if (revType == null) continue;

                var text = run.InnerText.Trim();
                if (text.Length == 0) continue;

                var dateStr = revDate.HasValue ? revDate.Value.ToString("o") : null;

                blocks.Add(new RevisionBlock
                {
                    Id = idMap.AllocateId("rev"),
                    Kind = "revision",
                    Type = revType,
                    Author = revAuthor,
                    Date = dateStr,
                    Text = text,
                });
                position++;
            }
        }

        // ====================================================================
        // Phase 5: Extract comments with anchor info (CommentRangeStart/End)
        // ====================================================================
        var commentsPart = mainPart?.WordprocessingCommentsPart;
        if (commentsPart?.Comments != null)
        {
            var anchorTexts = CollectCommentAnchors(body);
            var anchorBlockIds = CollectCommentAnchorBlockIds(body, blocks);

            foreach (var comment in commentsPart.Comments.Elements<Comment>())
            {
                var commentId = comment.Id?.Value ?? "";

                anchorTexts.TryGetValue(commentId, out var anchorText);
                anchorBlockIds.TryGetValue(commentId, out var anchorBlockId);

                blocks.Add(new CommentBlock
                {
                    Id = idMap.AllocateId("c"),
                    Kind = "comment",
                    Author = comment.Author?.Value,
                    Date = comment.Date?.Value.ToString("yyyy-MM-ddTHH:mm:ss"),
                    Text = comment.InnerText,
                    Initials = comment.Initials?.Value,
                    AnchorBlockId = anchorBlockId,
                    AnchorText = anchorText,
                });
                position++;
            }
        }

        // ====================================================================
        // Phase 6: Extract images to assets/ directory
        // ====================================================================
        if (mainPart != null)
        {
            int imgIdx = 0;

            // Track which block IDs use each image relationship ID
            var imageUsedBy = new Dictionary<string, List<string>>();
            foreach (var block in blocks)
            {
                if (block is ImageBlock img && img.ImageId != null)
                {
                    if (!imageUsedBy.ContainsKey(img.ImageId))
                        imageUsedBy[img.ImageId] = new List<string>();
                    imageUsedBy[img.ImageId].Add(img.Id);
                }
            }

            foreach (var (rId, ip) in imageParts)
            {
                imgIdx++;
                string ext = ContentTypeToExtension(ip.ContentType);
                string fileName = $"img_{imgIdx:D4}{ext}";
                string filePath = Path.Combine(assetsDir, fileName);

                using var stream = ip.GetStream();
                using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                stream.CopyTo(fs);

                long size = new FileInfo(filePath).Length;

                // Get actual image dimensions via ImageHeaderReader
                int? actualWidth = null;
                int? actualHeight = null;
                try
                {
                    var dims = ImageHeaderReader.GetDimensions(filePath);
                    actualWidth = dims.Width;
                    actualHeight = dims.Height;
                }
                catch { /* unsupported format — leave null */ }

                imageUsedBy.TryGetValue(rId, out var usedByList);
                var usedBy = usedByList ?? new List<string>();

                // Run image analysis if analyzer is available
                ImageAnalysis? assetAnalysis = null;
                if (imageAnalyzer != null)
                {
                    try
                    {
                        assetAnalysis = imageAnalyzer.Analyze(filePath);
                    }
                    catch { /* analysis failed */ }
                }

                var ocrInfo = new OcrInfo { Status = "notRun", Engine = "not available" };

                var assetEntry = new NongAssetEntry
                {
                    Id = rId,
                    File = $"assets/{fileName}",
                    ContentType = ip.ContentType,
                    Size = size,
                    Width = actualWidth,
                    Height = actualHeight,
                    UsedBy = usedBy,
                    InternalRelationshipId = rId,
                    Analysis = assetAnalysis,
                    Ocr = ocrInfo,
                };
                imageAssets.Add(assetEntry);

                // Update image blocks with real asset path, dimensions, analysis
                foreach (var block in blocks)
                {
                    if (block is ImageBlock img && img.ImageId == rId)
                    {
                        img.AssetPath = $"assets/{fileName}";
                        img.ContentType = ip.ContentType;

                        if (assetAnalysis != null)
                        {
                            img.Analysis = assetAnalysis;
                        }
                        else
                        {
                            img.Analysis ??= new ImageAnalysis { Engine = "not available" };
                        }
                    }
                }
            }
        }

        // Ensure all image blocks have analysis set
        foreach (var block in blocks)
        {
            if (block is ImageBlock img && img.Analysis == null)
            {
                img.Analysis = new ImageAnalysis { Engine = "not available" };
            }
        }

        // ====================================================================
        // Phase 7: Build structure, format, manifest and write output
        // ====================================================================
        return WriteOutputAndReturn(outputDir, blocks, warnings, sourcePath, doc, imageAssets);
    }

    // ========================================================================
    // Paragraph processing
    // ========================================================================

    private static List<NongBlock> ProcessParagraph(Paragraph para,
        MainDocumentPart? mainPart,
        List<(string rId, ImagePart part)> imageParts,
        Dictionary<string, Uri> hyperlinkUris,
        WordBlockIdMap idMap,
        int startPos, List<string> warnings)
    {
        var result = new List<NongBlock>();
        var styleId = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        var styleName = GetStyleName(mainPart, styleId);
        var outlineLvl = para.ParagraphProperties?.OutlineLevel?.Val?.Value;
        var paragraphFormat = ExtractParagraphFormat(para.ParagraphProperties);
        int pos = startPos;

        // Check for TOC field
        if (HasTocField(para))
        {
            result.Add(new TocBlock
            {
                Id = idMap.AllocateId("toc"),
                Kind = "toc",
                Instruction = "TOC",
                Switches = ExtractTocSwitches(para),
            });
            return result;
        }

        // Check for complex field codes
        if (HasComplexField(para))
        {
            result.Add(new FieldBlock
            {
                Id = idMap.AllocateId("fld"),
                Kind = "field",
                FieldCode = ExtractFieldCode(para),
                FieldResult = para.InnerText,
            });
            return result;
        }

        // Check if this is a pure heading
        bool isHeading = IsHeadingStyle(styleId) || (outlineLvl.HasValue && outlineLvl.Value >= 0);
        int headingLevel = DetermineHeadingLevel(styleId, outlineLvl);

        if (isHeading && headingLevel >= 1 && headingLevel <= 9)
        {
            var runs = ExtractRuns(para, idMap);
            result.Add(new HeadingBlock
            {
                Id = idMap.AllocateId("h"),
                Kind = "heading",
                Text = para.InnerText,
                Runs = runs,
                Level = headingLevel,
                StyleId = styleId,
                StyleName = styleName,
                Format = paragraphFormat,
            });
            return result;
        }

        // Check for OMML math
        var mathElements = para.Elements<OMML.OfficeMath>().ToList();
        var mathParaElements = para.Elements<OMML.Paragraph>().ToList();

        bool isPureMath = mathElements.Count > 0 || mathParaElements.Count > 0;
        bool onlyMath = isPureMath && string.IsNullOrWhiteSpace(
            string.Join("", para.Elements<Run>().Select(r => r.InnerText)));

        if (onlyMath)
        {
            foreach (var om in mathElements)
            {
                string? latex = OmmToLatex(om);
                result.Add(new EquationBlock
                {
                    Id = idMap.AllocateId("m"),
                    Kind = "equation",
                    Latex = latex,
                    Display = false,
                    OmmlPresent = true,
                    Source = latex != null ? "omml" : "omml",
                    TextFallback = om.InnerText,
                });
                pos++;
            }
            foreach (var omp in mathParaElements)
            {
                string? latex = OmmParaToLatex(omp);
                result.Add(new EquationBlock
                {
                    Id = idMap.AllocateId("m"),
                    Kind = "equation",
                    Latex = latex,
                    Display = true,
                    OmmlPresent = true,
                    Source = latex != null ? "omml" : "omml",
                    TextFallback = omp.InnerText,
                });
                pos++;
            }
            return result;
        }

        // Mixed paragraph: extract elements in order
        return ProcessMixedParagraph(para, mainPart, imageParts,
            hyperlinkUris, idMap, styleId, styleName, paragraphFormat, ref pos, warnings);
    }

    private static bool IsOfficeMathElement(OpenXmlElement element) =>
        element.LocalName == "oMath";

    private static bool IsOfficeMathParagraphElement(OpenXmlElement element) =>
        element.LocalName == "oMathPara";

    private static List<NongBlock> ProcessMixedParagraph(Paragraph para,
        MainDocumentPart? mainPart,
        List<(string rId, ImagePart part)> imageParts,
        Dictionary<string, Uri> hyperlinkUris,
        WordBlockIdMap idMap,
        string? styleId, string? styleName,
        NongParagraphFormat? paragraphFormat,
        ref int pos, List<string> warnings)
    {
        var result = new List<NongBlock>();
        var textRuns = new List<RunBlock>();
        var sb = new StringBuilder();
        bool hasText = false;

        foreach (var child in para.Elements())
        {
            if (child is Run run)
            {
                var drawings = run.Elements<Drawing>().ToList();
                if (drawings.Count > 0)
                {
                    // Flush pending text first
                    if (hasText)
                    {
                        result.Add(new ParagraphBlock
                        {
                            Id = idMap.AllocateId("p"),
                            Kind = "paragraph",
                            Text = sb.ToString().Trim(),
                            Runs = textRuns,
                            StyleId = styleId,
                            StyleName = styleName,
                            Format = paragraphFormat,
                        });
                        pos++;
                        textRuns = new List<RunBlock>();
                        sb.Clear();
                        hasText = false;
                    }

                    foreach (var drawing in drawings)
                    {
                        var imgBlock = ExtractImageFromDrawing(drawing, imageParts, idMap, warnings);
                        if (imgBlock != null)
                        {
                            result.Add(imgBlock);
                            pos++;
                        }
                    }
                }
                else
                {
                    var mathParaInRun = run.Descendants()
                        .Where(IsOfficeMathParagraphElement)
                        .ToList();
                    var mathInRun = run.Descendants()
                        .Where(e => IsOfficeMathElement(e)
                            && !e.Ancestors().Any(IsOfficeMathParagraphElement))
                        .ToList();
                    if (mathInRun.Count > 0 || mathParaInRun.Count > 0)
                    {
                        if (hasText)
                        {
                            result.Add(new ParagraphBlock
                            {
                                Id = idMap.AllocateId("p"),
                                Kind = "paragraph",
                                Text = sb.ToString().Trim(),
                                Runs = textRuns,
                                StyleId = styleId,
                                StyleName = styleName,
                                Format = paragraphFormat,
                            });
                            pos++;
                            textRuns = new List<RunBlock>();
                            sb.Clear();
                            hasText = false;
                        }

                        foreach (var om in mathInRun)
                        {
                            string? latex = om is OMML.OfficeMath typedOm ? OmmToLatex(typedOm) : null;
                            result.Add(new EquationBlock
                            {
                                Id = idMap.AllocateId("m"),
                                Kind = "equation",
                                Latex = latex,
                                Display = false,
                                OmmlPresent = true,
                                Source = latex != null ? "omml" : "omml",
                                TextFallback = om.InnerText,
                            });
                            pos++;
                        }

                        foreach (var omp in mathParaInRun)
                        {
                            string? latex = omp is OMML.Paragraph typedOmp ? OmmParaToLatex(typedOmp) : null;
                            result.Add(new EquationBlock
                            {
                                Id = idMap.AllocateId("m"),
                                Kind = "equation",
                                Latex = latex,
                                Display = true,
                                OmmlPresent = true,
                                Source = latex != null ? "omml" : "omml",
                                TextFallback = omp.InnerText,
                            });
                            pos++;
                        }
                    }
                    else
                    {
                        var rBlock = ExtractSingleRun(run, idMap);
                        textRuns.Add(rBlock);
                        sb.Append(rBlock.Text ?? "");
                        hasText = true;
                    }
                }
            }
            else if (child is OMML.OfficeMath om)
            {
                if (hasText)
                {
                    result.Add(new ParagraphBlock
                    {
                        Id = idMap.AllocateId("p"),
                        Kind = "paragraph",
                        Text = sb.ToString().Trim(),
                        Runs = textRuns,
                        StyleId = styleId,
                        StyleName = styleName,
                        Format = paragraphFormat,
                    });
                    pos++;
                    textRuns = new List<RunBlock>();
                    sb.Clear();
                    hasText = false;
                }

                string? latex = OmmToLatex(om);
                result.Add(new EquationBlock
                {
                    Id = idMap.AllocateId("m"),
                    Kind = "equation",
                    Latex = latex,
                    Display = false,
                    OmmlPresent = true,
                    Source = latex != null ? "omml" : "omml",
                    TextFallback = om.InnerText,
                });
                pos++;
            }
            else if (child is Hyperlink hl)
            {
                // Resolve real URL from HyperlinkRelationship
                string? actualUrl = null;
                string? internalAnchor = null;
                bool isInternal = false;

                var rId = hl.Id?.Value;
                if (!string.IsNullOrEmpty(rId) && hyperlinkUris.TryGetValue(rId, out var uri))
                {
                    actualUrl = uri.AbsoluteUri;
                    isInternal = false;
                }
                else
                {
                    internalAnchor = hl.Anchor?.Value;
                    if (!string.IsNullOrEmpty(internalAnchor))
                    {
                        actualUrl = $"#{internalAnchor}";
                        isInternal = true;
                    }
                    else
                    {
                        actualUrl = rId;
                        isInternal = !string.IsNullOrEmpty(rId) &&
                            !rId.StartsWith("http", StringComparison.OrdinalIgnoreCase);
                    }
                }

                var hlRuns = ExtractRuns(hl, idMap);
                result.Add(new HyperlinkBlock
                {
                    Id = idMap.AllocateId(isInternal ? "xref" : "link"),
                    Kind = "hyperlink",
                    Text = hl.InnerText,
                    Url = actualUrl,
                    InternalAnchor = internalAnchor,
                    IsInternal = isInternal,
                    Runs = hlRuns,
                });
                pos++;
            }
            else if (child is BookmarkStart bmStart)
            {
                result.Add(new BookmarkBlock
                {
                    Id = idMap.AllocateId("bm"),
                    Kind = "bookmark",
                    Name = bmStart.Name?.Value,
                    BookmarkId = bmStart.Id?.Value,
                    IsStart = true,
                });
                pos++;
            }
            else if (child is OMML.Paragraph omp)
            {
                if (hasText)
                {
                    result.Add(new ParagraphBlock
                    {
                        Id = idMap.AllocateId("p"),
                        Kind = "paragraph",
                        Text = sb.ToString().Trim(),
                        Runs = textRuns,
                        StyleId = styleId,
                        StyleName = styleName,
                        Format = paragraphFormat,
                    });
                    pos++;
                    textRuns = new List<RunBlock>();
                    sb.Clear();
                    hasText = false;
                }

                string? latex = OmmParaToLatex(omp);
                result.Add(new EquationBlock
                {
                    Id = idMap.AllocateId("m"),
                    Kind = "equation",
                    Latex = latex,
                    Display = true,
                    OmmlPresent = true,
                    Source = latex != null ? "omml" : "omml",
                });
                pos++;
            }
            else if (child is CommentRangeStart or CommentRangeEnd or CommentReference)
            {
                // Comment markers are structural, not content blocks — skip
            }
            else if (child is BookmarkEnd)
            {
                // Bookmark end markers — skip
            }
        }

        // Flush remaining text
        if (hasText)
        {
            string paragraphText = sb.ToString().Trim();
            var chemResult = TryExtractChemEquation(paragraphText);
            if (chemResult != null)
            {
                chemResult.Id = idMap.AllocateId("ce");
                result.Add(chemResult);
            }
            else
            {
                result.Add(new ParagraphBlock
                {
                    Id = idMap.AllocateId("p"),
                    Kind = "paragraph",
                    Text = paragraphText.Length == 0 ? null : paragraphText,
                    Runs = textRuns,
                    StyleId = styleId,
                    StyleName = styleName,
                    Format = paragraphFormat,
                });
            }
            pos++;
        }

        // If no blocks were added, emit an empty paragraph
        if (result.Count == 0)
        {
            result.Add(new ParagraphBlock
            {
                Id = idMap.AllocateId("p"),
                Kind = "paragraph",
                Text = null,
                Runs = new List<RunBlock>(),
                StyleId = styleId,
                StyleName = styleName,
                Format = paragraphFormat,
            });
            pos++;
        }

        return result;
    }

    // ========================================================================
    // Chem equation detection
    // ========================================================================

    private static ChemEquationBlock? TryExtractChemEquation(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (text.Length > 300) return null;

        if (!HasChemArrow.IsMatch(text)) return null;

        var match = ChemEquationRegex.Match(text);
        if (!match.Success) return null;

        var species = new List<string>();
        var parts = Regex.Split(text,
            @"\s*(->|→|←|⇌|→|<-|<=>|⇀|⇁|⇄|⇆|=|\+)\s*");
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            if (Regex.IsMatch(trimmed, @"^(->|→|←|⇌|→|<-|<=>|⇀|⇁|⇄|⇆|=|\+)$")) continue;
            species.Add(trimmed);
        }

        var normalized = Regex.Replace(text, @"\s*\([sglqacd]{1,3}\)\s*", "");
        normalized = Regex.Replace(normalized, @"\s+", " ");
        normalized = normalized.Trim();

        return new ChemEquationBlock
        {
            Kind = "chemEquation",
            Text = text,
            Normalized = normalized,
            Species = species,
            Source = "text",
            Confidence = 0.7,
        };
    }

    // ========================================================================
    // Table processing (tr/tc prefix counters, NOT rCounter)
    // ========================================================================

    private static TableBlock ProcessTable(Table table,
        WordBlockIdMap idMap, int position, List<string> warnings)
    {
        var rows = new List<TableRowBlock>();
        int rowIdx = 0;

        foreach (var row in table.Elements<TableRow>())
        {
            var cells = new List<TableCellBlock>();
            bool isHeader = rowIdx == 0;

            foreach (var cell in row.Elements<TableCell>())
            {
                var cellRuns = ExtractRuns(cell, idMap);
                int gridSpan = 1;
                var tcPr = cell.TableCellProperties;
                if (tcPr?.GridSpan?.Val?.Value != null)
                    gridSpan = tcPr.GridSpan.Val.Value;

                int rowSpan = 1;
                if (tcPr?.VerticalMerge?.Val?.Value == MergedCellValues.Restart)
                    rowSpan = 1;

                cells.Add(new TableCellBlock
                {
                    Id = idMap.AllocateId("tc"),
                    Kind = "tableCell",
                    Text = cell.InnerText,
                    Runs = cellRuns,
                    GridSpan = gridSpan,
                    RowSpan = rowSpan,
                    Format = ExtractTableCellFormat(tcPr),
                });
            }

            rows.Add(new TableRowBlock
            {
                Id = idMap.AllocateId("tr"),
                Kind = "tableRow",
                Cells = cells,
                IsHeader = isHeader,
            });
            rowIdx++;
        }

        int colCount = rows.Count > 0 ? rows[0].Cells.Count : 0;
        var tblStyle = table.TableProperties?.TableStyle?.Val?.Value;
        var tableFormat = ExtractTableFormat(table.TableProperties);

        return new TableBlock
        {
            Id = idMap.AllocateId("t"),
            Kind = "table",
            Rows = rows,
            RowCount = rows.Count,
            ColCount = colCount,
            StyleId = tblStyle,
            Format = tableFormat,
        };
    }

    // ========================================================================
    // Layout/format extraction
    // ========================================================================

    private static NongParagraphFormat? ExtractParagraphFormat(ParagraphProperties? ppr)
    {
        if (ppr == null) return null;

        var spacing = ppr.SpacingBetweenLines;
        var indentation = ppr.Indentation;
        var format = new NongParagraphFormat
        {
            Alignment = ppr.Justification?.Val?.Value.ToString(),
            FirstLineIndent = indentation?.FirstLine?.Value,
            LeftIndent = indentation?.Left?.Value,
            RightIndent = indentation?.Right?.Value,
            LineSpacing = spacing?.Line?.Value,
            LineRule = spacing?.LineRule?.Value.ToString(),
            SpaceBefore = spacing?.Before?.Value,
            SpaceAfter = spacing?.After?.Value,
            KeepNext = ppr.KeepNext == null ? null : ppr.KeepNext.Val?.Value ?? true,
        };

        return HasParagraphFormat(format) ? format : null;
    }

    private static bool HasParagraphFormat(NongParagraphFormat format) =>
        format.Alignment != null ||
        format.FirstLineIndent != null ||
        format.LeftIndent != null ||
        format.RightIndent != null ||
        format.LineSpacing != null ||
        format.LineRule != null ||
        format.SpaceBefore != null ||
        format.SpaceAfter != null ||
        format.KeepNext != null;

    private static NongTableFormat? ExtractTableFormat(TableProperties? tpr)
    {
        if (tpr == null) return null;

        var width = tpr.TableWidth;
        var format = new NongTableFormat
        {
            Justification = tpr.TableJustification?.Val?.Value.ToString(),
            Width = width?.Width?.Value,
            WidthType = width?.Type?.Value.ToString(),
            Borders = ExtractTableBorders(tpr.TableBorders),
        };

        return HasTableFormat(format) ? format : null;
    }

    private static bool HasTableFormat(NongTableFormat format) =>
        format.Justification != null ||
        format.Width != null ||
        format.WidthType != null ||
        format.Borders != null;

    private static NongTableCellFormat? ExtractTableCellFormat(TableCellProperties? tcPr)
    {
        if (tcPr == null) return null;

        var width = tcPr.TableCellWidth;
        var format = new NongTableCellFormat
        {
            Width = width?.Width?.Value,
            WidthType = width?.Type?.Value.ToString(),
            VerticalAlignment = tcPr.TableCellVerticalAlignment?.Val?.Value.ToString(),
            ShadingFill = tcPr.Shading?.Fill?.Value,
            Borders = ExtractTableBorders(tcPr.TableCellBorders),
        };

        return HasTableCellFormat(format) ? format : null;
    }

    private static bool HasTableCellFormat(NongTableCellFormat format) =>
        format.Width != null ||
        format.WidthType != null ||
        format.VerticalAlignment != null ||
        format.ShadingFill != null ||
        format.Borders != null;

    private static NongTableBorders? ExtractTableBorders(TableBorders? borders)
    {
        if (borders == null) return null;

        var result = new NongTableBorders
        {
            Top = ExtractBorder(borders.TopBorder),
            Bottom = ExtractBorder(borders.BottomBorder),
            Left = ExtractBorder(borders.LeftBorder),
            Right = ExtractBorder(borders.RightBorder),
            InsideH = ExtractBorder(borders.InsideHorizontalBorder),
            InsideV = ExtractBorder(borders.InsideVerticalBorder),
        };

        return HasBorders(result) ? result : null;
    }

    private static NongTableBorders? ExtractTableBorders(TableCellBorders? borders)
    {
        if (borders == null) return null;

        var result = new NongTableBorders
        {
            Top = ExtractBorder(borders.TopBorder),
            Bottom = ExtractBorder(borders.BottomBorder),
            Left = ExtractBorder(borders.LeftBorder),
            Right = ExtractBorder(borders.RightBorder),
            InsideH = ExtractBorder(borders.InsideHorizontalBorder),
            InsideV = ExtractBorder(borders.InsideVerticalBorder),
        };

        return HasBorders(result) ? result : null;
    }

    private static bool HasBorders(NongTableBorders borders) =>
        borders.Top != null ||
        borders.Bottom != null ||
        borders.Left != null ||
        borders.Right != null ||
        borders.InsideH != null ||
        borders.InsideV != null;

    private static NongBorderInfo? ExtractBorder(BorderType? border)
    {
        if (border == null) return null;

        var result = new NongBorderInfo
        {
            Val = border.Val?.Value.ToString(),
            Size = border.Size?.Value,
            Color = border.Color?.Value,
            Space = border.Space?.Value,
        };

        return result.Val != null || result.Size != null || result.Color != null || result.Space != null
            ? result
            : null;
    }

    // ========================================================================
    // Run extraction (preserving all run-level formatting)
    // ========================================================================

    private static List<RunBlock> ExtractRuns(OpenXmlElement parent, WordBlockIdMap idMap)
    {
        var runs = new List<RunBlock>();
        foreach (var run in parent.Descendants<Run>())
        {
            runs.Add(ExtractSingleRun(run, idMap));
        }
        return runs;
    }

    private static RunBlock ExtractSingleRun(Run run, WordBlockIdMap idMap)
    {
        var format = new NongRunFormat();
        var rpr = run.RunProperties;

        if (rpr != null)
        {
            format.StyleId = rpr.RunStyle?.Val?.Value;

            var rf = rpr.RunFonts;
            format.FontEastAsia = rf?.EastAsia?.Value;
            format.FontAscii = rf?.Ascii?.Value ?? rf?.HighAnsi?.Value;

            if (rpr.FontSize?.Val?.Value != null)
            {
                format.FontSizePt = double.Parse(rpr.FontSize.Val.Value) / 2.0;
            }
            else if (rpr.FontSizeComplexScript?.Val?.Value != null)
            {
                format.FontSizePt = double.Parse(rpr.FontSizeComplexScript.Val.Value) / 2.0;
            }

            format.Bold = rpr.Bold?.Val?.Value == true || rpr.BoldComplexScript?.Val?.Value == true;
            if (format.Bold == false) format.Bold = null;

            format.Italic = rpr.Italic?.Val?.Value == true || rpr.ItalicComplexScript?.Val?.Value == true;
            if (format.Italic == false) format.Italic = null;

            format.Underline = rpr.Underline?.Val?.Value.ToString();
            format.Color = rpr.Color?.Val?.Value;

            var vertAlign = rpr.VerticalTextAlignment?.Val?.Value;
            if (vertAlign == VerticalPositionValues.Superscript)
                format.Superscript = true;
            else if (vertAlign == VerticalPositionValues.Subscript)
                format.Subscript = true;
        }

        bool hasFormat = format.StyleId != null || format.FontEastAsia != null ||
            format.FontAscii != null || format.FontSizePt != null ||
            format.Bold != null || format.Italic != null ||
            format.Underline != null || format.Color != null ||
            format.Superscript != null || format.Subscript != null;

        return new RunBlock
        {
            Id = idMap.AllocateId("r"),
            Kind = "run",
            Text = run.InnerText,
            Format = hasFormat ? format : null,
        };
    }

    // ========================================================================
    // Image extraction
    // ========================================================================

    private static ImageBlock? ExtractImageFromDrawing(Drawing drawing,
        List<(string rId, ImagePart part)> imageParts,
        WordBlockIdMap idMap, List<string> warnings)
    {
        var blip = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().FirstOrDefault();
        if (blip?.Embed?.Value == null) return null;

        var rId = blip.Embed.Value;
        var imgPart = imageParts.FirstOrDefault(ip => ip.rId == rId);

        long? widthEmu = null, heightEmu = null;
        var extent = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Extents>().FirstOrDefault();
        if (extent != null)
        {
            widthEmu = extent.Cx?.Value;
            heightEmu = extent.Cy?.Value;
        }

        int widthPx = 0, heightPx = 0;
        if (widthEmu.HasValue) widthPx = (int)(widthEmu.Value / 9525);
        if (heightEmu.HasValue) heightPx = (int)(heightEmu.Value / 9525);

        string? altText = null;
        var docPr = drawing.Descendants<DocumentFormat.OpenXml.Drawing.NonVisualDrawingProperties>().FirstOrDefault();
        if (docPr != null)
        {
            altText = docPr.Description?.Value ?? docPr.Name?.Value;
        }

        return new ImageBlock
        {
            Id = idMap.AllocateId("img"),
            Kind = "image",
            ImageId = rId,
            ContentType = imgPart.part?.ContentType,
            WidthEmu = widthEmu,
            HeightEmu = heightEmu,
            WidthPx = widthPx,
            HeightPx = heightPx,
            AltText = altText,
        };
    }

    // ========================================================================
    // OMML -> LaTeX converter (best-effort; sets latex=null on failure)
    // ========================================================================

    private static string? OmmToLatex(OMML.OfficeMath om)
    {
        try
        {
            var sb = new StringBuilder();
            foreach (var child in om.Elements())
            {
                string? part = OmmElementToLatex(child);
                if (part == null) return null;
                sb.Append(part);
            }
            return sb.ToString().Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string? OmmParaToLatex(OMML.Paragraph omp)
    {
        try
        {
            var parts = new List<string>();
            foreach (var om in omp.Elements<OMML.OfficeMath>())
            {
                var latex = OmmToLatex(om);
                if (latex != null) parts.Add(latex);
            }
            return parts.Count > 0 ? string.Join(" \\\\\n", parts) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? OmmElementToLatex(OpenXmlElement element)
    {
        return element.LocalName switch
        {
            "r" => OmmRunToLatex(element),
            "f" => OmmFracToLatex(element),
            "sSup" => OmmSupToLatex(element),
            "sSub" => OmmSubToLatex(element),
            "sSubSup" => OmmSubSupToLatex(element),
            "rad" => OmmRadToLatex(element),
            "nary" => OmmNaryToLatex(element),
            "acc" => OmmAccToLatex(element),
            "bar" => OmmBarToLatex(element),
            "d" => OmmDelimToLatex(element),
            "func" => OmmFuncToLatex(element),
            "groupChr" => OmmGroupChrToLatex(element),
            "box" => OmmBoxToLatex(element),
            "eqArr" => null,
            "m" => null,
            "limLow" => null,
            "limUpp" => null,
            "sPre" => null,
            _ => null,
        };
    }

    private static string OmmRunToLatex(OpenXmlElement run)
    {
        var text = run.Elements().FirstOrDefault(e => e.LocalName == "t");
        if (text == null) return "";
        var val = text.InnerText ?? "";
        return val switch
        {
            "α" => "\\alpha", "β" => "\\beta", "γ" => "\\gamma", "δ" => "\\delta",
            "ε" => "\\epsilon", "ζ" => "\\zeta", "η" => "\\eta", "θ" => "\\theta",
            "ι" => "\\iota", "κ" => "\\kappa", "λ" => "\\lambda", "μ" => "\\mu",
            "ν" => "\\nu", "ξ" => "\\xi", "π" => "\\pi", "ρ" => "\\rho",
            "σ" => "\\sigma", "τ" => "\\tau", "υ" => "\\upsilon", "φ" => "\\phi",
            "χ" => "\\chi", "ψ" => "\\psi", "ω" => "\\omega",
            "Γ" => "\\Gamma", "Δ" => "\\Delta", "Θ" => "\\Theta", "Λ" => "\\Lambda",
            "Ξ" => "\\Xi", "Π" => "\\Pi", "Σ" => "\\Sigma", "Υ" => "\\Upsilon",
            "Φ" => "\\Phi", "Ψ" => "\\Psi", "Ω" => "\\Omega",
            "∑" => "\\sum", "∏" => "\\prod", "∫" => "\\int", "∬" => "\\iint",
            "∭" => "\\iiint", "∮" => "\\oint",
            "∞" => "\\infty", "∂" => "\\partial", "∇" => "\\nabla",
            "√" => "\\sqrt", "×" => "\\times", "÷" => "\\div",
            "±" => "\\pm", "∓" => "\\mp",
            "≤" => "\\le", "≥" => "\\ge", "≠" => "\\ne", "≈" => "\\approx",
            "≡" => "\\equiv", "∝" => "\\propto",
            "→" => "\\rightarrow", "←" => "\\leftarrow", "↔" => "\\leftrightarrow",
            "⇒" => "\\Rightarrow", "⇐" => "\\Leftarrow", "⇔" => "\\Leftrightarrow",
            "⋅" => "\\cdot", "⋆" => "\\star",
            _ => val,
        };
    }

    // ----- Fraction, superscript, subscript, radical, nary, accent, etc. -----

    private static string? OmmFracToLatex(OpenXmlElement frac)
    {
        var num = frac.Elements().FirstOrDefault(e => e.LocalName == "num");
        var den = frac.Elements().FirstOrDefault(e => e.LocalName == "den");
        if (num == null || den == null) return null;
        string? numLatex = OmmChildrenToLatex(num);
        string? denLatex = OmmChildrenToLatex(den);
        if (numLatex == null || denLatex == null) return null;
        return $"\\frac{{{numLatex}}}{{{denLatex}}}";
    }

    private static string? OmmSupToLatex(OpenXmlElement sSup)
    {
        var baseEl = sSup.Elements().FirstOrDefault(e => e.LocalName == "e");
        var supEl = sSup.Elements().FirstOrDefault(e => e.LocalName == "sup");
        if (baseEl == null || supEl == null) return null;
        string? baseLatex = OmmChildrenToLatex(baseEl);
        string? supLatex = OmmChildrenToLatex(supEl);
        if (baseLatex == null || supLatex == null) return null;
        return $"{baseLatex}^{{{supLatex}}}";
    }

    private static string? OmmSubToLatex(OpenXmlElement sSub)
    {
        var baseEl = sSub.Elements().FirstOrDefault(e => e.LocalName == "e");
        var subEl = sSub.Elements().FirstOrDefault(e => e.LocalName == "sub");
        if (baseEl == null || subEl == null) return null;
        string? baseLatex = OmmChildrenToLatex(baseEl);
        string? subLatex = OmmChildrenToLatex(subEl);
        if (baseLatex == null || subLatex == null) return null;
        return $"{baseLatex}_{{{subLatex}}}";
    }

    private static string? OmmSubSupToLatex(OpenXmlElement sSubSup)
    {
        var baseEl = sSubSup.Elements().FirstOrDefault(e => e.LocalName == "e");
        var subEl = sSubSup.Elements().FirstOrDefault(e => e.LocalName == "sub");
        var supEl = sSubSup.Elements().FirstOrDefault(e => e.LocalName == "sup");
        if (baseEl == null) return null;
        string? baseLatex = OmmChildrenToLatex(baseEl);
        if (baseLatex == null) return null;
        string? subLatex = subEl != null ? OmmChildrenToLatex(subEl) : null;
        string? supLatex = supEl != null ? OmmChildrenToLatex(supEl) : null;
        var sb = new StringBuilder(baseLatex);
        if (subLatex != null) sb.Append($"_{{{subLatex}}}");
        if (supLatex != null) sb.Append($"^{{{supLatex}}}");
        return sb.ToString();
    }

    private static string? OmmRadToLatex(OpenXmlElement rad)
    {
        var degEl = rad.Elements().FirstOrDefault(e => e.LocalName == "deg");
        var baseEl = rad.Elements().FirstOrDefault(e => e.LocalName == "e");
        if (baseEl == null) return null;
        string? degLatex = degEl != null ? OmmChildrenToLatex(degEl) : null;
        string? baseLatex = OmmChildrenToLatex(baseEl);
        if (baseLatex == null) return null;
        return degLatex != null
            ? $"\\sqrt[{degLatex}]{{{baseLatex}}}"
            : $"\\sqrt{{{baseLatex}}}";
    }

    private static string? OmmNaryToLatex(OpenXmlElement nary)
    {
        var baseEl = nary.Elements().FirstOrDefault(e => e.LocalName == "e");
        var subEl = nary.Elements().FirstOrDefault(e => e.LocalName == "sub");
        var supEl = nary.Elements().FirstOrDefault(e => e.LocalName == "sup");
        if (baseEl == null) return null;
        string? opLatex = OmmChildrenToLatex(baseEl);
        if (opLatex == null) return null;

        string cmd = opLatex switch
        {
            "∑" => "\\sum", "∏" => "\\prod", "∫" => "\\int",
            "∬" => "\\iint", "∭" => "\\iiint", "∮" => "\\oint",
            "⋃" => "\\bigcup", "⋂" => "\\bigcap",
            "⋁" => "\\bigvee", "⋀" => "\\bigwedge",
            "⨁" => "\\bigoplus", "⨂" => "\\bigotimes", "⊙" => "\\bigodot",
            _ => opLatex,
        };

        string? subLatex = subEl != null ? OmmChildrenToLatex(subEl) : null;
        string? supLatex = supEl != null ? OmmChildrenToLatex(supEl) : null;
        if (subLatex != null || supLatex != null)
        {
            var sb = new StringBuilder(cmd);
            if (subLatex != null) sb.Append($"_{{{subLatex}}}");
            if (supLatex != null) sb.Append($"^{{{supLatex}}}");
            return sb.ToString();
        }
        return cmd;
    }

    private static string? OmmAccToLatex(OpenXmlElement acc)
    {
        var accChrEl = acc.Elements().FirstOrDefault(e => e.LocalName == "accPr");
        var baseEl = acc.Elements().FirstOrDefault(e => e.LocalName == "e");
        if (baseEl == null) return null;
        string? baseLatex = OmmChildrenToLatex(baseEl);
        if (baseLatex == null) return null;
        string? accentChar = null;
        if (accChrEl != null)
        {
            var chrEl = accChrEl.Elements().FirstOrDefault(e => e.LocalName == "chr");
            if (chrEl != null)
            {
                var attr = chrEl.GetAttribute("val",
                    "http://schemas.openxmlformats.org/officeDocument/2006/math");
                accentChar = attr.Value;
            }
        }
        string cmd = accentChar switch
        {
            "̂" or "^" or "\x302" => "\\hat", "̄" or "\x304" => "\\bar",
            "̇" or "\x307" => "\\dot", "̈" or "\x308" => "\\ddot",
            "⃗" => "\\vec", "̃" or "\x303" => "\\tilde",
            _ => "\\hat",
        };
        return $"{cmd}{{{baseLatex}}}";
    }

    private static string? OmmBarToLatex(OpenXmlElement bar)
    {
        var baseEl = bar.Elements().FirstOrDefault(e => e.LocalName == "e");
        if (baseEl == null) return null;
        string? baseLatex = OmmChildrenToLatex(baseEl);
        return baseLatex != null ? $"\\bar{{{baseLatex}}}" : null;
    }

    private static string? OmmDelimToLatex(OpenXmlElement delim)
    {
        var sb = new StringBuilder();
        foreach (var child in delim.Elements())
        {
            if (child.LocalName == "dPr") continue;
            string? part = OmmElementToLatex(child);
            if (part == null) return null;
            sb.Append(part);
        }
        return $"\\left( {sb} \\right)";
    }

    private static string? OmmBoxToLatex(OpenXmlElement box)
    {
        var baseEl = box.Elements().FirstOrDefault(e => e.LocalName == "e");
        return baseEl != null ? OmmChildrenToLatex(baseEl) : null;
    }

    private static string? OmmGroupChrToLatex(OpenXmlElement groupChr)
    {
        var baseEl = groupChr.Elements().FirstOrDefault(e => e.LocalName == "e");
        return baseEl != null ? OmmChildrenToLatex(baseEl) : null;
    }

    private static string? OmmFuncToLatex(OpenXmlElement func)
    {
        var funcNameEl = func.Elements().FirstOrDefault(e => e.LocalName == "funcName");
        var baseEl = func.Elements().FirstOrDefault(e => e.LocalName == "e");
        string? funcName = funcNameEl != null ? OmmChildrenToLatex(funcNameEl) : null;
        string? bodyLatex = baseEl != null ? OmmChildrenToLatex(baseEl) : null;
        var sb = new StringBuilder();
        if (funcName != null) sb.Append($"\\{funcName}");
        if (bodyLatex != null) sb.Append($"{{{bodyLatex}}}");
        return sb.Length > 0 ? sb.ToString() : null;
    }

    private static string? OmmChildrenToLatex(OpenXmlElement parent)
    {
        var sb = new StringBuilder();
        foreach (var child in parent.Elements())
        {
            string? part = OmmElementToLatex(child);
            if (part == null) return null;
            sb.Append(part);
        }
        return sb.ToString();
    }

    // ========================================================================
    // Output writing
    // ========================================================================

    private static WordSliceResult WriteOutputAndReturn(string outputDir,
        List<NongBlock> blocks, List<string> warnings, string sourcePath,
        WordprocessingDocument? doc = null,
        List<NongAssetEntry>? imageAssets = null)
    {
        // Compute source SHA256
        string sha256;
        try
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(sourcePath);
            var hash = sha.ComputeHash(fs);
            sha256 = Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch
        {
            sha256 = "unknown";
        }

        imageAssets ??= new List<NongAssetEntry>();
        var metrics = ComputeMetrics(blocks);

        // 1. Write manifest.json (sourceSha256, createdAt, Content)
        var manifest = new NongManifest
        {
            SchemaVersion = "nongmark/v1",
            Source = Path.GetFileName(sourcePath),
            SourceSha256 = sha256,
            CreatedAt = DateTime.UtcNow,
            Streams = new NongStreamPaths
            {
                Document = "document.json",
                Content = "content.md",
                ContentJsonl = "content.jsonl",
                Structure = "structure.json",
                Format = "format.json",
                Assets = "assets/manifest.json",
            },
            Metrics = metrics,
            Warnings = warnings,
        };
        WriteJson(Path.Combine(outputDir, "manifest.json"), manifest);

        // 2. Write document.json (canonical source alongside content.jsonl)
        var document = new NongDocument
        {
            SchemaVersion = "nongmark/v1",
            Source = Path.GetFileName(sourcePath),
            Blocks = blocks,
        };
        WriteJson(Path.Combine(outputDir, "document.json"), document);

        // 3. Write content.md (human preview)
        var md = GenerateMarkdown(blocks);
        File.WriteAllText(Path.Combine(outputDir, "content.md"), md, Encoding.UTF8);

        // 4. Write content.jsonl (one JSON block per line, each with id/kind)
        using (var sw = new StreamWriter(Path.Combine(outputDir, "content.jsonl"), false, Encoding.UTF8))
        {
            foreach (var block in blocks)
            {
                string json = JsonSerializer.Serialize(block, block.GetType(), JsonlOpts);
                sw.WriteLine(json);
            }
        }

        // 5. Write structure.json
        var structure = BuildStructure(blocks);
        WriteJson(Path.Combine(outputDir, "structure.json"), structure);

        // 6. Write format.json
        NongFormat format;
        if (doc != null)
        {
            format = BuildFormat(doc, blocks, warnings);
        }
        else
        {
            format = new NongFormat
            {
                SchemaVersion = "nongmark/v1",
                Source = Path.GetFileName(sourcePath),
                Warnings = new List<string> { "Format extraction skipped (document not available)." },
            };
        }
        WriteJson(Path.Combine(outputDir, "format.json"), format);

        // 7. Write assets/manifest.json (always written, even with 0 images)
        var assetManifest = new NongAssetManifest
        {
            SchemaVersion = "nongmark/v1",
            Source = Path.GetFileName(sourcePath),
            Items = imageAssets,
        };
        WriteJson(Path.Combine(outputDir, "assets", "manifest.json"), assetManifest);

        return new WordSliceResult(
            OutputDir: Path.GetFullPath(outputDir),
            ManifestPath: Path.GetFullPath(Path.Combine(outputDir, "manifest.json")),
            BlockCount: blocks.Count,
            Warnings: warnings
        );
    }

    // ========================================================================
    // Markdown generation (content.md)
    // ========================================================================

    private static string GenerateMarkdown(List<NongBlock> blocks)
    {
        var sb = new StringBuilder();

        foreach (var block in blocks)
        {
            switch (block)
            {
                case HeadingBlock h:
                    string prefix = new string('#', Math.Min(h.Level, 6));
                    sb.AppendLine($"{prefix} {h.Text ?? ""}");
                    sb.AppendLine();
                    break;

                case ParagraphBlock p:
                    if (!string.IsNullOrEmpty(p.Text))
                        sb.AppendLine(p.Text);
                    sb.AppendLine();
                    break;

                case TableBlock t:
                    sb.AppendLine(GenerateMarkdownTable(t));
                    sb.AppendLine();
                    break;

                case ImageBlock img:
                    var alt = img.AltText ?? "image";
                    var src = img.AssetPath ?? img.ImageId ?? "";
                    sb.AppendLine($"![{alt}]({src})");
                    sb.AppendLine();
                    break;

                case EquationBlock eq:
                    if (eq.Latex != null)
                    {
                        if (eq.Display)
                            sb.AppendLine($"$$\n{eq.Latex}\n$$");
                        else
                            sb.AppendLine($"${eq.Latex}$");
                    }
                    else
                    {
                        sb.AppendLine($"`[Math: {eq.TextFallback ?? "OMML formula"}]`");
                    }
                    sb.AppendLine();
                    break;

                case ChemEquationBlock ce:
                    sb.AppendLine($"`[ChemEq: {ce.Text ?? ""}]`");
                    sb.AppendLine();
                    break;

                case FootnoteBlock fn:
                    sb.AppendLine($"[^{fn.Number}]: {fn.Text ?? ""}");
                    sb.AppendLine();
                    break;

                case EndnoteBlock en:
                    sb.AppendLine($"**[Endnote {en.Number}]** {en.Text ?? ""}");
                    sb.AppendLine();
                    break;

                case HyperlinkBlock hl:
                    sb.AppendLine($"[{hl.Text ?? hl.Url}]({hl.Url})");
                    sb.AppendLine();
                    break;

                case CommentBlock c:
                    sb.AppendLine($"> **Comment [{c.Author}]** {c.Text ?? ""}");
                    sb.AppendLine();
                    break;

                case RevisionBlock rev:
                    sb.AppendLine($"> **[{rev.Type}]** {rev.Author}: {rev.Text ?? ""}");
                    sb.AppendLine();
                    break;

                default:
                    if (block is RunBlock) continue;
                    break;
            }
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    private static string GenerateMarkdownTable(TableBlock table)
    {
        if (table.Rows.Count == 0) return "";
        var sb = new StringBuilder();
        int colCount = table.Rows.Max(r => r.Cells.Count);
        if (colCount == 0) return "";

        var headerRow = table.Rows[0];
        var headers = new string[colCount];
        for (int i = 0; i < colCount; i++)
        {
            headers[i] = i < headerRow.Cells.Count
                ? (headerRow.Cells[i].Text ?? "").Replace("\n", " ").Trim()
                : "";
        }
        sb.AppendLine("| " + string.Join(" | ", headers) + " |");

        var separators = new string[colCount];
        Array.Fill(separators, "---");
        sb.AppendLine("| " + string.Join(" | ", separators) + " |");

        for (int r = 1; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            var cells = new string[colCount];
            for (int i = 0; i < colCount; i++)
            {
                cells[i] = i < row.Cells.Count
                    ? (row.Cells[i].Text ?? "").Replace("\n", " ").Trim()
                    : "";
            }
            sb.AppendLine("| " + string.Join(" | ", cells) + " |");
        }

        return sb.ToString().TrimEnd();
    }

    // ========================================================================
    // Structure builder
    // ========================================================================

    private static NongStructure BuildStructure(List<NongBlock> blocks)
    {
        var structure = new NongStructure
        {
            SchemaVersion = "nongmark/v1",
        };

        var outlineStack = new Stack<NongOutlineItem>();

        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];

            // Extract textPreview and styleId for block index
            string? textPreview = ExtractTextPreview(block);
            string? styleId = (block as ParagraphBlock)?.StyleId
                ?? (block as HeadingBlock)?.StyleId
                ?? (block as TableBlock)?.StyleId;

            // Block index (with order, textPreview, styleId)
            structure.BlockIndex[block.Id] = new NongBlockEntry
            {
                Kind = block.Kind,
                Order = i,
                Position = i,
                TextPreview = textPreview,
                StyleId = styleId,
            };

            // Outline (heading blocks only)
            if (block is HeadingBlock h)
            {
                var item = new NongOutlineItem
                {
                    Id = h.Id,
                    Text = h.Text,
                    Level = h.Level,
                };

                while (outlineStack.Count > 0 && outlineStack.Peek().Level >= h.Level)
                    outlineStack.Pop();

                if (outlineStack.Count > 0)
                    outlineStack.Peek().Children.Add(item);
                else
                    structure.Outline.Add(item);

                outlineStack.Push(item);
            }

            // Tables
            if (block is TableBlock t)
            {
                structure.Tables.Add(new NongTableRef
                {
                    Id = t.Id,
                    Position = i,
                    RowCount = t.RowCount,
                    ColCount = t.ColCount,
                });
            }

            // Footnotes
            if (block is FootnoteBlock fn)
            {
                structure.Footnotes.Add(new NongNoteRef
                {
                    Id = fn.Id,
                    Number = fn.Number,
                    Position = i,
                });
            }

            // Endnotes
            if (block is EndnoteBlock en)
            {
                structure.Endnotes.Add(new NongNoteRef
                {
                    Id = en.Id,
                    Number = en.Number,
                    Position = i,
                });
            }

            // Hyperlinks
            if (block is HyperlinkBlock hl)
            {
                structure.Hyperlinks.Add(new NongHyperlinkRef
                {
                    Id = hl.Id,
                    Url = hl.Url,
                    Position = i,
                });
            }

            // Bookmarks
            if (block is BookmarkBlock bm)
            {
                structure.Bookmarks.Add(new NongBookmarkRef
                {
                    Id = bm.Id,
                    Name = bm.Name,
                    Position = i,
                });
            }

            // Comments
            if (block is CommentBlock cm)
            {
                structure.Comments.Add(new NongCommentRef
                {
                    Id = cm.Id,
                    Author = cm.Author,
                    Position = i,
                });
            }

            // Revisions
            if (block is RevisionBlock rev)
            {
                structure.Revisions.Add(new NongRevisionRef
                {
                    Id = rev.Id,
                    Type = rev.Type,
                    Position = i,
                });
            }

            // Math
            if (block is EquationBlock eq)
            {
                structure.Math.Add(new NongMathRef
                {
                    Id = eq.Id,
                    Display = eq.Display,
                    HasLatex = eq.Latex != null,
                    Position = i,
                });
            }

            // Chem equations and structures
            if (block is ChemEquationBlock ce)
            {
                structure.Chem.Add(new NongChemRef
                {
                    Id = ce.Id,
                    Type = "chemEquation",
                    Position = i,
                });
            }
            if (block is ChemicalStructureBlock cs)
            {
                structure.Chem.Add(new NongChemRef
                {
                    Id = cs.Id,
                    Type = "chemicalStructure",
                    Position = i,
                });
            }
        }

        return structure;
    }

    /// <summary>Extract a short text preview (max 80 chars) for the block index.</summary>
    private static string? ExtractTextPreview(NongBlock block)
    {
        string? text = block switch
        {
            ParagraphBlock p => p.Text,
            HeadingBlock h => h.Text,
            TableBlock t => $"[{t.RowCount}x{t.ColCount} table]",
            ImageBlock img => img.AltText ?? "[image]",
            EquationBlock eq => eq.Latex ?? eq.TextFallback,
            ChemEquationBlock ce => ce.Normalized,
            FootnoteBlock fn => fn.Text,
            EndnoteBlock en => en.Text,
            HyperlinkBlock hl => hl.Text,
            CommentBlock c => c.Text,
            RevisionBlock rev => rev.Text,
            BookmarkBlock bm => bm.Name,
            TocBlock toc => "[TOC]",
            FieldBlock fld => fld.FieldCode,
            RawOpenXmlRefBlock raw => raw.Element,
            TableRowBlock tr => $"[{tr.Cells.Count} cells]",
            TableCellBlock tc => tc.Text,
            RunBlock r => r.Text,
            _ => null,
        };

        if (string.IsNullOrEmpty(text)) return null;
        text = text.Replace("\n", " ").Replace("\r", "").Trim();
        return text.Length <= 80 ? text : text[..80] + "...";
    }

    // ========================================================================
    // Format builder
    // ========================================================================

    private static NongFormat BuildFormat(WordprocessingDocument doc,
        List<NongBlock> blocks, List<string> warnings)
    {
        var format = new NongFormat
        {
            SchemaVersion = "nongmark/v1",
        };

        // Styles
        var sp = doc.MainDocumentPart?.StyleDefinitionsPart;
        if (sp?.Styles != null)
        {
            foreach (var style in sp.Styles.Elements<Style>())
            {
                format.Styles.Add(new NongStyleDef
                {
                    Id = style.StyleId?.Value ?? "",
                    Name = style.StyleName?.Val?.Value,
                    Type = style.Type?.InnerText ?? "",
                    BasedOn = style.BasedOn?.Val?.Value,
                    IsDefault = style.Default?.Value ?? false,
                    IsCustom = style.CustomStyle?.Value ?? false,
                });
            }
        }

        // Fonts
        var fontSet = new HashSet<string>();
        var eastAsiaSet = new HashSet<string>();
        var asciiSet = new HashSet<string>();
        var fontCounts = new Dictionary<string, int>();

        var body = doc.MainDocumentPart?.Document?.Body;
        if (body != null)
        {
            foreach (var run in body.Descendants<Run>())
            {
                var rf = run.RunProperties?.RunFonts;
                if (rf == null) continue;

                AddFont(rf.Ascii?.Value, fontSet, fontCounts);
                AddFont(rf.HighAnsi?.Value, fontSet, fontCounts);
                AddFont(rf.EastAsia?.Value, fontSet, eastAsiaSet, fontCounts);
                AddFont(rf.ComplexScript?.Value, fontSet, fontCounts);
            }
        }

        format.Fonts = new NongFontSummary
        {
            Families = fontSet.OrderBy(f => f).ToList(),
            EastAsia = eastAsiaSet.OrderBy(f => f).ToList(),
            Ascii = asciiSet.OrderBy(f => f).ToList(),
            Counts = fontCounts,
        };

        // Numbering
        var numPart = doc.MainDocumentPart?.NumberingDefinitionsPart;
        if (numPart?.Numbering != null)
        {
            format.Numbering = new NongNumberingInfo
            {
                AbstractNums = numPart.Numbering.Elements<AbstractNum>().Count(),
                Instances = numPart.Numbering.Elements<NumberingInstance>().Count(),
                Types = numPart.Numbering.Elements<AbstractNum>()
                    .Select(a => a.MultiLevelType?.InnerText ?? "hybridMultilevel")
                    .Distinct()
                    .ToList(),
            };
        }

        // Sections
        if (body != null)
        {
            int secIdx = 0;
            foreach (var sectPr in body.Descendants<SectionProperties>())
            {
                var pgSz = sectPr.GetFirstChild<PageSize>();
                var pgMar = sectPr.GetFirstChild<PageMargin>();

                var sec = new NongSectionInfo { Index = secIdx++ };

                if (pgSz != null)
                {
                    if (pgSz.Width?.HasValue == true)
                        sec.PageWidth = (int)(pgSz.Width.Value / 20);
                    if (pgSz.Height?.HasValue == true)
                        sec.PageHeight = (int)(pgSz.Height.Value / 20);
                    sec.Orientation = pgSz.Orient?.InnerText;
                }

                if (pgMar != null)
                {
                    if (pgMar.Top?.HasValue == true) sec.MarginTop = (int)pgMar.Top.Value;
                    if (pgMar.Bottom?.HasValue == true) sec.MarginBottom = (int)pgMar.Bottom.Value;
                    if (pgMar.Left?.HasValue == true) sec.MarginLeft = (int)pgMar.Left.Value;
                    if (pgMar.Right?.HasValue == true) sec.MarginRight = (int)pgMar.Right.Value;
                }

                format.Sections.Add(sec);

                if (secIdx == 1)
                {
                    format.Page = new NongPageInfo
                    {
                        DefaultWidth = sec.PageWidth,
                        DefaultHeight = sec.PageHeight,
                        DefaultOrientation = sec.Orientation,
                    };
                }
            }
        }

        // Table formats
        foreach (var block in blocks)
        {
            if (block is TableBlock t)
            {
                format.Tables.Add(new NongTableFormatInfo
                {
                    BlockId = t.Id,
                    StyleId = t.StyleId,
                    StyleName = t.StyleName,
                    Format = t.Format,
                });
            }
        }

        format.Warnings = warnings;

        return format;
    }

    private static void AddFont(string? name, HashSet<string> set, Dictionary<string, int> counts)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        set.Add(name);
        if (counts.ContainsKey(name))
            counts[name]++;
        else
            counts[name] = 1;
    }

    private static void AddFont(string? name, HashSet<string> set,
        HashSet<string> subSet, Dictionary<string, int> counts)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        set.Add(name);
        subSet.Add(name);
        if (counts.ContainsKey(name))
            counts[name]++;
        else
            counts[name] = 1;
    }

    // ========================================================================
    // Style detection helpers
    // ========================================================================

    private static string? GetStyleName(MainDocumentPart? mainPart, string? styleId)
    {
        if (mainPart == null || styleId == null) return null;
        var sp = mainPart.StyleDefinitionsPart;
        if (sp?.Styles == null) return null;

        var style = sp.Styles.Elements<Style>()
            .FirstOrDefault(s => s.StyleId?.Value == styleId);
        return style?.StyleName?.Val?.Value;
    }

    private static bool IsHeadingStyle(string? styleId) => styleId switch
    {
        "Heading1" or "1" => true,
        "Heading2" or "2" => true,
        "Heading3" or "3" => true,
        "Heading4" or "4" => true,
        "Heading5" or "5" => true,
        "Heading6" or "6" => true,
        "Heading7" or "7" => true,
        "Heading8" or "8" => true,
        "Heading9" or "9" => true,
        _ => !string.IsNullOrEmpty(styleId) && styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase),
    };

    private static int DetermineHeadingLevel(string? styleId, int? outlineLvl)
    {
        if (outlineLvl.HasValue && outlineLvl.Value >= 0 && outlineLvl.Value <= 8)
            return outlineLvl.Value + 1;

        return styleId switch
        {
            "Heading1" or "1" => 1,
            "Heading2" or "2" => 2,
            "Heading3" or "3" => 3,
            "Heading4" or "4" => 4,
            "Heading5" or "5" => 5,
            "Heading6" or "6" => 6,
            "Heading7" or "7" => 7,
            "Heading8" or "8" => 8,
            "Heading9" or "9" => 9,
            _ => 1,
        };
    }

    // ========================================================================
    // TOC and Field detection
    // ========================================================================

    private static bool HasTocField(Paragraph para)
    {
        foreach (var fc in para.Descendants<FieldCode>())
        {
            if (fc.InnerText.Contains("TOC", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static List<string> ExtractTocSwitches(Paragraph para)
    {
        var switches = new List<string>();
        foreach (var fc in para.Descendants<FieldCode>())
        {
            var matches = Regex.Matches(fc.InnerText, @"\\[a-zA-Z]");
            foreach (Match m in matches)
                switches.Add(m.Value);
        }
        return switches;
    }

    private static bool HasComplexField(Paragraph para)
    {
        var fieldChars = para.Descendants<FieldChar>().ToList();
        if (fieldChars.Count == 0) return false;

        foreach (var fc in para.Descendants<FieldCode>())
        {
            var code = fc.InnerText.Trim().ToUpperInvariant();
            if (code.StartsWith("PAGE") || code.StartsWith("DATE") ||
                code.StartsWith("SEQ") || code.StartsWith("REF") ||
                code.StartsWith("NUMPAGES") || code.StartsWith("SECTION") ||
                code.StartsWith("SECTIONPAGES") || code.StartsWith("STYLEREF") ||
                code.StartsWith("DOCPROPERTY") || code.StartsWith("MERGEFIELD") ||
                code.StartsWith("HYPERLINK"))
                return true;
        }
        return false;
    }

    private static string? ExtractFieldCode(Paragraph para)
    {
        var codes = new List<string>();
        foreach (var fc in para.Descendants<FieldCode>())
            codes.Add(fc.InnerText);
        return codes.Count > 0 ? string.Join(" ", codes) : null;
    }

    // ========================================================================
    // Comment anchor helpers
    // ========================================================================

    /// <summary>
    /// Collect anchor text for each comment by walking siblings
    /// between CommentRangeStart and CommentRangeEnd markers.
    /// </summary>
    private static Dictionary<string, string> CollectCommentAnchors(Body? body)
    {
        var anchors = new Dictionary<string, string>();
        if (body == null) return anchors;

        foreach (var crs in body.Descendants<CommentRangeStart>())
        {
            var commentId = crs.Id?.Value;
            if (string.IsNullOrEmpty(commentId)) continue;

            var sb = new StringBuilder();
            var current = crs.NextSibling();
            while (current != null)
            {
                if (current is CommentRangeEnd end && end.Id?.Value == commentId)
                    break;

                sb.Append(current.InnerText);
                current = current.NextSibling();
            }

            var anchor = sb.ToString().Trim();
            if (anchor.Length > 0)
                anchors[commentId] = anchor;
        }

        return anchors;
    }

    /// <summary>
    /// For each CommentRangeStart, walk up to find the parent paragraph,
    /// then match that paragraph's text against block text previews to
    /// determine the nearest enclosing block ID.
    /// </summary>
    private static Dictionary<string, string> CollectCommentAnchorBlockIds(Body? body, List<NongBlock> blocks)
    {
        var result = new Dictionary<string, string>();
        if (body == null) return result;

        foreach (var crs in body.Descendants<CommentRangeStart>())
        {
            var commentId = crs.Id?.Value;
            if (string.IsNullOrEmpty(commentId)) continue;

            // Walk up to find parent paragraph
            var parent = crs.Parent;
            while (parent != null && parent is not Paragraph)
                parent = parent.Parent;

            if (parent is not Paragraph para) continue;

            string paraText = para.InnerText.Trim();
            if (paraText.Length == 0) continue;

            // Find the first block whose text preview overlaps with paragraph text
            foreach (var block in blocks)
            {
                string? blockText = ExtractTextPreview(block);
                if (blockText == null || blockText.Length < 3) continue;

                int checkLen = Math.Min(paraText.Length, 20);
                string paraPrefix = paraText[..checkLen];

                if (paraPrefix.StartsWith(blockText[..Math.Min(blockText.Length, checkLen)],
                        StringComparison.OrdinalIgnoreCase) ||
                    blockText.StartsWith(paraPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    result[commentId] = block.Id;
                    break;
                }
            }
        }

        return result;
    }

    // ========================================================================
    // Utility helpers
    // ========================================================================

    private static string ContentTypeToExtension(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/gif" => ".gif",
        "image/bmp" => ".bmp",
        "image/tiff" => ".tiff",
        "image/x-wmf" => ".wmf",
        "image/x-emf" => ".emf",
        "image/svg+xml" => ".svg",
        "image/webp" => ".webp",
        _ => ".bin",
    };

    private static NongMetrics ComputeMetrics(List<NongBlock> blocks)
    {
        var m = new NongMetrics { Blocks = blocks.Count };
        foreach (var block in blocks)
        {
            switch (block)
            {
                case ParagraphBlock: m.Paragraphs++; break;
                case HeadingBlock: m.Headings++; break;
                case TableBlock: m.Tables++; break;
                case ImageBlock: m.Images++; break;
                case EquationBlock: m.Equations++; break;
                case ChemEquationBlock: m.ChemEquations++; break;
                case ChemicalStructureBlock: m.ChemStructures++; break;
                case FootnoteBlock: m.Footnotes++; break;
                case EndnoteBlock: m.Endnotes++; break;
                case CommentBlock: m.Comments++; break;
                case RevisionBlock: m.Revisions++; break;
                case HyperlinkBlock: m.Hyperlinks++; break;
                case BookmarkBlock: m.Bookmarks++; break;
                case FigureBlock: m.Figures++; break;
                case TocBlock: m.Tocs++; break;
                case FieldBlock: m.Fields++; break;
                case RawOpenXmlRefBlock: m.RawRefs++; break;
            }
        }
        return m;
    }

    private static void WriteJson(string path, object obj)
    {
        var json = JsonSerializer.Serialize(obj, obj.GetType(), JsonOpts);
        File.WriteAllText(path, json, Encoding.UTF8);
    }
}
