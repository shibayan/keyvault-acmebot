# Custom Tags Feature Implementation

## Context
Implemented custom tags feature for keyvault-acmebot project as requested in GitHub issue #387. This allows users to add custom metadata (Key Vault certificate tags) to certificates during creation.

## Repository
- **Git Repository:** https://github.com/projecthosts/keyvault-acmebot
- **Branch:** `feature/certificate-tags`
- **Base Repository:** Forked from https://github.com/shibayan/keyvault-acmebot

## Use Case
Users wanted ability to add custom tags like "Customer: Kraft", "Stage: production" to organize and track certificates with flexible metadata.

## Changes Made

### Backend (C#)

#### 1. Models/CertificatePolicyItem.cs
- Added `Tags` property (IDictionary<string, string>) at line 37-38
- Accepts custom tags from API POST requests during certificate creation

#### 2. Models/CertificateItem.cs
- Added `Tags` property (IDictionary<string, string>) at line 58-59
- Returns custom tags when certificates are retrieved via API

#### 3. Internal/CertificateExtensions.cs

**ToCertificateItem() method (lines 27-49):**
- Extracts tags from Key Vault certificate properties
- Filters OUT reserved system tags: Issuer, Endpoint, DnsProvider, DnsAlias
- Returns only custom user-defined tags in CertificateItem.Tags

**ToCertificateMetadata() method (lines 83-96):**
- Takes custom tags from CertificatePolicyItem.Tags
- Merges them into certificate metadata dictionary
- PREVENTS overwriting reserved system tags
- Tags are stored in Key Vault when certificate is created

#### 4. Functions/SharedActivity.cs
**CRITICAL FIX at line 391:**
- Changed to: `await _certificateClient.StartCreateCertificateAsync(certificatePolicyItem.CertificateName, certificatePolicy, enabled: true, tags: metadata, cancellationToken: default)`
- **Issue**: The parameter name must be `tags:` not a positional argument, and `cancellationToken` is required to avoid method overload ambiguity
- This was causing InternalServerError during deployment

### Frontend (JavaScript/Vue.js)

#### 5. wwwroot/dashboard/index.html

**Add Certificate Modal (lines ~322-352):**
- Input fields for tag key and tag value
- "Add Tag" button
- Display area showing added tags as info badges with delete buttons
- Only visible when "Use Advanced Options" is enabled

**Certificate Details Modal (lines ~503-516):**
- Display section for custom tags
- Shows tags as info badges (key: value format)
- Only displays if tags exist on certificate

**JavaScript data() (lines ~554-556):**
- `add.tags: {}` - stores tags being added
- `add.tagKey: ""` - current tag key input
- `add.tagValue: ""` - current tag value input

**JavaScript methods:**
- `addTag()` (lines ~644-649): Adds tag to add.tags object, clears inputs
- `removeTag(key)` (lines ~651-653): Deletes tag from add.tags object
- `addCertificate()` (lines ~671-673): Includes tags in POST request if any exist
- `openAdd()` (lines ~702-704): Resets tags when opening modal

## Data Flow

### Certificate Creation:
1. User enters tags in UI (key-value pairs)
2. JavaScript adds to `add.tags` object
3. `addCertificate()` includes tags in POST to `/api/certificate`
4. `AddCertificate.HttpStart()` receives CertificatePolicyItem with Tags
5. `SharedActivity.FinalizeOrder()` calls `ToCertificateMetadata()` which merges custom tags
6. Tags stored in Key Vault via `StartCreateCertificateAsync(certificateName, policy, enabled: true, tags: metadata, cancellationToken: default)`

### Certificate Retrieval:
1. `SharedActivity.GetAllCertificates()` retrieves certificates from Key Vault
2. `ToCertificateItem()` extracts custom tags (excludes reserved system tags)
3. CertificateItem.Tags sent to frontend via API
4. Vue.js displays tags in Details modal

## Reserved System Tags (Protected)
These tags are used internally and cannot be overwritten by users:
- `Issuer` - Always "Acmebot"
- `Endpoint` - ACME endpoint host
- `DnsProvider` - DNS provider name
- `DnsAlias` - DNS alias if configured

## Build and Deployment Process

### Building for Deployment:
1. Ensure git submodules are initialized: `git submodule update --init --recursive`
2. Run from `KeyVault.Acmebot/` directory:
   ```bash
   dotnet publish -c Release -o ../keyvault-acmebot-deployment
   ```
