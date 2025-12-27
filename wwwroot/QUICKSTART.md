# Quick Start

## To Run (Development)

```bash
cd Motely.API/wwwroot/JamlUI3
npm install
npm run dev
```

Then open: http://localhost:5173

## To Build (Production)

```bash
npm run build
```

Then copy `dist/` folder contents to your web server.

## If you see a blank page:

1. **Check browser console** (F12) for errors
2. **Make sure you're running `npm run dev`** - don't open the HTML file directly
3. **Check that all dependencies installed**: `npm install`
4. **Try clearing browser cache**

## Common Issues:

- **Blank page**: You need to run `npm run dev` - the HTML file alone won't work
- **Module errors**: Run `npm install` first
- **Port 5173 in use**: Change port in `vite.config.js`

Merry Christmas! 🎄


