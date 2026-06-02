namespace PptxCore;

public static class LayoutSystem
{
    public const int SlideWidth = 960;
    public const int SlideHeight = 540;

    public const int Spacing_XS = 8;
    public const int Spacing_SM = 16;
    public const int Spacing_MD = 24;
    public const int Spacing_LG = 32;
    public const int Spacing_XL = 48;

    public const int Margin_X = 48;
    public const int Margin_Y = 40;
    public const int ContentWidth = SlideWidth - 2 * Margin_X;

    public static class FontSizes
    {
        public const decimal Display = 56;
        public const decimal H1 = 40;
        public const decimal H2 = 32;
        public const decimal H3 = 24;
        public const decimal Body_LG = 20;
        public const decimal Body = 16;
        public const decimal Body_SM = 14;
        public const decimal Caption = 12;
    }

    public static class Cover
    {
        public const int TitleY = 160;
        public const int SubtitleY = 240;
        public const int AuthorY = 440;
        public const int DecorationY = 100;
        public const int SubtitleHeight = 40;
    }

    public static class Content
    {
        public const int TitleY = 40;
        public const int TitleHeight = 50;
        public const int BodyY = 110;
        public const int BulletLineHeight = 36;
        public const int BulletIndent = 24;
    }

    public static class Chart
    {
        public const int TitleY = 40;
        public const int ChartY = 110;
        public const int ChartWidth = 864;
        public const int ChartHeight = 380;
    }

    public static class Card
    {
        public const int DefaultHeight = 240;
        public const int TitleHeight = 40;
        public const int BodyHeight = 180;
        public const int Padding = 16;
    }

    public static class TwoColumn
    {
        public const int ColumnSpacing = 32;
        public static int ColumnWidth => (ContentWidth - ColumnSpacing) / 2;
    }

    public static class ThreeColumn
    {
        public const int ColumnSpacing = 24;
        public static int ColumnWidth => (ContentWidth - 2 * ColumnSpacing) / 3;
    }

    public static class Decoration
    {
        public const int AccentLineY = 95;
        public const int AccentLineWidth = 60;
        public const int AccentLineHeight = 3;
        public const int FooterY = 510;
    }
}
