# Docusaurus + OpenAPI Implementation Plan
# VoiceByAuribus API Documentation Site

**Created**: 2025-11-25
**Status**: In Progress
**Goal**: Create a professional, interactive documentation site for VoiceByAuribus API using Docusaurus + OpenAPI plugins

---

## 📋 Overview

This document outlines the complete implementation plan for creating an online documentation site for the VoiceByAuribus API. The site will be:
- Easy to edit (Markdown-based)
- Professional and client-ready
- Interactive with OpenAPI integration
- Auto-deployable via CI/CD
- Free to host (GitHub Pages or Vercel)

---

## 🎯 Technology Stack

- **Docusaurus 3.x**: Static site generator for documentation
- **TypeScript**: Type-safe configuration
- **docusaurus-plugin-openapi-docs**: Generates API docs from OpenAPI spec
- **docusaurus-theme-openapi-docs**: Interactive API documentation theme
- **GitHub Pages / Vercel**: Free hosting options
- **GitHub Actions**: Auto-deploy on push to main

---

## 📦 Project Structure

```
VoiceByAuribus-API/
├── docs-site/                         # NEW: Documentation site (Docusaurus)
│   ├── docs/                          # Markdown documentation
│   │   ├── intro.md
│   │   ├── getting-started/
│   │   ├── guides/
│   │   ├── features/
│   │   ├── architecture/
│   │   ├── security/
│   │   └── api/                       # Auto-generated from OpenAPI
│   ├── openapi/
│   │   └── voicebyauribus-api.yaml    # OpenAPI specification
│   ├── src/
│   │   ├── components/                # Custom React components
│   │   ├── css/
│   │   │   └── custom.css             # Branding customization
│   │   └── pages/
│   ├── static/
│   │   └── img/                       # Logos, diagrams, assets
│   ├── docusaurus.config.ts           # Main configuration
│   ├── sidebars.ts                    # Sidebar structure
│   ├── package.json
│   └── tsconfig.json
│
├── .github/workflows/
│   └── deploy-docs.yml                # NEW: CI/CD for docs deployment
│
├── .ai_doc/                           # Existing docs (will migrate)
│   ├── ARCHITECTURE.md
│   ├── COGNITO_M2M_AUTH.md
│   ├── WEBHOOK_AND_API_CONVENTIONS.md
│   └── v1/*.md
│
└── VoiceByAuribus.API/                # Existing API project
```

---

## 🚀 Implementation Phases

### Phase 1: Setup Docusaurus Project ✅

**Goal**: Create base Docusaurus project with TypeScript

**Commands**:
```bash
# From repository root
npx create-docusaurus@latest docs-site classic --typescript
cd docs-site
```

**Generated Structure**:
```
docs-site/
├── docs/
│   ├── intro.md
│   └── tutorial-basics/
├── src/
│   ├── components/
│   ├── css/
│   └── pages/
├── static/
│   └── img/
├── docusaurus.config.ts
├── sidebars.ts
├── package.json
└── tsconfig.json
```

**Verification**:
```bash
npm start  # Should open http://localhost:3000
```

---

### Phase 2: Install OpenAPI Plugins ✅

**Goal**: Add OpenAPI documentation generation capabilities

**Commands**:
```bash
cd docs-site
npm install --save \
  docusaurus-plugin-openapi-docs \
  docusaurus-theme-openapi-docs
```

**Updated `package.json`** should include:
```json
{
  "dependencies": {
    "docusaurus-plugin-openapi-docs": "^3.x.x",
    "docusaurus-theme-openapi-docs": "^3.x.x"
  }
}
```

---

### Phase 3: Generate OpenAPI Specification 📝

**Goal**: Create OpenAPI spec from existing API

**Option A: Add Swashbuckle to .NET API** (Recommended)

1. Install NuGet package:
```bash
cd ../VoiceByAuribus.API
dotnet add package Swashbuckle.AspNetCore
```

2. Configure in `Program.cs`:
```csharp
// Add before builder.Build()
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VoiceByAuribus API",
        Version = "v1",
        Description = "Voice Model Management and Audio Processing API",
        Contact = new OpenApiContact
        {
            Name = "Auribus",
            Email = "support@auribus.com"
        }
    });

    // Include XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);

    // Add JWT authentication
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add after app is built
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "VoiceByAuribus API v1");
    });
}
```

