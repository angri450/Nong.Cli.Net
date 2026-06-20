# Number Format Strings

## ClosedXML / Excel Number Format Quick Reference

### Integers & Decimals
| Format | Output | Constant |
|--------|--------|----------|
| `#,##0` | 1,500 | ``Formats.Integer`` |
| `#,##0.00` | 1,500.00 | ``Formats.Decimal2`` |

### Currency
| Format | Output | Constant |
|--------|--------|----------|
| `"¥"#,##0.00` | ¥1,500.00 | ``Formats.CurrencyRmb`` |
| `"$"#,##0.00` | $1,500.00 | ``Formats.CurrencyUsd`` |

### Percentage
| Format | Output | Constant |
|--------|--------|----------|
| `0.0%` | 15.0% | ``Formats.Percent1`` |
| `0.00%` | 15.00% | ``Formats.Percent2`` |

### Special
| Format | Output | Constant |
|--------|--------|----------|
| `0.0"x"` | 12.5x | ``Formats.Multiple`` |
| `@` | 2026 | ``Formats.Text`` (years as text) |
| `#,##0;-#,##0;"-"` | 1,500 / -1,500 / - | ``Formats.ZeroDash`` |
| `yyyy-mm-dd` | 2026-01-01 | ``Formats.DateYMD`` |

### Usage

```csharp
// Direct
cell.Style.NumberFormat.SetFormat("#,##0.00");

// Via ExcelBuilder
ExcelBuilder.Sheet(wb, "Sheet").NumberFormat("B2:D100", "#,##0");

// Via constants
cell.Style.NumberFormat.SetFormat(StylePresets.Formats.CurrencyRmb);
```

### Common Format Patterns

| Pattern | Meaning |
|---------|---------|
| `0` | Digit placeholder, always shows |
| `#` | Digit placeholder, hides insignificant zeros |
| `.` | Decimal separator |
| `,` | Thousands separator |
| `%` | Multiply by 100 and show % |
| `"text"` | Literal text |
| `;` | Section separator (positive;negative;zero) |
| `@` | Text placeholder |
