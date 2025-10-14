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
- Feature branch committed: `0850c14` on `feature/certificate-tags`
- Successfully built and deployed to Azure Functions
- Tested and verified working in production
- Ready to merge upstream updates as needed

## Session Summary (October 8, 2025)
- ✅ Cloned repository from https://github.com/projecthosts/keyvault-acmebot.git
- ✅ Initialized ACMESharpCore submodule
- ✅ Migrated all 5 file changes from old ZIP extraction
- ✅ Build verified with ACMESharpCore project reference (no DLL workaround needed)
- ✅ Deployment created and successfully deployed to Azure
- ✅ Fixed deployment issue (nested ZIP directory structure)
- ✅ Documentation updated with deployment troubleshooting
- ✅ Git commit created with proper attribution
- ✅ Feature branch `feature/certificate-tags` ready for push
- ⏳ **Pending: Push to remote repository** - awaiting organization to grant push permissions to user JakeMF on projecthosts/keyvault-acmebot repository (403 permission error)

## Upstream Merge Risk Assessment

### High Risk Files (Most Likely to Conflict):

**1. KeyVault.Acmebot/Functions/SharedActivity.cs (line 391)**
- Contains CRITICAL fix to `StartCreateCertificateAsync()` method call
- Changed to use named parameter `tags:` and added `cancellationToken: default`
- If upstream modifies certificate creation logic, conflicts are likely
- This is the most critical change that fixes InternalServerError bug
- **Action on conflict**: Preserve our named parameter approach to avoid method overload ambiguity

**2. KeyVault.Acmebot/wwwroot/dashboard/index.html**
- Extensive UI modifications across multiple sections:
  - Add Certificate Modal (lines ~322-352): Tag input fields and UI
  - Certificate Details Modal (lines ~503-516): Tag display section
  - JavaScript data() (lines ~554-556): Tag-related data properties
  - JavaScript methods (lines ~644-653, 671-704): Tag management functions
- Highest conflict risk due to frequent upstream UI framework updates
- **Action on conflict**: Carefully merge to preserve tag functionality while adopting upstream improvements

### Medium Risk Files:

**3. KeyVault.Acmebot/Internal/CertificateExtensions.cs**
- Modified `ToCertificateItem()` (lines 27-49): Filters reserved system tags
- Modified `ToCertificateMetadata()` (lines 83-96): Merges custom tags, protects reserved tags
- Could conflict if upstream refactors certificate metadata handling
- **Action on conflict**: Preserve tag filtering and reserved tag protection logic

### Low Risk Files:

**4. KeyVault.Acmebot/Models/CertificatePolicyItem.cs (lines 37-38)**
**5. KeyVault.Acmebot/Models/CertificateItem.cs (lines 58-59)**
- Simple `Tags` property additions
- Low conflict probability unless upstream adds identical/similar properties
- **Action on conflict**: Easy to resolve, keep our Tags property

### Other Considerations:

**ACMESharpCore Submodule**:
- If upstream updates submodule pointer to different commit, review compatibility
- Check if ACMESharp API changes affect our certificate creation code
- **Action**: Test build after accepting upstream submodule update

**Merge Strategy Recommendation**:
1. Before merging upstream: Create backup branch of current feature/certificate-tags
2. Test build after merge to catch breaking changes
3. Focus testing on certificate creation with tags (SharedActivity.cs line 391)
4. Verify UI tag functionality still works after index.html merge

## Next Steps
1. **Push Feature Branch**: Once push permissions are granted by organization owner, run:
   ```bash
   git push -u origin feature/certificate-tags
   ```
2. **Create Pull Request**: After push succeeds, create PR to merge feature/certificate-tags into main branch
3. **Review and Testing**: Complete code review and testing before merging to production

---

# Application Gateway Integration Feature (Branch: feature/appgw-integration)

## Context
Built on top of the `feature/certificate-tags` branch to provide a streamlined workflow for Application Gateway certificate deployments that require specific Azure infrastructure tags.

## Branch Dependency
- **Base Branch:** `feature/certificate-tags` (must be merged first)
- **Feature Branch:** `feature/appgw-integration`
- **Purpose:** Company-specific enhancement for Application Gateway certificate management

## Use Case
Internal employees deploying certificates to Application Gateways need to specify 3 mandatory Azure infrastructure tags:
- **EntraID**: Azure Entra ID identifier
- **SubscriptionID**: Azure subscription identifier
- **KeyVaultName**: Name of the Key Vault

This feature provides a dedicated UI mode that enforces these required fields and automatically tags certificates appropriately.

## Changes Made

### Frontend (JavaScript/Vue.js)

#### wwwroot/dashboard/index.html