3. This creates proper Azure Functions deployment structure with:
   - Individual function folders with function.json files
   - bin/ folder with all compiled DLLs (including ACMESharp.dll from submodule)
   - host.json
   - wwwroot/ folder

**IMPORTANT**: When creating the ZIP file, ensure the function folders and host.json are at the **ROOT** of the ZIP, not nested in a subdirectory.
- ✅ Correct: `deployment.zip` contains `host.json`, `bin/`, function folders at root
- ❌ Incorrect: `deployment.zip` contains `deployment/host.json`, `deployment/bin/`, etc.

### Deployment ZIP Structure:
```
keyvault-acmebot-deploy.zip
├── AddCertificate_HttpStart/
├── AnswerChallenges/
├── bin/
│   ├── ACMESharp.dll
│   ├── KeyVault.Acmebot.dll
│   └── [other dependencies]
├── [other function folders]
├── host.json
└── wwwroot/
```

### Azure Function Deployment:
1. Upload ZIP to Azure Blob Storage (public read access or with SAS token)
2. Set `WEBSITE_RUN_FROM_PACKAGE` app setting to blob URL
3. If Azure caches old package, force refresh by:
   - Stop Function App
   - Clear package cache in Kudu: `rm -rf /home/data/SitePackages/*`
   - Start Function App
   - OR add query parameter to URL like `?v=2`

### Common Deployment Issues:
- **"No functions detected" in Azure Portal**: ZIP structure has nested directories
  - Ensure function folders are at ZIP root, not nested
  - Re-create ZIP with correct structure
- **"FAILED TO INITIALIZE RUN FROM PACKAGE"**: Azure can't download/extract ZIP
  - Check blob is accessible (anonymous or valid SAS token)
  - Clear Azure cache and restart
  - Verify ZIP structure matches official release
- **Azure shows old version after deployment**: Package cache not cleared
  - Go to Function App → Advanced Tools → Go (Kudu console)
  - Debug console → CMD/PowerShell
  - Run: `rm -rf /home/data/SitePackages/*`
  - Restart Function App in Azure Portal
- **Method overload ambiguity**: Ensure all parameters are correctly named in method calls

## Git Workflow

### Initial Setup (Already Complete):
```bash
git clone https://github.com/projecthosts/keyvault-acmebot.git
cd keyvault-acmebot
git submodule update --init --recursive
git checkout -b feature/certificate-tags
# Apply changes and commit
```

### Merging Upstream Updates:
```bash
# Add upstream remote (if not already added)
git remote add upstream https://github.com/shibayan/keyvault-acmebot.git

# Fetch latest changes from upstream
git fetch upstream

# Merge upstream changes into feature branch
git checkout feature/certificate-tags
git merge upstream/main

# Resolve any conflicts in the 5 modified files:
# - KeyVault.Acmebot/Models/CertificatePolicyItem.cs
# - KeyVault.Acmebot/Models/CertificateItem.cs
# - KeyVault.Acmebot/Internal/CertificateExtensions.cs
# - KeyVault.Acmebot/Functions/SharedActivity.cs
# - KeyVault.Acmebot/wwwroot/dashboard/index.html

# Test build
dotnet build

# Commit merge
git commit -m "Merge upstream changes"
```

## Testing Considerations
- ✅ Verify tags are stored in Key Vault
- ✅ Verify tags are retrieved and displayed
- ✅ Verify reserved tags cannot be overwritten
- Test empty tags (should work fine, just won't display)
- Test special characters in tag keys/values
- Test tag deletion in UI before submission

## File Locations
- Backend Models: `KeyVault.Acmebot/Models/`
- Backend Logic: `KeyVault.Acmebot/Internal/`, `KeyVault.Acmebot/Functions/`
- Frontend: `KeyVault.Acmebot/wwwroot/dashboard/index.html`
- Project Config: `KeyVault.Acmebot/KeyVault.Acmebot.csproj`
- ACMESharpCore Submodule: `ACMESharpCore/` (managed by git)

## Status
✅ **Successfully implemented and migrated to git repository** (October 8, 2025)
- Changes migrated from ZIP extraction to proper git clone
- ACMESharpCore submodule initialized and working with project reference
- Feature branch ready for testing and deployment
- Ready to merge upstream updates as needed