3. Enable XML documentation in `.csproj`:
```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

4. Generate OpenAPI spec:
```bash
dotnet run
# Navigate to http://localhost:5037/swagger/v1/swagger.json
# Save to docs-site/openapi/voicebyauribus-api.yaml
```

**Option B: Create manually** (If Swashbuckle not suitable)

Create `docs-site/openapi/voicebyauribus-api.yaml`:
```yaml
openapi: 3.0.1
info:
  title: VoiceByAuribus API
  description: Voice Model Management and Audio Processing API
  version: '1.0'
  contact:
    name: Auribus
    email: support@auribus.com
servers:
  - url: https://api.voicebyauribus.com
    description: Production
  - url: https://staging-api.voicebyauribus.com
    description: Staging
paths:
  /api/v1/voices:
    get:
      tags:
        - Voices
      summary: Get all voice models
      # ... (continue with all endpoints)
```

---

### Phase 4: Configure Docusaurus 🔧

**Goal**: Configure Docusaurus with OpenAPI plugin and branding

**File**: `docs-site/docusaurus.config.ts`

```typescript
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

const config: Config = {
  title: 'VoiceByAuribus API Documentation',
  tagline: 'Voice Model Management and Audio Processing API',
  favicon: 'img/favicon.ico',

  // Your production URL
  url: 'https://docs.voicebyauribus.com',
  baseUrl: '/',

  // GitHub Pages deployment
  organizationName: 'auribus',
  projectName: 'voicebyauribus-api-docs',

  onBrokenLinks: 'throw',
  onBrokenMarkdownLinks: 'warn',

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
          routeBasePath: 'docs',
          docItemComponent: "@theme/ApiItem",
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  plugins: [
    [
      'docusaurus-plugin-openapi-docs',
      {
        id: "api",
        docsPluginId: "classic",
        config: {
          voicebyauribus: {
            specPath: "openapi/voicebyauribus-api.yaml",
            outputDir: "docs/api",
            sidebarOptions: {
              groupPathsBy: "tag",
              categoryLinkSource: "tag",
            },
          },
        },
      },
    ],
  ],

  themes: ["docusaurus-theme-openapi-docs"],

  themeConfig: {
    image: 'img/auribus-social-card.jpg',

    navbar: {
      title: 'VoiceByAuribus',
      logo: {
        alt: 'Auribus Logo',
        src: 'img/logo.svg',
        srcDark: 'img/logo-dark.svg',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'tutorialSidebar',
          position: 'left',
          label: 'Documentation',
        },
        {
          to: '/docs/api',
          label: 'API Reference',
          position: 'left',
        },
        {
          href: 'https://github.com/auribus/voicebyauribus-api',
          label: 'GitHub',
          position: 'right',
        },
      ],
    },

    footer: {
      style: 'dark',
      links: [
        {
          title: 'Documentation',
          items: [
            {
              label: 'Getting Started',
              to: '/docs/getting-started/quickstart',
            },
            {
              label: 'API Reference',
              to: '/docs/api',
            },
          ],
        },
        {
          title: 'Community',
          items: [
            {
              label: 'Support',
              href: 'mailto:support@auribus.com',
            },
          ],
        },
        {
          title: 'More',
          items: [
            {
              label: 'GitHub',
              href: 'https://github.com/auribus/voicebyauribus-api',
            },
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} Auribus. Built with Docusaurus.`,
    },

    prism: {
      theme: require('prism-react-renderer').themes.github,
      darkTheme: require('prism-react-renderer').themes.dracula,
      additionalLanguages: ['csharp', 'bash', 'json', 'yaml'],
    },

    colorMode: {
      defaultMode: 'light',
      disableSwitch: false,
      respectPrefersColorScheme: true,
    },

  } satisfies Preset.ThemeConfig,
};

export default config;
```

---

### Phase 5: Create Documentation Structure 📁

**Goal**: Set up folder structure and sidebar configuration

**Step 5.1**: Create folder structure

```bash
cd docs-site/docs
mkdir -p getting-started guides features architecture security
```

**Step 5.2**: Configure sidebar

**File**: `docs-site/sidebars.ts`

```typescript
import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
  tutorialSidebar: [
    {
      type: 'doc',
      id: 'intro',
      label: 'Introduction',
    },
    {
      type: 'category',
      label: 'Getting Started',
      collapsed: false,
      items: [
        'getting-started/quickstart',
        'getting-started/authentication',
        'getting-started/environments',
        'getting-started/rate-limits',
      ],
    },
    {
      type: 'category',
      label: 'Guides',
      items: [
        'guides/uploading-audio',
        'guides/voice-conversion',
        'guides/pitch-shifting',
        'guides/webhooks',
        'guides/error-handling',
      ],
    },
    {
      type: 'category',
      label: 'Features',
      items: [
        'features/voices',
        'features/audio-files',
        'features/voice-conversions',
        'features/webhook-subscriptions',
      ],
    },
    {
      type: 'category',
      label: 'Architecture',
      items: [
        'architecture/overview',
        'architecture/vertical-slices',
        'architecture/data-flow',
      ],
    },
    {
      type: 'category',
      label: 'Security',
      items: [
        'security/authentication',
        'security/webhook-signatures',
        'security/best-practices',
      ],
    },
  ],
};

export default sidebars;
```

---

### Phase 6: Customize Branding 🎨

**Goal**: Apply Auribus branding (colors, logo, fonts)

**File**: `docs-site/src/css/custom.css`

```css
/**
 * VoiceByAuribus API Documentation Custom Styles
 */

:root {
  /* Primary brand colors */
  --ifm-color-primary: #2e8555;
  --ifm-color-primary-dark: #29784c;
  --ifm-color-primary-darker: #277148;
  --ifm-color-primary-darkest: #205d3b;
  --ifm-color-primary-light: #33925d;
  --ifm-color-primary-lighter: #359962;
  --ifm-color-primary-lightest: #3cad6e;

  /* Code blocks */
  --ifm-code-font-size: 95%;
  --docusaurus-highlighted-code-line-bg: rgba(0, 0, 0, 0.1);

  /* Fonts */
  --ifm-font-family-base: system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
  --ifm-font-family-monospace: 'JetBrains Mono', 'Fira Code', Consolas, Monaco, 'Courier New', monospace;
}

[data-theme='dark'] {
  --ifm-color-primary: #25c2a0;
  --ifm-color-primary-dark: #21af90;
  --ifm-color-primary-darker: #1fa588;
  --ifm-color-primary-darkest: #1a8870;
  --ifm-color-primary-light: #29d5b0;
  --ifm-color-primary-lighter: #32d8b4;
  --ifm-color-primary-lightest: #4fddbf;
  --docusaurus-highlighted-code-line-bg: rgba(0, 0, 0, 0.3);
}

/* Custom admonitions */
.admonition {
  border-radius: 8px;
}

/* Better code blocks */
pre {
  border-radius: 8px;
}

/* API method badges */
.openapi-method {
  font-weight: 600;
  border-radius: 4px;
  padding: 2px 8px;
}

.openapi-method.get {
  background-color: #61affe;
  color: white;
}

.openapi-method.post {
  background-color: #49cc90;
  color: white;
}

.openapi-method.put {
  background-color: #fca130;
  color: white;
}

.openapi-method.delete {
  background-color: #f93e3e;
  color: white;
}

/* Responsive tables */
table {
  display: block;
  overflow-x: auto;
  white-space: nowrap;
}
```

**Assets to add**:
```
docs-site/static/img/
├── logo.svg              # Auribus logo (light mode)
├── logo-dark.svg         # Auribus logo (dark mode)
├── favicon.ico
└── diagrams/
    ├── audio-upload-flow.png
    ├── voice-conversion-flow.png
    └── webhook-flow.png
```

---

### Phase 7: Create Core Documentation 📝

**Goal**: Create essential documentation pages

#### 7.1: Introduction Page

**File**: `docs-site/docs/intro.md`

```markdown
---
sidebar_position: 1
slug: /
---

# Welcome to VoiceByAuribus API

VoiceByAuribus is a powerful voice model management and audio processing API that enables you to:

- 🎤 **Manage Voice Models**: Upload and manage custom voice models
- 🎵 **Process Audio Files**: Upload and preprocess audio files for conversion
- 🔄 **Voice Conversion**: Transform audio using different voice models with pitch shifting
- 🔔 **Webhook Notifications**: Receive real-time updates on conversion status

## Quick Links

- **[Quickstart Guide](getting-started/quickstart)**: Get started in 5 minutes
- **[Authentication](getting-started/authentication)**: Learn about AWS Cognito M2M auth
- **[API Reference](api)**: Interactive API documentation
- **[Webhook Guide](guides/webhooks)**: Integrate webhook notifications

## Architecture

VoiceByAuribus API is built with:

- **.NET 10.0**: Modern, high-performance framework
- **PostgreSQL**: Robust relational database
- **AWS Services**: S3, SQS, Lambda, Cognito
- **Vertical Slice Architecture**: Clean, maintainable codebase

## Authentication

The API uses **AWS Cognito M2M (Machine-to-Machine)** authentication with scope-based authorization:

- `voice-by-auribus-api/base`: Standard user access
- `voice-by-auribus-api/admin`: Administrative access

[Learn more about authentication →](getting-started/authentication)

## Support

Need help? Contact us at [support@auribus.com](mailto:support@auribus.com)
```

#### 7.2: Quickstart Guide

**File**: `docs-site/docs/getting-started/quickstart.md`

```markdown
---
sidebar_position: 1
---

# Quickstart Guide

Get started with VoiceByAuribus API in 5 minutes.

## Prerequisites

- AWS Cognito credentials (Client ID and Client Secret)
- An HTTP client (curl, Postman, or your favorite programming language)

## Step 1: Get Access Token

Request an access token from AWS Cognito:

```bash
curl -X POST https://voicebyauribus.auth.us-east-1.amazoncognito.com/oauth2/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=YOUR_CLIENT_ID" \
  -d "client_secret=YOUR_CLIENT_SECRET" \
  -d "scope=voice-by-auribus-api/base"
```

**Response**:
```json
{
  "access_token": "eyJraWQiOiI...",
  "expires_in": 3600,
  "token_type": "Bearer"
}
```

:::tip
Tokens expire after 1 hour. Implement automatic token refresh in your application.
:::

## Step 2: Make Your First API Call

List available voice models:

```bash
curl -X GET https://api.voicebyauribus.com/api/v1/voices \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

**Response**:
```json
{
  "success": true,
  "data": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "name": "My Voice Model",
      "description": "Custom voice model",
      "created_at": "2025-01-15T10:30:00Z"
    }
  ]
}
```

## Step 3: Upload an Audio File

### 3.1: Request Pre-signed URL

```bash
curl -X POST https://api.voicebyauribus.com/api/v1/audio-files \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "file_name": "my-audio.wav",
    "content_type": "audio/wav"
  }'
```

**Response**:
```json
{
  "success": true,
  "data": {
    "id": "660e8400-e29b-41d4-a716-446655440001",
    "upload_url": "https://bucket.s3.amazonaws.com/...",
    "expires_at": "2025-01-15T11:00:00Z"
  }
}
```

### 3.2: Upload to S3

```bash
curl -X PUT "UPLOAD_URL_FROM_PREVIOUS_STEP" \
  -H "Content-Type: audio/wav" \
  --upload-file my-audio.wav
```

## Step 4: Start Voice Conversion

```bash
curl -X POST https://api.voicebyauribus.com/api/v1/voice-conversions \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "audio_file_id": "660e8400-e29b-41d4-a716-446655440001",
    "voice_model_id": "550e8400-e29b-41d4-a716-446655440000",
    "pitch_shift": "same_octave"
  }'
```

**Response**:
```json
{
  "success": true,
  "data": {
    "id": "770e8400-e29b-41d4-a716-446655440002",
    "status": "pending",
    "created_at": "2025-01-15T10:35:00Z"
  }
}
```

## Step 5: Check Conversion Status

```bash
curl -X GET https://api.voicebyauribus.com/api/v1/voice-conversions/770e8400-e29b-41d4-a716-446655440002 \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

**Response**:
```json
{
  "success": true,
  "data": {
    "id": "770e8400-e29b-41d4-a716-446655440002",
    "status": "completed",
    "output_url": "https://bucket.s3.amazonaws.com/...",
    "completed_at": "2025-01-15T10:40:00Z"
  }
}
```

:::tip
Use [webhooks](../guides/webhooks) to receive notifications when conversions complete instead of polling.
:::

## Next Steps

- [Learn about authentication](authentication)
- [Explore webhook integration](../guides/webhooks)
- [Understand pitch shifting](../guides/pitch-shifting)
- [Browse API reference](../api)

## Code Examples

### C# Example

```csharp
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

var client = new HttpClient();
client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", "YOUR_ACCESS_TOKEN");

var response = await client.GetAsync(
    "https://api.voicebyauribus.com/api/v1/voices");

var content = await response.Content.ReadAsStringAsync();
var result = JsonSerializer.Deserialize<ApiResponse>(content);
```

### Python Example

```python
import requests

headers = {
    "Authorization": "Bearer YOUR_ACCESS_TOKEN"
}

response = requests.get(
    "https://api.voicebyauribus.com/api/v1/voices",
    headers=headers
)

data = response.json()
print(data)
```

### JavaScript Example

```javascript
const response = await fetch(
  'https://api.voicebyauribus.com/api/v1/voices',
  {
    headers: {
      'Authorization': 'Bearer YOUR_ACCESS_TOKEN'
    }
  }
);

const data = await response.json();
console.log(data);
```
```

---

### Phase 8: Migration Script 🔄

**Goal**: Create script to migrate existing docs from `.ai_doc/` to `docs-site/docs/`

**File**: `docs-site/migrate-docs.sh`

```bash
#!/bin/bash

# Migration script for .ai_doc/ to docs-site/docs/

SOURCE_DIR="../.ai_doc"
DEST_DIR="./docs"

echo "Migrating documentation from $SOURCE_DIR to $DEST_DIR..."

# Architecture docs
mkdir -p "$DEST_DIR/architecture"
cp "$SOURCE_DIR/ARCHITECTURE.md" "$DEST_DIR/architecture/overview.md"

# Security docs
mkdir -p "$DEST_DIR/security"
cp "$SOURCE_DIR/COGNITO_M2M_AUTH.md" "$DEST_DIR/security/authentication.md"
cp "$SOURCE_DIR/WEBHOOK_AND_API_CONVENTIONS.md" "$DEST_DIR/security/webhook-signatures.md"

# API v1 docs
if [ -d "$SOURCE_DIR/v1" ]; then
  mkdir -p "$DEST_DIR/guides"
  for file in "$SOURCE_DIR/v1"/*.md; do
    filename=$(basename "$file")
    cp "$file" "$DEST_DIR/guides/$filename"
  done
fi

echo "Migration complete!"
echo "Note: You may need to add frontmatter to migrated files"
```

Make executable:
```bash
chmod +x docs-site/migrate-docs.sh
```

---

### Phase 9: Deployment Setup 🚀

**Goal**: Configure automatic deployment to GitHub Pages

**File**: `.github/workflows/deploy-docs.yml`

```yaml
name: Deploy Documentation

on:
  push:
    branches: [main]
    paths:
      - 'docs-site/**'
      - '.github/workflows/deploy-docs.yml'
  workflow_dispatch:

permissions:
  contents: read
  pages: write
  id-token: write

concurrency:
  group: "pages"
  cancel-in-progress: false

jobs:
  build:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: docs-site

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: 'npm'
          cache-dependency-path: docs-site/package-lock.json

      - name: Install dependencies
        run: npm ci

      - name: Build website
        run: npm run build
        env:
          NODE_ENV: production

      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: docs-site/build

  deploy:
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    runs-on: ubuntu-latest
    needs: build
    steps:
      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
```

**GitHub Pages Setup**:
1. Go to repository Settings → Pages
2. Source: GitHub Actions
3. Custom domain (optional): `docs.voicebyauribus.com`

---

### Phase 10: Advanced Features 🎯

#### 10.1: Add Search

```bash
cd docs-site
npm install --save @easyops-cn/docusaurus-search-local
```

Update `docusaurus.config.ts`:
```typescript
themes: [
  'docusaurus-theme-openapi-docs',
  [
    require.resolve("@easyops-cn/docusaurus-search-local"),
    {
      hashed: true,
      language: ["en"],
      indexBlog: false,
      docsRouteBasePath: '/docs',
    },
  ],
],
```

#### 10.2: Add Analytics (Optional)

Google Analytics in `docusaurus.config.ts`:
```typescript
themeConfig: {
  gtag: {
    trackingID: 'G-XXXXXXXXXX',
    anonymizeIP: true,
  },
}
```

#### 10.3: Version Management

For future API versions:
```bash
npm run docusaurus docs:version 1.0
```

Creates versioned docs in `versioned_docs/version-1.0/`

---

## 📋 Complete Implementation Checklist

### Phase 1: Setup
- [ ] Create Docusaurus project: `npx create-docusaurus@latest docs-site classic --typescript`
- [ ] Verify project runs: `npm start`
- [ ] Commit initial structure

### Phase 2: OpenAPI Plugin
- [ ] Install OpenAPI plugins
- [ ] Verify installation in package.json

### Phase 3: OpenAPI Spec
- [ ] Add Swashbuckle to .NET API (or create manual spec)
- [ ] Configure Swagger in Program.cs
- [ ] Generate openapi.yaml
- [ ] Place in docs-site/openapi/

### Phase 4: Configuration
- [ ] Update docusaurus.config.ts with OpenAPI config
- [ ] Configure branding (title, tagline, URLs)
- [ ] Configure navbar and footer
- [ ] Add security definitions for JWT

### Phase 5: Structure
- [ ] Create folder structure (getting-started, guides, features, etc.)
- [ ] Configure sidebars.ts
- [ ] Delete tutorial examples

### Phase 6: Branding
- [ ] Customize custom.css with Auribus colors
- [ ] Add logo.svg and logo-dark.svg
- [ ] Add favicon.ico
- [ ] Add diagram images

### Phase 7: Core Content
- [ ] Write intro.md
- [ ] Write getting-started/quickstart.md
- [ ] Write getting-started/authentication.md
- [ ] Write getting-started/environments.md
- [ ] Write getting-started/rate-limits.md

### Phase 8: Feature Guides
- [ ] Write guides/uploading-audio.md
- [ ] Write guides/voice-conversion.md
- [ ] Write guides/pitch-shifting.md
- [ ] Write guides/webhooks.md (with HMAC examples)
- [ ] Write guides/error-handling.md

### Phase 9: Feature Documentation
- [ ] Write features/voices.md
- [ ] Write features/audio-files.md
- [ ] Write features/voice-conversions.md
- [ ] Write features/webhook-subscriptions.md

### Phase 10: Architecture
- [ ] Migrate .ai_doc/ARCHITECTURE.md → architecture/overview.md
- [ ] Write architecture/vertical-slices.md
- [ ] Create architecture/data-flow.md with diagrams

### Phase 11: Security
- [ ] Migrate .ai_doc/COGNITO_M2M_AUTH.md → security/authentication.md
- [ ] Migrate .ai_doc/WEBHOOK_AND_API_CONVENTIONS.md → security/webhook-signatures.md
- [ ] Write security/best-practices.md

### Phase 12: Code Examples
- [ ] Add C# examples to quickstart
- [ ] Add Python examples to quickstart
- [ ] Add JavaScript examples to quickstart
- [ ] Add curl examples throughout
- [ ] Add webhook verification code examples

### Phase 13: Deployment
- [ ] Create .github/workflows/deploy-docs.yml
- [ ] Configure GitHub Pages in repository settings
- [ ] Test deployment
- [ ] Configure custom domain (optional)

### Phase 14: Advanced Features
- [ ] Install and configure search plugin
- [ ] Add Google Analytics (optional)
- [ ] Configure versioning for future API versions

### Phase 15: Testing & Polish
- [ ] Test all internal links
- [ ] Test all code examples
- [ ] Verify OpenAPI docs render correctly
- [ ] Test on mobile/tablet
- [ ] Test dark mode
- [ ] Proofread all content
- [ ] Get client feedback

---

## 🛠️ Common Commands Reference

```bash
# Development
cd docs-site
npm start                    # Start dev server (http://localhost:3000)
npm run build                # Production build
npm run serve                # Serve production build locally

# OpenAPI
npm run docusaurus gen-api-docs all    # Generate API docs from OpenAPI spec
npm run docusaurus clean-api-docs all  # Clean generated API docs

# Versioning
npm run docusaurus docs:version 1.0    # Create version 1.0

# Clear cache
npm run clear
npm run docusaurus clear

# Deployment
npm run deploy              # Deploy to GitHub Pages (if configured)
```

---

## 📁 File Reference

### Key Configuration Files
- `docs-site/docusaurus.config.ts` - Main configuration
- `docs-site/sidebars.ts` - Sidebar structure
- `docs-site/src/css/custom.css` - Custom styling
- `docs-site/openapi/voicebyauribus-api.yaml` - OpenAPI specification
- `.github/workflows/deploy-docs.yml` - CI/CD pipeline

### Documentation Structure
```
docs-site/docs/
├── intro.md                              # Homepage
├── getting-started/
│   ├── quickstart.md                     # 5-min quickstart
│   ├── authentication.md                 # Cognito M2M guide
│   ├── environments.md                   # API endpoints
│   └── rate-limits.md                    # Rate limiting
├── guides/
│   ├── uploading-audio.md               # Audio upload flow
│   ├── voice-conversion.md              # Conversion guide
│   ├── pitch-shifting.md                # Pitch shift options
│   ├── webhooks.md                      # Webhook integration
│   └── error-handling.md                # Error codes
├── features/
│   ├── voices.md                        # Voice models
│   ├── audio-files.md                   # Audio files
│   ├── voice-conversions.md             # Conversions
│   └── webhook-subscriptions.md         # Webhooks
├── architecture/
│   ├── overview.md                      # Architecture overview
│   ├── vertical-slices.md               # Pattern explanation
│   └── data-flow.md                     # Data flow diagrams
├── security/
│   ├── authentication.md                # Deep dive auth
│   ├── webhook-signatures.md            # HMAC verification
│   └── best-practices.md                # Security tips
└── api/                                 # Auto-generated from OpenAPI
    └── (generated files)
```

---

## 🔍 Troubleshooting

### Issue: OpenAPI docs not generating
```bash
# Clear cache and regenerate
npm run docusaurus clean-api-docs all
npm run docusaurus gen-api-docs all
npm run clear
npm start
```

### Issue: Broken links
```bash
# Build will fail on broken links
npm run build
# Check output for broken link errors
```

### Issue: Styling not applying
```bash
# Clear cache
npm run clear
# Restart dev server
npm start
```

### Issue: Deployment fails
- Check GitHub Pages is enabled in repository settings
- Verify workflow has proper permissions
- Check build logs in Actions tab

---

## 🎯 Success Criteria

When complete, the documentation site should:

✅ **Be accessible online** via GitHub Pages or Vercel
✅ **Have interactive API docs** generated from OpenAPI spec
✅ **Include comprehensive guides** for all major features
✅ **Provide code examples** in multiple languages (C#, Python, JS, curl)
✅ **Support dark mode** automatically
✅ **Have working search** functionality
✅ **Be mobile-responsive**
✅ **Auto-deploy** on push to main
✅ **Load quickly** (< 2 seconds)
✅ **Be easy to maintain** (just edit Markdown files)

---

## 📚 Resources

- [Docusaurus Documentation](https://docusaurus.io/docs)
- [OpenAPI Plugin Docs](https://github.com/PaloAltoNetworks/docusaurus-openapi-docs)
- [Markdown Guide](https://www.markdownguide.org/)
- [OpenAPI Specification](https://swagger.io/specification/)

---

## 🔄 Maintenance

### Adding New Features
1. Update OpenAPI spec with new endpoints
2. Regenerate API docs: `npm run docusaurus gen-api-docs all`
3. Create guide in `docs/guides/` if needed
4. Update intro.md with link to new feature
5. Commit and push (auto-deploys)

### Updating Existing Content
1. Edit Markdown file in `docs/`
2. Preview locally: `npm start`
3. Commit and push (auto-deploys)

### Creating New Versions
When releasing API v2:
```bash
npm run docusaurus docs:version 1.0
# Update openapi spec for v2
# Continue editing current docs for v2
```

---

**Status**: 🚧 In Progress
**Started**: 2025-11-25
**Expected Completion**: TBD

---

## Notes

- This document should be updated as implementation progresses
- Mark checklist items as they are completed
- Document any deviations from the plan
- Add troubleshooting tips as issues are encountered
