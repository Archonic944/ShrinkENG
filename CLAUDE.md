# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ShrinkENG is a natural language compression tool for English text that compresses words using dictionary indices with static operators for capitalization, punctuation, and whitespace. It's implemented as a .NET 6 Blazor WebAssembly application that generates static files for deployment.

## Build and Development Commands

- **Build and run (debug)**: `dotnet run`
- **Build release**: `dotnet build -c Release`
- **Build static files**: Static files are generated in `bin/Debug/net6.0/wwwroot/` or `bin/Release/net6.0/wwwroot/`
- **Deploy to Vercel**: Upload the contents of the `wwwroot` directory to Vercel

## Architecture

**Core Components:**
- `ShrinkEngine.cs`: Main compression/decompression engine with dictionary management
- `Operator.cs`: Handles text operators (capitalization, punctuation, whitespace)
- `ShrinkUtils.cs`: String manipulation utilities
- `Pages/Index.razor`: Main Blazor page with file upload/download functionality
- `Components/MainLayout.razor`: Application layout component
- `Program.cs`: Blazor Server startup configuration

**Web Components:**
- `Pages/_Host.cshtml`: Main host page for Blazor Server
- `Pages/Shared/_Layout.cshtml`: HTML layout template
- `wwwroot/css/app.css`: Application styles with Bootstrap integration
- `wwwroot/js/app.js`: JavaScript for file download functionality

**Compression Format:**
- File format starts with version (2 bytes) + magic number "SENG" (4 bytes)
- Dictionary: 25,000 most common English words stored as variable-length integers
- Operators are packed into bytes using a custom bit schema (see ShrinkEngine.cs:359-403)
- UTF-8 fallback for words not in dictionary

**Key Technical Details:**
- Dictionary loaded from embedded resource `resources/wordfreq-en-25000.txt`
- Uses 7-bit variable-length encoding for word indices
- Operator state packed into 1-2 bytes with extension byte for complex cases
- Supports streaming compression/decompression from either end of stream
- Web interface supports file upload (max 50MB) and download via JavaScript

## File Structure

- `Pages/`: Blazor pages and layouts
- `Components/`: Reusable Blazor components
- `wwwroot/`: Static web assets (CSS, JavaScript, images)
- `resources/`: Contains word frequency dictionary
- `test/`: Sample files for testing compression/decompression
- `publish-scripts/`: Build scripts for web deployment

## Development Notes

- Project uses Blazor WebAssembly for client-side execution
- Configured with Bootstrap 5 and Font Awesome icons
- File processing handled entirely client-side using WebAssembly
- State management: ShrinkEngine.State tracks initialization (0=uninitialized, 1=ready, 2=error)
- Supports both .txt (compress) and .eng (decompress) file formats via web interface