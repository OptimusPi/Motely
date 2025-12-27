// Minimal clean layout with Interact.js drag/dock

const panels = [
  { id: 'panel-jaml', title: 'JAML', content: 'JAML editor area', dock: 'left', order: 0 },
  { id: 'panel-blueprint', title: 'Blueprint', content: 'Blueprint analyzer', dock: 'left', order: 1 },
  { id: 'panel-results', title: 'Results', content: 'Results table', dock: 'right', order: 0 }
];

const state = {
  splitterPct: 50,
};

const stackLeft = document.getElementById('stackLeft');
const stackRight = document.getElementById('stackRight');
const splitter = document.getElementById('splitter');
const dropOverlay = document.getElementById('dropOverlay');
const layoutFileInput = document.getElementById('layoutFileInput');

function renderPanels() {
  stackLeft.innerHTML = '';
  stackRight.innerHTML = '';
  const leftPanels = panels.filter(p => p.dock === 'left').sort((a,b)=>a.order-b.order);
  const rightPanels = panels.filter(p => p.dock === 'right').sort((a,b)=>a.order-b.order);
  leftPanels.forEach(p => stackLeft.appendChild(makePanelEl(p)));
  rightPanels.forEach(p => stackRight.appendChild(makePanelEl(p)));
  attachDrag();
}

function makePanelEl(panel) {
  const wrapper = document.createElement('div');
  wrapper.className = 'panel';
  wrapper.id = panel.id;

  const tab = document.createElement('button');
  tab.className = 'tab';
  tab.textContent = panel.title;
  tab.dataset.panelId = panel.id;
  wrapper.appendChild(tab);

  const body = document.createElement('div');
  body.className = 'panel-content';
  body.textContent = panel.content;
  wrapper.appendChild(body);

  return wrapper;
}

function attachDrag() {
  interact('.tab').unset();
  interact('.tab').draggable({
    listeners: {
      start (event) {
        dropOverlay.classList.add('active');
        event.target.classList.add('dragging');
      },
      move (event) {
        const panelEl = event.target.parentElement;
        const dy = (parseFloat(panelEl.dataset.y) || 0) + event.dy;
        // Vertical resize (simple height adjust)
        const newHeight = Math.max(120, panelEl.offsetHeight + event.dy);
        panelEl.style.height = `${newHeight}px`;
        panelEl.dataset.y = dy;
      },
      end (event) {
        dropOverlay.classList.remove('active');
        event.target.classList.remove('dragging');
        const drop = event.interactable && event.dropzone
          ? event.dropzone
          : null;
      }
    }
  });

  // Dropzones
  interact('.drop-zone').dropzone({
    ondragenter (event) {
      showGhost(event.relatedTarget.parentElement, event.target);
    },
    ondragleave () {
      clearGhost();
    },
    ondrop (event) {
      const panelId = event.relatedTarget.dataset.panelId;
      const side = event.target.dataset.drop;
      movePanel(panelId, side);
      clearGhost();
    }
  });
}

let ghostEl = null;
function showGhost(panelEl, zoneEl) {
  clearGhost();
  ghostEl = panelEl.cloneNode(true);
  ghostEl.classList.add('ghost');
  const rect = zoneEl.getBoundingClientRect();
  ghostEl.style.left = `${rect.left + 8}px`;
  ghostEl.style.top = `${rect.top + 8}px`;
  ghostEl.style.width = `${rect.width - 16}px`;
  ghostEl.style.height = `${Math.min(panelEl.offsetHeight, rect.height - 16)}px`;
  document.body.appendChild(ghostEl);
}
function clearGhost() {
  if (ghostEl) {
    ghostEl.remove();
    ghostEl = null;
  }
}

function movePanel(panelId, dock) {
  const panel = panels.find(p => p.id === panelId);
  if (!panel) return;
  panel.dock = dock;
  const siblings = panels.filter(p => p.dock === dock && p.id !== panelId);
  panel.order = siblings.length ? Math.max(...siblings.map(p=>p.order))+1 : 0;
  renderPanels();
  saveLayoutLocal();
}

// Splitter drag (horizontal)
interact(splitter).draggable({
  axis: 'x',
  listeners: {
    move (event) {
      const container = document.getElementById('mainSplit');
      const rect = container.getBoundingClientRect();
      const deltaPct = (event.dx / rect.width) * 100;
      state.splitterPct = Math.min(85, Math.max(15, state.splitterPct + deltaPct));
      applySplitter();
    }
  }
});

function applySplitter() {
  stackLeft.style.flex = `0 0 ${state.splitterPct}%`;
  stackRight.style.flex = `1`;
}

function saveLayoutLocal() {
  const data = { panels, splitterPct: state.splitterPct };
  localStorage.setItem('jamlui-layout', JSON.stringify(data));
}
function loadLayoutLocal() {
  try {
    const raw = localStorage.getItem('jamlui-layout');
    if (!raw) return;
    const data = JSON.parse(raw);
    if (data.panels) {
      data.panels.forEach(dp => {
        const p = panels.find(x => x.id === dp.id);
        if (p) Object.assign(p, dp);
      });
    }
    if (data.splitterPct) state.splitterPct = data.splitterPct;
  } catch {}
}

// Save/Load buttons
document.getElementById('saveLayoutBtn').onclick = () => {
  const blob = new Blob([JSON.stringify({ panels, splitterPct: state.splitterPct }, null, 2)], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url; a.download = 'jamlui-layout.json';
  a.click();
  URL.revokeObjectURL(url);
  saveLayoutLocal();
};
document.getElementById('loadLayoutBtn').onclick = () => layoutFileInput.click();
layoutFileInput.onchange = (e) => {
  const file = e.target.files[0];
  if (!file) return;
  const reader = new FileReader();
  reader.onload = (evt) => {
    try {
      const data = JSON.parse(evt.target.result);
      if (data.panels) {
        data.panels.forEach(dp => {
          const p = panels.find(x => x.id === dp.id);
          if (p) Object.assign(p, dp);
        });
      }
      if (data.splitterPct) state.splitterPct = data.splitterPct;
      renderPanels();
      applySplitter();
      saveLayoutLocal();
    } catch (err) { console.error('Invalid layout', err); }
  };
  reader.readAsText(file);
};
document.getElementById('resetLayoutBtn').onclick = () => {
  panels.forEach(p => { p.dock = p.id === 'panel-results' ? 'right' : 'left'; p.order = 0; });
  state.splitterPct = 50;
  renderPanels();
  applySplitter();
  saveLayoutLocal();
};

function init() {
  loadLayoutLocal();
  renderPanels();
  applySplitter();
}
init();

