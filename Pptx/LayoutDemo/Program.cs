using PptxCore;

var builder = SlideBuilder.Create()
    .Theme(ThemePreset.Professional)
    .AddTitleSlide(opt => opt
        .Title("PptxCore v2.0 — Layout System Demo")
        .Subtitle("Gravity-field layouts + CJK fonts + Borders + Rotation")
        .Author("angri450"))

    .AddSlide()
        .TextBox("SingleFocus", 48, 40, 864, 60, fontSize: 28, bold: true, colorHex: "1F4E79")
        .SingleFocus(mainContent: "120%", subtitle: "Year-over-year growth")
        .EndSlide()

    .AddSlide()
        .TextBox("Symmetric", 48, 40, 864, 60, fontSize: 28, bold: true, colorHex: "1F4E79")
        .Symmetric(leftTitle: "Before", leftContent: "Manual process, 8 hours",
                   rightTitle: "After", rightContent: "Automated, 15 minutes")
        .EndSlide()

    .AddSlide()
        .TextBox("Asymmetric", 48, 40, 864, 60, fontSize: 28, bold: true, colorHex: "1F4E79")
        .Asymmetric(mainTitle: "Core Finding", mainContent: "AI adoption +40% productivity",
                    sideTitle: "Methodology", sideContent: "500 companies, 2020-2024")
        .EndSlide()

    .AddSlide()
        .TextBox("ThreeColumn", 48, 40, 864, 60, fontSize: 28, bold: true, colorHex: "1F4E79")
        .ThreeColumn(c1t: "Speed", c1c: "2x faster", c2t: "Quality", c2c: "95% accuracy", c3t: "Cost", c3c: "50% reduction")
        .EndSlide()

    .AddSlide()
        .TextBox("PrimarySecondary", 48, 40, 864, 60, fontSize: 28, bold: true, colorHex: "1F4E79")
        .PrimarySecondary(mainTitle: "Key Insight", mainContent: "Strong AI-revenue correlation",
            ("Sample", "500 companies"), ("Period", "2020-2024"), ("Confidence", "95%"))
        .EndSlide()

    .AddSlide()
        .HeroTop(heroTitle: "Q4 2024 Results", heroContent: "Record-breaking quarter",
            ("Revenue", "$120M"), ("Users", "2.5M"), ("NPS", "72"), ("Retention", "94%"))
        .EndSlide()

    .AddSlide()
        .Cards(title: "Card Layouts",
            ("Revenue", "$120M, +30% YoY"),
            ("Users", "2.5M active"),
            ("Retention", "94%"))
        .EndSlide()

    .AddSlide()
        .BigNumber(number: "120%", description: "Year-over-year growth", unit: "growth")
        .EndSlide()

    .AddSlide()
        .Shape(Geometry.Triangle, 40, 260, 30, 30, fillHex: "1F4E79", rotation: -10)
        .Quote(quoteText: "The future belongs to those who prepare for it today.", attribution: "Malcolm X")
        .EndSlide()

    .AddTableSlide(opt => opt
        .Title("10 Themes")
        .Data(new[] {
            new[] { "Theme", "Accent", "Use Case" },
            new[] { "Professional", "#1F4E79", "Business" },
            new[] { "Coral Energy", "#F96167", "Startup" },
            new[] { "Teal Trust", "#028090", "Medical" }
        }))

    .AddChartSlide(opt => opt
        .Title("Theme Distribution")
        .PieChart(new Dictionary<string, double> { { "Business", 4 }, { "Creative", 3 }, { "Data", 3 } }))

    .Notes("PptxCore v2.0 — all features verified")
    .Save("layout-demo.pptx");

Console.WriteLine("Done: layout-demo.pptx");
