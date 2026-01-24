# Scripts

## pre-commit-check.sh

Pre-commit hook script that checks for common build errors before committing.

### Installation

```bash
# Install as git hook (recommended)
make install-hooks

# Or manually:
chmod +x scripts/pre-commit-check.sh
mkdir -p .git/hooks
echo '#!/bin/sh' > .git/hooks/pre-commit
echo 'exec bash scripts/pre-commit-check.sh' >> .git/hooks/pre-commit
chmod +x .git/hooks/pre-commit
```

### Manual Usage

```bash
# Run before committing
./scripts/pre-commit-check.sh
```

### What It Checks

- ✅ Trailing whitespace in staged files
- ✅ Build succeeds (`dotnet build --configuration Release`)

### Exit Codes

- `0` - All checks passed
- `1` - Checks failed (fix errors before committing)

---

## remove-plugin-from-jellyfin.sh

Script to completely remove the Jellyfin.Xtream plugin from a Jellyfin Docker container.

### Setup

**⚠️ IMPORTANT: This script contains private information (IP addresses, passwords) and is NOT tracked in git.**

1. Copy the example file:
   ```bash
   cp scripts/remove-plugin-from-jellyfin.sh.example scripts/remove-plugin-from-jellyfin.sh
   ```

2. Edit the script and configure:
   - `JELLYFIN_HOST`: Your Jellyfin server IP/hostname
   - `SSH_PASSWORD`: Your SSH password (or use SSH keys instead)

3. Make it executable:
   ```bash
   chmod +x scripts/remove-plugin-from-jellyfin.sh
   ```

### Usage

```bash
# With default container name (jellyfin) and configured host
./scripts/remove-plugin-from-jellyfin.sh

# With custom container name
./scripts/remove-plugin-from-jellyfin.sh my-jellyfin-container

# With custom container name and host
./scripts/remove-plugin-from-jellyfin.sh my-jellyfin-container 192.168.1.100
```

### What It Does

- Finds and removes plugin DLL files
- Removes plugin configuration files
- Removes plugin cache files
- Restarts the Jellyfin container

### Security Note

The actual script (`remove-plugin-from-jellyfin.sh`) is in `.gitignore` to prevent committing private information. Only the example template (`remove-plugin-from-jellyfin.sh.example`) is tracked in git.

---

## Makefile Targets

See root `Makefile` for available targets:

- `make check` - Run all checks (fix whitespace + build)
- `make fix-whitespace` - Remove trailing whitespace
- `make build` - Build the project
- `make install-hooks` - Install pre-commit hook
