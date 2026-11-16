# Unity Animation Assets - Battle Horizons

## Supported Formats

### Primary Formats (Git LFS Recommended):
- `.glb` - GLB binary format (recommended for Unity)
- `.gltf` - GLTF text format
- `.fbx` - FBX format
- `.obj` - OBJ format
- `.bin` - Binary buffer (GLTF companion)
- `.mtl` - Material library (OBJ companion)

### Animation-Specific:
- `.anim` - Unity animation clips
- `.controller` - Animator controllers

## Recommended Folder Structure

```
Assets/UserAssets/Animations/
├── Common/           ← Shared animations for Male/Female
│   ├── Idle.glb
│   ├── Walk.glb
│   ├── Run.glb
│   ├── Jump.glb
│   ├── Crouch.glb
│   └── ...
├── Male/             ← Male-specific animations (optional)
│   ├── Idle.glb
│   └── ...
└── Female/           ← Female-specific animations (optional)
    ├── Idle.glb
    └── ...
```

## GitHub Browser Upload Limits

### Free Account Limits:
- Maximum file size: **25 MB** per file via browser
- Repository size limit: **1 GB** (including LFS)
- LFS storage limit: **1 GB free** (varies by plan)

### Recommended Workflow:
| File Size | Upload Method |
|-----------|---------------|
| < 10 MB   | GitHub Browser ✅ |
| 10-25 MB  | GitHub Browser (may work) |
| > 25 MB   | Git LFS CLI 🔧 |

## Git LFS Upload Instructions

### For Android/ Mobile Browsers:

**Option 1: GitHub Web Interface (Small Files < 10MB)**
1. Navigate to `unity-project/Assets/UserAssets/Animations/`
2. Click "Add file" → "Upload files"
3. Select your `.glb`/`.fbx`/`.gltf` files
4. Commit changes with descriptive message

**Option 2: Git LFS Command Line (Recommended for Large Files)**
```bash
# Install Git LFS
git lfs install

# Track animation file types
git lfs track "*.glb" "*.gltf" "*.fbx" "*.obj" "*.bin" "*.mtl"

# Add files to LFS
git lfs untrack "*.glb" "*.gltf" "*.fbx" "*.obj" "*.bin" "*.mtl"
# Then track with proper size limits
git lfs track --size=10M "*.glb" "*.fbx"

# Add and commit
git add unity-project/Assets/UserAssets/Animations/your-animation.glb
git commit -m "Add animation: your-animation"
git push origin arena/baa666d3-battle-royal-indian-edition
```

**Option 3: GitHub CLI (cross-platform)**
```bash
# Install: brew install gh (Mac) or winget install GitHub.cli (Windows)
git lfs track "*.glb" "*.fbx"
gh repo view --web
# Then upload via gh cli
```

## Unity Humanoid Import Settings

### For `.glb` and `.fbx` Files:

1. **Rig Tab:**
   - Animation Type: `Humanoid`
   - Avatar: `Create From This Model`
   - Optimize Game Objects: `Enabled`

2. **Animation Tab:**
   - Generated Clips: All detected animations
   - Loop Time: Check if looping needed
   - Root Node Position: Based on needs

3. **Model Tab:**
   - Scale: `1` (meters)
   - Forward: `Z Up` or `Y Up` based on source
   - Up: `Y Up` or `Z Up`

4. **Humanoid Retargeting:**
   - Unity auto-generates Avatar
   - Works for both Male and Female characters
   - Same animation clips can be shared
   - Height/proportions auto-adjusted

### Example Import Settings for Shared Animations:

```yaml
# Recommended setup for shared Male/Female animations:
- Rig:
  * Animation Type: Humanoid
  * Avatar: Auto-generated
- Animation clips work on both Male/Female characters
- Unity scales based on Avatar height
- No manual bone mapping needed for standard motions
```

## Male/Female Character Animation Sharing

### How Unity Handles Both:

```
Same Animation Clips → Both Male & Female Characters

Process:
1. Import male .glb → Humanoid rig → Auto Avatar
2. Import female .glb → Humanoid rig → Auto Avatar  
3. Both use SAME animation clips
4. Unity retargets automatically:
   - Height differences scaled
   - Proportions adjusted
   - Bone mappings matched
```

### Shared Animation Clips Work Because:

| Feature | Male | Female | Unity Handles |
|---------|------|--------|---------------|
| **Walking** | ✅ | ✅ | Auto-height scale |
| **Running** | ✅ | ✅ | Proportion adjust |
| **Jumping** | ✅ | ✅ | Bone mapping |
| **Crouching** | ✅ | ✅ | Height offset |
| **Idle** | ✅ | ✅ | No retargeting needed |
```

## Android-Friendly Upload Procedure

### Step 1: Prepare Files
```bash
# Ensure files are optimized
# - Remove unnecessary metadata
# - Keep file size reasonable (< 25MB browser, < 10MB ideal)
# - Test in Unity first
```

### Step 2: Choose Upload Method

**For files < 10MB (Browser):**
1. Open GitHub in Android browser
2. Navigate to animations folder
3. Tap "Add file" → "Upload files"
4. Select files, add commit message
5. Tap "Commit changes"

**For files > 10MB (Git LFS):**
```bash
# Use GitHub app or connect via SSH
# Or use mobile Git LFS client

# Option A: GitHub Android App
1. Install GitHub app on Android
2. Open repository
3. Navigate to folder
4. Upload via app (handles LFS)

# Option B: Termux + Git LFS
1. Install Termux from F-Droid/Play Store
2: pkg install git-lfs
3. git lfs install
4. git lfs track "*.glb"
5. git add .gitattributes
6. git commit and push
```

**Option C: Desktop then Push**
1. Download files to computer
2. Use git commands with LFS
3. Push to repository
4. Files sync to Android via GitHub app

## File Size Guidelines

| Usage | Recommended Max Size | Browser Friendly | LFS Required |
|-------|---------------------|------------------|--------------|
| **Idle/Loop animations** | 5 MB | ✅ Yes | ❌ No |
| **Walk/Run cycles** | 8 MB | ✅ Yes | ❌ No |
| **Full body actions** | 15 MB | ⚠️ May work | ✅ Yes |
| **High-res GLB models** | 50 MB | ❌ No | ✅ Yes |
| **Reference animations** | 2 MB | ✅ Yes | ❌ No |

## Workflow Summary

### For New Animations:

1. **Create/Export animation** in supported format (.glb recommended)
2. **Test in Unity** with Humanoid rig
3. **Verify** works on both Male & Female characters
4. **Check file size**:
   - < 10MB → Upload via GitHub browser
   - > 10MB → Use Git LFS CLI or GitHub app
5. **Commit** with descriptive message
6. **Validate** with project checks

### Browser Upload Checklist:
- [ ] File size < 25MB (ideal < 10MB)
- [ ] File extension supported (.glb, .fbx, .gltf, .obj)
- [ ] Unity Humanoid import verified
- [ ] Works on both Male/Female characters
- [ ] Commit message describes animation
- [ ] Push to correct branch