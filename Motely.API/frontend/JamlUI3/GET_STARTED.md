# Getting Started with JamlUI3

## First Time Setup (3 steps):

### 1. Install Dependencies
```bash
cd Motely.API/wwwroot/JamlUI3
npm install
```

This will download Vue, Vite, and all other packages (~5 minutes first time)

### 2. Start Dev Server
```bash
npm run dev
```

You should see:
```
  VITE v5.x.x  ready in xxx ms

  ➜  Local:   http://localhost:5173/
  ➜  Network: use --host to expose
```

### 3. Open Browser
Go to: **http://localhost:5173**

---

## If You See a Blank Page:

1. **Open Browser Console** (F12)
2. **Check for errors** - usually red text
3. **Common fixes:**
   - Make sure you ran `npm install` first
   - Make sure you're running `npm run dev` (not opening HTML directly)
   - Try clearing browser cache (Ctrl+Shift+R)

## What You Should See:

- Dark theme with red/blue/green/purple panels
- Top bar with "JAML" title
- Left side: Editor + Blueprint
- Right side: Active Searches + Results
- Smooth drag/resize bars between panels

## To Build for Production:

```bash
npm run build
```

Output goes to `dist/` folder - copy those files to your web server.

---

**That's it!** Vue is ready to go. The app is fully functional with all features from the old version, but with clean, modern code.


