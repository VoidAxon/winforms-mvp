# Deploying Wiki to GitHub

This guide explains how to deploy the wiki content to your GitHub repository.

## Prerequisites

- GitHub repository with wiki feature enabled
- Git command line installed
- Write access to the repository

## Step 1: Enable GitHub Wiki

1. Go to your GitHub repository: https://github.com/VoidAxon/winforms-mvp
2. Click on **Settings** tab
3. Scroll down to **Features** section
4. Check the **Wikis** checkbox
5. Click **Save**

## Step 2: Clone the Wiki Repository

The GitHub Wiki is actually a separate Git repository. Clone it:

```bash
# Navigate to a temporary directory (not inside your main repo)
cd /tmp

# Clone the wiki repository
git clone https://github.com/VoidAxon/winforms-mvp.wiki.git

cd winforms-mvp.wiki
```

## Step 3: Copy Wiki Content

Copy the markdown files from your main repository to the wiki repository:

```bash
# Assuming you're in the wiki directory
cp /c/workspace/winforms-mvp/wiki/*.md .

# Verify files were copied
ls -la
```

You should see:
- `Home.md`
- `Example-Master-Detail.md`
- `Example-Validation.md`
- `Example-Async-Operations.md`

## Step 4: Commit and Push

```bash
# Add all markdown files
git add *.md

# Commit
git commit -m "docs: Add comprehensive wiki pages

- Add Home page with navigation
- Add Master-Detail Pattern example
- Add Complex Validation example
- Add Async Operations example"

# Push to GitHub
git push origin master
```

## Step 5: Verify

1. Go to https://github.com/VoidAxon/winforms-mvp/wiki
2. You should see the Home page with all navigation links
3. Click on example links to verify all pages are there

## Alternative: Manual Upload

If you prefer, you can create wiki pages manually through GitHub's web interface:

1. Go to https://github.com/VoidAxon/winforms-mvp/wiki
2. Click **New Page**
3. Copy content from `wiki/Home.md` and paste
4. Set page title to "Home"
5. Click **Save Page**
6. Repeat for other markdown files

## Updating Wiki Later

To update existing wiki pages:

```bash
cd /tmp/winforms-mvp.wiki

# Copy updated files
cp /c/workspace/winforms-mvp/wiki/*.md .

# Commit and push
git add *.md
git commit -m "docs: Update wiki pages"
git push origin master
```

## File Name Mapping

GitHub Wiki uses file names for page URLs:

| File Name | Wiki URL | Page Title |
|-----------|----------|------------|
| `Home.md` | `/wiki` | Home (main page) |
| `Example-Master-Detail.md` | `/wiki/Example-Master-Detail` | Master-Detail Pattern Example |
| `Example-Validation.md` | `/wiki/Example-Validation` | Complex Validation Example |
| `Example-Async-Operations.md` | `/wiki/Example-Async-Operations` | Async Operations Example |

## Troubleshooting

**Problem:** `git clone` fails with "repository not found"

**Solution:** Make sure you've enabled the Wiki feature in GitHub Settings first.

---

**Problem:** Wiki pages show broken internal links

**Solution:** GitHub Wiki uses the format `[Link Text](Page-Name)` without `.md` extension. Our pages already use this format.

---

**Problem:** Images don't appear

**Solution:** Upload images through GitHub's web interface, then reference them as:
```markdown
![Alt Text](image-name.png)
```

## Adding More Wiki Pages

To add new wiki pages:

1. Create a new `.md` file in `wiki/` directory
2. Add link to `Home.md` navigation
3. Follow the deployment steps above
4. Naming convention: Use `PascalCase-With-Hyphens.md`

Example:
```markdown
<!-- In your new wiki/Advanced-Topics.md -->
# Advanced Topics

Content here...
```

Then add to `Home.md`:
```markdown
- [Advanced Topics](Advanced-Topics)
```

---

**Done!** Your wiki is now live at: https://github.com/VoidAxon/winforms-mvp/wiki
