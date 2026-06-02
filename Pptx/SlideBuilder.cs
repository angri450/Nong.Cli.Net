using ShapeCrawler;

namespace PptxCore;

public static class SlideBuilder
{
    public static PresentationBuilder Create()
    {
        var pres = new Presentation();
        return new PresentationBuilder(pres);
    }

    public static PresentationBuilder Open(string path)
    {
        var pres = new Presentation(path);
        return new PresentationBuilder(pres);
    }
}
