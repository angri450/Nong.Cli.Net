using SkiaSharp;

namespace DiagramCore.Renderers;

public interface IRenderer
{
    void Render(string outputPath, int width = 800, int height = 600);
    SKBitmap RenderToBitmap(int width = 800, int height = 600);
}
