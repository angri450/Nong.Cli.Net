# Angri450.Nong.Metaso

Pure .NET 8 Metaso REST API client for `nong lit` and `nong metaso`.

Provides web/search result retrieval, metadata normalization, and deterministic JSON output over `HttpClient`.

[![NuGet](https://img.shields.io/nuget/v/Angri450.Nong.Metaso)](https://www.nuget.org/packages/Angri450.Nong.Metaso)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)](https://dotnet.microsoft.com)

## Install

```bash
dotnet add package Angri450.Nong.Metaso
```

## Quick Start

```csharp
using Metaso;

var client = new MetasoClient();
var results = await client.SearchAsync("PP-OCRv6", take: 10);
```
