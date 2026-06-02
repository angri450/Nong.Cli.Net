using System;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;

[assembly: AssemblyTitle("SkiaSharp")]
[assembly: AssemblyDescription("SkiaSharp is a cross-platform 2D graphics API for .NET platforms that can be used across mobile, server and desktop models to render images.")]
[assembly: AssemblyCompany("Microsoft Corporation")]
[assembly: AssemblyProduct("SkiaSharp")]
[assembly: AssemblyCopyright("© Microsoft Corporation. All rights reserved.")]
[assembly: NeutralResourcesLanguage("en")]

[assembly: InternalsVisibleTo("SkiaSharp.HarfBuzz")]
[assembly: InternalsVisibleTo("ScottPlot")]

[assembly: AssemblyMetadata("IsTrimmable", "True")]

#if NET7_0_OR_GREATER
[assembly: DisableRuntimeMarshalling]
#endif
