# Angri450.Nong.Aminer

Pure .NET 8 AMiner REST API client for `nong lit` and `nong aminer`.

Provides scholar search, paper search, patent search, organization search, venue search, and metadata normalization over `HttpClient`.

[![NuGet](https://img.shields.io/nuget/v/Angri450.Nong.Aminer)](https://www.nuget.org/packages/Angri450.Nong.Aminer)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)](https://dotnet.microsoft.com)

## Install

```bash
dotnet add package Angri450.Nong.Aminer
```

## Quick Start

```csharp
using Aminer;

var client = new AminerClient();
var papers = await client.SearchPapersAsync("photosynthesis", take: 10);
```
