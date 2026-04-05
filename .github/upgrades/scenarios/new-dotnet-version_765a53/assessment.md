# Projects and dependencies analysis

This document provides a comprehensive overview of the projects and their dependencies in the context of upgrading to .NETCoreApp,Version=v10.0.

## Table of Contents

- [Executive Summary](#executive-Summary)
  - [Highlevel Metrics](#highlevel-metrics)
  - [Projects Compatibility](#projects-compatibility)
  - [Package Compatibility](#package-compatibility)
  - [API Compatibility](#api-compatibility)
- [Aggregate NuGet packages details](#aggregate-nuget-packages-details)
- [Top API Migration Challenges](#top-api-migration-challenges)
  - [Technologies and Features](#technologies-and-features)
  - [Most Frequent API Issues](#most-frequent-api-issues)
- [Projects Relationship Graph](#projects-relationship-graph)
- [Project Details](#project-details)

  - [Gw2Giveaway\Gw2Giveaway.csproj](#gw2giveawaygw2giveawaycsproj)


## Executive Summary

### Highlevel Metrics

| Metric | Count | Status |
| :--- | :---: | :--- |
| Total Projects | 1 | All require upgrade |
| Total NuGet Packages | 6 | 1 need upgrade |
| Total Code Files | 25 |  |
| Total Code Files with Incidents | 1 |  |
| Total Lines of Code | 6005 |  |
| Total Number of Issues | 2 |  |
| Estimated LOC to modify | 0+ | at least 0,0% of codebase |

### Projects Compatibility

| Project | Target Framework | Difficulty | Package Issues | API Issues | Est. LOC Impact | Description |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| [Gw2Giveaway\Gw2Giveaway.csproj](#gw2giveawaygw2giveawaycsproj) | net8.0-windows | 🟢 Low | 1 | 0 |  | Wpf, Sdk Style = True |

### Package Compatibility

| Status | Count | Percentage |
| :--- | :---: | :---: |
| ✅ Compatible | 5 | 83,3% |
| ⚠️ Incompatible | 0 | 0,0% |
| 🔄 Upgrade Recommended | 1 | 16,7% |
| ***Total NuGet Packages*** | ***6*** | ***100%*** |

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 0 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 0 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 0 |  |
| ***Total APIs Analyzed*** | ***0*** |  |

## Aggregate NuGet packages details

| Package | Current Version | Suggested Version | Projects | Description |
| :--- | :---: | :---: | :--- | :--- |
| Microsoft.Data.Sqlite | 10.0.2 | 10.0.5 | [Gw2Giveaway.csproj](#gw2giveawaygw2giveawaycsproj) | NuGet package upgrade is recommended |
| Newtonsoft.Json | 13.0.4 |  | [Gw2Giveaway.csproj](#gw2giveawaygw2giveawaycsproj) | ✅Compatible |
| PixiEditor.ColorPicker | 3.4.2.2 |  | [Gw2Giveaway.csproj](#gw2giveawaygw2giveawaycsproj) | ✅Compatible |
| TwitchLib.Api | 3.10.2 |  | [Gw2Giveaway.csproj](#gw2giveawaygw2giveawaycsproj) | ✅Compatible |
| TwitchLib.Client | 4.0.1 |  | [Gw2Giveaway.csproj](#gw2giveawaygw2giveawaycsproj) | ✅Compatible |
| TwitchLib.EventSub.Websockets | 0.8.0 |  | [Gw2Giveaway.csproj](#gw2giveawaygw2giveawaycsproj) | ✅Compatible |

## Top API Migration Challenges

### Technologies and Features

| Technology | Issues | Percentage | Migration Path |
| :--- | :---: | :---: | :--- |

### Most Frequent API Issues

| API | Count | Percentage | Category |
| :--- | :---: | :---: | :--- |

## Projects Relationship Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart LR
    P1["<b>📦&nbsp;Gw2Giveaway.csproj</b><br/><small>net8.0-windows</small>"]
    click P1 "#gw2giveawaygw2giveawaycsproj"

```

## Project Details

<a id="gw2giveawaygw2giveawaycsproj"></a>
### Gw2Giveaway\Gw2Giveaway.csproj

#### Project Info

- **Current Target Framework:** net8.0-windows
- **Proposed Target Framework:** net10.0-windows
- **SDK-style**: True
- **Project Kind:** Wpf
- **Dependencies**: 0
- **Dependants**: 0
- **Number of Files**: 28
- **Number of Files with Incidents**: 1
- **Lines of Code**: 6005
- **Estimated LOC to modify**: 0+ (at least 0,0% of the project)

#### Dependency Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart TB
    subgraph current["Gw2Giveaway.csproj"]
        MAIN["<b>📦&nbsp;Gw2Giveaway.csproj</b><br/><small>net8.0-windows</small>"]
        click MAIN "#gw2giveawaygw2giveawaycsproj"
    end

```

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 0 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 0 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 0 |  |
| ***Total APIs Analyzed*** | ***0*** |  |