**Application Gateway Mode Toggle (lines ~203-221):**
- Radio button section: "Application Gateway Integration?" (Yes/No)
- Positioned on main page, before "Use Advanced Options?" section
- Binding: `add.useAppGatewayMode` (boolean, default: false)

**Required AppGW Tag Fields (lines ~222-257):**
- Three conditionally-visible input fields (shown when AppGW mode = true):
  - **EntraID*** - Text input for Azure Entra ID
  - **SubscriptionID*** - Text input for Subscription ID
  - **KeyVaultName*** - Text input for Key Vault name
- All fields marked as required with asterisk (*)

**JavaScript Data Model (lines ~604-606):**
Added to `add` object:
```javascript
useAppGatewayMode: false,
appGwEntraId: "",
appGwSubscriptionId: "",
appGwKeyVaultName: "",
```

**JavaScript Validation (lines ~715-720):**
Client-side validation in `addCertificate()` method:
- Checks all 3 AppGW fields are non-empty when AppGW mode is enabled
- Displays alert with list of required fields if validation fails
- Prevents form submission until all fields are filled

**Tag Building Logic (lines ~740-746):**
Merge logic in `addCertificate()` method:
```javascript
if (this.add.useAppGatewayMode) {
  postData.tags = {
    EntraID: this.add.appGwEntraId,
    SubscriptionID: this.add.appGwSubscriptionId,
    KeyVaultName: this.add.appGwKeyVaultName,
    ...this.add.tags  // Merge any additional custom tags
  };
}
```
- AppGW tags are always included when mode is enabled
- Additional custom tags (from Advanced Options) are merged in
- Generic tags feature remains fully functional alongside AppGW mode

**Field Reset Logic (lines ~779-781):**
Reset in `openAdd()` method:
- Resets `useAppGatewayMode` to false
- Clears all 3 AppGW tag input fields when modal opens

## Data Flow

### Certificate Creation with AppGW Mode:
1. User selects "Yes" for Application Gateway Integration
2. UI displays 3 required tag input fields
3. User fills in EntraID, SubscriptionID, KeyVaultName
4. User can optionally enable Advanced Options to add additional custom tags
5. On submit, JavaScript validates all 3 AppGW fields are non-empty
6. If valid, builds tags object with 3 required AppGW tags + any additional tags
7. POST to `/api/certificate` with tags in request body
8. Backend stores all tags in Key Vault (leverages existing tags feature)
9. Certificate created with proper Azure infrastructure metadata

### Certificate Retrieval:
- AppGW tags are stored as standard Key Vault certificate tags
- Retrieved and displayed like any other custom tags
- No special handling needed on backend (leverages existing infrastructure)

## Design Decisions

### Client-Side Only Implementation
- **No server-side validation**: Acceptable for internal-only application with trusted users
- **No C# model changes**: AppGW tags are just regular custom tags on the backend
- **Rationale**:
  - Simplifies implementation and maintenance
  - Faster development cycle
  - Azure Key Vault provides final validation layer
  - Worst case: incomplete submission fails at Azure level with clear error

### UI Design
- **Radio button placement**: Above Advanced Options to emphasize it's a primary workflow choice
- **Conditional visibility**: Fields only show when AppGW mode enabled to reduce clutter
- **Custom tags remain visible**: Users can add extra tags beyond the 4 required ones
- **Clear required indicator**: Asterisk (*) on all required field labels

### Tag Merging Strategy
- AppGW required tags come first in the object
- Additional custom tags merged after using spread operator
- This ensures AppGW tags take precedence if there's a naming conflict (unlikely)

## Testing Considerations
- ✅ Test AppGW mode ON: All 3 fields required
- ✅ Test AppGW mode OFF: Generic tags functionality unchanged
- ✅ Test validation: Empty fields trigger alert
- ✅ Test tag merging: AppGW tags + custom tags both included
- ✅ Test modal reset: Fields clear when reopening modal
- Test certificate creation with AppGW tags
- Test certificate retrieval displays AppGW tags correctly

## Deployment Notes
- This feature requires `feature/certificate-tags` to be deployed first
- No backend changes means no rebuild/redeploy needed for AppGW feature
- UI changes deployed via same process (included in wwwroot/ folder)
- Compatible with existing Azure Function deployment

## Branch Strategy
1. `feature/certificate-tags` contains base custom tags functionality
2. `feature/appgw-integration` builds on top with company-specific UI
3. Both can be merged independently:
   - Merge `feature/certificate-tags` first (base functionality)
   - Then merge `feature/appgw-integration` (optional company-specific enhancement)

## Future Enhancements (Optional)
- Add dropdown for common resource groups/key vaults
- Pre-populate fields based on selected DNS zone
- Server-side validation if application becomes externally exposed
- Save AppGW tag presets for faster form completion
