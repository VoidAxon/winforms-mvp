#!/bin/bash
# Bash script to deploy wiki content to GitHub
# Usage: ./deploy-wiki.sh

set -e  # Exit on error

# Colors
CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${CYAN}============================================${NC}"
echo -e "${CYAN}  WinForms MVP Framework - Wiki Deployer${NC}"
echo -e "${CYAN}============================================${NC}"
echo ""

# Configuration
WIKI_REPO_URL="https://github.com/VoidAxon/winforms-mvp.wiki.git"
TEMP_DIR="/tmp/winforms-mvp-wiki-deploy"
WIKI_SOURCE_DIR="$(cd "$(dirname "$0")" && pwd)"

# Step 1: Clean up any previous deployment
if [ -d "$TEMP_DIR" ]; then
    echo -e "${YELLOW}[1/5] Cleaning up previous deployment...${NC}"
    rm -rf "$TEMP_DIR"
else
    echo -e "${GREEN}[1/5] No previous deployment found${NC}"
fi

# Step 2: Clone wiki repository
echo -e "${YELLOW}[2/5] Cloning wiki repository...${NC}"
if git clone "$WIKI_REPO_URL" "$TEMP_DIR"; then
    echo -e "${GREEN}    Wiki repository cloned successfully${NC}"
else
    echo -e "${RED}    ERROR: Failed to clone wiki repository${NC}"
    echo -e "${RED}    Make sure Wiki is enabled in GitHub Settings${NC}"
    echo -e "${YELLOW}    Go to: https://github.com/VoidAxon/winforms-mvp/settings${NC}"
    exit 1
fi

# Step 3: Copy wiki content
echo -e "${YELLOW}[3/5] Copying wiki content...${NC}"
WIKI_FILES=$(find "$WIKI_SOURCE_DIR" -maxdepth 1 -name "*.md" ! -name "DEPLOY.md" ! -name "README.md")

if [ -z "$WIKI_FILES" ]; then
    echo -e "${RED}    ERROR: No wiki markdown files found${NC}"
    exit 1
fi

for file in $WIKI_FILES; do
    filename=$(basename "$file")
    cp "$file" "$TEMP_DIR/"
    echo -e "${GREEN}    Copied: $filename${NC}"
done

# Step 4: Commit changes
echo -e "${YELLOW}[4/5] Committing changes...${NC}"
cd "$TEMP_DIR"

git add *.md
COMMIT_MESSAGE="docs: Update wiki pages - $(date '+%Y-%m-%d %H:%M')"

if git commit -m "$COMMIT_MESSAGE"; then
    echo -e "${GREEN}    Changes committed successfully${NC}"
else
    echo -e "${YELLOW}    No changes to commit (wiki is already up to date)${NC}"
fi

# Step 5: Push to GitHub
echo -e "${YELLOW}[5/5] Pushing to GitHub...${NC}"
if git push origin master; then
    echo -e "${GREEN}    Successfully pushed to GitHub!${NC}"
else
    echo -e "${RED}    ERROR: Failed to push to GitHub${NC}"
    echo -e "${YELLOW}    You may need to authenticate with GitHub${NC}"
    exit 1
fi

# Cleanup
echo ""
echo -e "${YELLOW}Cleaning up temporary files...${NC}"
rm -rf "$TEMP_DIR"

echo ""
echo -e "${CYAN}============================================${NC}"
echo -e "${GREEN}  Deployment Complete!${NC}"
echo -e "${CYAN}============================================${NC}"
echo ""
echo -e "${YELLOW}View your wiki at: https://github.com/VoidAxon/winforms-mvp/wiki${NC}"
echo ""

# Open wiki in browser (works on most systems)
read -p "Open wiki in browser? (Y/N): " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    if command -v xdg-open > /dev/null; then
        xdg-open "https://github.com/VoidAxon/winforms-mvp/wiki"
    elif command -v open > /dev/null; then
        open "https://github.com/VoidAxon/winforms-mvp/wiki"
    else
        echo "Please open https://github.com/VoidAxon/winforms-mvp/wiki manually"
    fi
fi
