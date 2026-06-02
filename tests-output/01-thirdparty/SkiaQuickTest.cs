using SkiaSharp;

try {
    var info = new SKImageInfo(100, 100);
    using var bmp = new SKBitmap(info);
    Console.WriteLine("PASS: SKBitmap created " + bmp.Width + "x" + bmp.Height);
} catch (Exception ex) {
    Console.WriteLine("FAIL: " + ex.GetType().Name + " - " + ex.Message);
}
