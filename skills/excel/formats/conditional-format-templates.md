# Conditional Formatting Templates

## Data Bars

### Blue data bars (monochrome default)
```csharp
ExcelBuilder.Sheet(wb, "Sheet").DataBars("C2:C100", "4A90D9");
```
```csharp
ws.Range("C2:C100").AddConditionalFormat()
    .DataBar(XLColor.FromHtml("4A90D9"))
    .LowestValue()
    .HighestValue();
```

### Green/red data bars (finance — positive up)
```csharp
ws.Range("D2:D100").AddConditionalFormat()
    .DataBar(XLColor.FromHtml("63BE7B"), XLColor.FromHtml("F8696B"))
    .LowestValue()
    .HighestValue();
```

## Color Scales

### Red-Yellow-Green (3-color)
```csharp
ExcelBuilder.Sheet(wb, "Sheet").ColorScale("E2:E100", "F8696B", "FFEB84", "63BE7B");
```
```csharp
ws.Range("E2:E100").AddConditionalFormat()
    .ColorScale()
    .LowestValue(XLColor.FromHtml("F8696B"))
    .Midpoint(XLCFContentType.Percent, 50, XLColor.FromHtml("FFEB84"))
    .HighestValue(XLColor.FromHtml("63BE7B"));
```

### White to Blue (2-color)
```csharp
ws.Range("F2:F100").AddConditionalFormat()
    .ColorScale()
    .LowestValue(XLColor.FromHtml("FFFFFF"))
    .HighestValue(XLColor.FromHtml("4A90D9"));
```

## Icon Sets

```csharp
// Traffic lights
ws.Range("G2:G100").AddConditionalFormat()
    .IconSet(XLIconSetStyle.ThreeTrafficLights1, false, false);

// Directional arrows
ws.Range("H2:H100").AddConditionalFormat()
    .IconSet(XLIconSetStyle.ThreeArrows, false, false);

// Ratings (5 stars)
ws.Range("I2:I100").AddConditionalFormat()
    .IconSet(XLIconSetStyle.FiveRating, false, false);
```

Available icon styles: `ThreeArrows`, `ThreeArrowsGray`, `ThreeTrafficLights1`, `ThreeTrafficLights2`, `ThreeSigns`, `ThreeSymbols`, `ThreeSymbols2`, `ThreeFlags`, `FourArrows`, `FourArrowsGray`, `FourRedToBlack`, `FourRating`, `FourTrafficLights`, `FiveArrows`, `FiveArrowsGray`, `FiveRating`, `FiveQuarters`.

## Cell Highlighting

```csharp
// Value above threshold → green fill
ws.Range("J2:J100").AddConditionalFormat()
    .WhenGreaterThan(100000)
    .Fill.SetBackgroundColor(XLColor.FromHtml("C6EFCE"));

// Value below threshold → red fill
ws.Range("K2:K100").AddConditionalFormat()
    .WhenLessThan(0)
    .Fill.SetBackgroundColor(XLColor.FromHtml("FFC7CE"));

// Text contains → highlight
ws.Range("L2:L100").AddConditionalFormat()
    .WhenContains("Error")
    .Fill.SetBackgroundColor(XLColor.FromHtml("FFC7CE"));

// Duplicate values
ws.Range("M2:M100").AddConditionalFormat()
    .WhenIsDuplicate()
    .Fill.SetBackgroundColor(XLColor.FromHtml("FFEB84"));

// Top 10
ws.Range("N2:N100").AddConditionalFormat()
    .WhenIsTop(10)
    .Fill.SetBackgroundColor(XLColor.FromHtml("C6EFCE"));

// Formula-based
ws.Range("A2:D100").AddConditionalFormat()
    .WhenIsTrue("$D2>1000")
    .Font.SetBold();
```

## Usage Limits

- Apply conditional formatting to 2-4 key columns per sheet maximum
- Consistent color semantics across the workbook
- Data bars + number values together provide the most information density
- Color scales are best for distribution visualization (heat maps)
- Icon sets work best on summary KPIs
