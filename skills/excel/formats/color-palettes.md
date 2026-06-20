# Color Palettes

## Minimalist Monochrome (Default)

```
Header background (dark grey):   #333333
Header foreground (white):       #FFFFFF
Alternating row:                 #F5F5F5
Accent (blue):                   #0066CC
Border (light grey):             #D0D0D0
```

Usage:
```csharp
StylePresets.Colors.MonoHeaderBg
StylePresets.Colors.MonoHeaderFg
StylePresets.Colors.MonoRowAlt
StylePresets.Colors.MonoAccent
StylePresets.Colors.MonoBorder
```

## Professional Finance

```
Header background (navy):        #1F4E79
Header foreground (white):       #FFFFFF
Accent warm (cream):             #FFF3E0
```

Usage:
```csharp
StylePresets.Colors.FinHeaderBg
StylePresets.Colors.FinHeaderFg
StylePresets.Colors.FinAccentWarm
```

## Regional Finance Color Convention

### China (Mainland)
- Price UP:   Red (#FF0000) — `StylePresets.Colors.UpRed`
- Price DOWN: Green (#009933) — `StylePresets.Colors.DownGreen`

### International
- Price UP:   Green (#009933) — `StylePresets.Colors.UpGreen`
- Price DOWN: Red (#FF0000) — `StylePresets.Colors.DownRed`

## Financial Model Text Colors

- Blue (#0066CC): Hardcoded inputs, assumptions that users change
- Black (#000000): All formulas and calculations
- Green (#008000): Links to other sheets within same workbook
- Red (#FF0000): Links to external files
- Yellow background (#FFFF00): Key assumptions needing attention
