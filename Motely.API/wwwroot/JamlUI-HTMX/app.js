const apiBase = '';

let state = {
  filterJaml: '',
  seedSource: 'all',
  seedCount: null,
  seedSources: [],
  currentSearchId: null,
  searchStatus: null,
  isSearching: false,
  results: [],
  columns: ['seed','score'],
  progressPercent: 0,
  seedsSearched: 0,
  seedsPerSecond: 0,
  lastError: null,
  connection: null
};

const qs = (s)=>document.querySelector(s);

function renderStatus() {
  qs('#statusBox').style.display = state.currentSearchId ? 'block' : 'none';
  qs('#statusText').textContent = state.searchStatus || 'Idle';
  qs('#statusText').classList.toggle('status-running', state.searchStatus==='running');
  qs('#progressText').textContent = `${state.progressPercent.toFixed(1)}%`;
  qs('#searchedText').textContent = formatNumber(state.seedsSearched);
  qs('#foundText').textContent = formatNumber(state.results.length);
  qs('#speedText').textContent = state.seedsPerSecond>0 ? `${formatNumber(state.seedsPerSecond)}/s` : '-';
  if (state.lastError) {
    const box = qs('#errorBox');
    box.style.display = 'block';
    box.textContent = `Error: ${state.lastError}`;
  } else qs('#errorBox').style.display = 'none';
  qs('#startBtn').disabled = state.isSearching || !state.filterJaml.trim();
  qs('#stopBtn').disabled = !state.currentSearchId;
}

function renderTable() {
  qs('#resultCount').textContent = state.results.length;
  const empty = qs('#emptyState');
  const wrap = qs('#tableWrapper');
  if (!state.results.length) {
    empty.style.display = 'block';
    wrap.style.display = 'none';
    qs('#clearBtn').disabled = true;
    qs('#exportBtn').disabled = true;
    return;
  }
  empty.style.display = 'none';
  wrap.style.display = 'block';
  qs('#clearBtn').disabled = false;
  qs('#exportBtn').disabled = false;
  const thead = qs('#tableHead');
  thead.innerHTML = '';
  const headRow = document.createDocumentFragment();
  const thNum = document.createElement('th'); thNum.textContent = '#'; headRow.appendChild(thNum);
  state.columns.forEach(c=>{ const th=document.createElement('th'); th.textContent=c; headRow.appendChild(th); });
  thead.appendChild(headRow);
  const tbody = qs('#tableBody'); tbody.innerHTML='';
  state.results.forEach((r,idx)=>{
    const tr=document.createElement('tr');
    const tdNum=document.createElement('td'); tdNum.className='row-number'; tdNum.textContent=idx+1; tr.appendChild(tdNum);
    state.columns.forEach((c,i)=>{
      const td=document.createElement('td');
      if (c==='seed') td.classList.add('seed-cell');
      if (c==='score') td.classList.add('score-cell');
      if (c==='seed') td.innerHTML = `<code>${r.seed}</code>`;
      else if (c==='score') td.textContent = r.score || 0;
      else td.textContent = getTally(r, i-2);
      tr.appendChild(td);
    });
    tbody.appendChild(tr);
  });
}

function formatNumber(n){ return n==null ? '0' : n.toLocaleString(); }
function getTally(r, idx){ return (r.tallies && r.tallies[idx]!==undefined) ? r.tallies[idx] : '-'; }

async function fetchSeedSources() {
  try { const res = await fetch(`${apiBase}/seed-sources`); if (res.ok) state.seedSources = await res.json(); }
  catch {}
  const select = qs('#seedSource'); select.innerHTML='';
  state.seedSources.forEach(src=>{
    const opt=document.createElement('option'); opt.value=src.key; opt.textContent=src.label||src.displayName||src.key; select.appendChild(opt);
  });
  if (state.seedSources.length) { state.seedSource = state.seedSources[0].key || 'all'; select.value=state.seedSource; }
}

async function loadActive() {
  try { const res=await fetch(`${apiBase}/searches`); if (res.ok) { const data=await res.json(); if (data.searches?.length){ state.currentSearchId=data.searches[0].id; await loadStatus(); }}} catch {}
}

function setupSignalR() {
  if (!window.signalR?.HubConnectionBuilder) return;
  state.connection = new window.signalR.HubConnectionBuilder().withUrl('/searchHub').withAutomaticReconnect().build();
  state.connection.on('Result', data=>{
    try{
      const msg= typeof data==='string'? JSON.parse(data): data;
      if (msg.searchId !== state.currentSearchId) return;
      if (Array.isArray(msg.results)) {
        const map=new Map(state.results.map(r=>[r.seed,r]));
        msg.results.forEach(r=>{ if(!map.has(r.seed)) map.set(r.seed,r); });
        state.results = Array.from(map.values()).sort((a,b)=>(b.score||0)-(a.score||0));
      }
      if (msg.columns) state.columns = msg.columns;
      if (msg.progressPercent!==undefined) state.progressPercent = msg.progressPercent;
      if (msg.seedsSearched!==undefined) state.seedsSearched = msg.seedsSearched;
      if (msg.seedsPerSecond!==undefined) state.seedsPerSecond = msg.seedsPerSecond;
      renderStatus(); renderTable();
    }catch(err){ console.error(err); }
  });
  state.connection.start().then(()=> qs('#connIndicator').textContent='🟢').catch(console.error);
}

async function loadStatus() {
  if (!state.currentSearchId) return;
  try {
    const res=await fetch(`${apiBase}/search?id=${encodeURIComponent(state.currentSearchId)}`);
    if (res.ok) {
      const data=await res.json();
      state.searchStatus = data.status || data.searchStatus || 'stopped';
      state.isSearching = state.searchStatus==='running';
      if (data.progressPercent!==undefined) state.progressPercent=data.progressPercent;
      if (data.seedsSearched!==undefined) state.seedsSearched=data.seedsSearched;
      if (data.seedsPerSecond!==undefined) state.seedsPerSecond=data.seedsPerSecond;
      state.lastError = data.lastError || null;
      if (Array.isArray(data.results)) {
        const map=new Map(state.results.map(r=>[r.seed,r]));
        data.results.forEach(r=>{ if(!map.has(r.seed)) map.set(r.seed,r); });
        state.results = Array.from(map.values()).sort((a,b)=>(b.score||0)-(a.score||0));
      }
      if (data.columns) state.columns=data.columns;
      if (data.filterJaml && !state.filterJaml) {
        state.filterJaml = data.filterJaml; qs('#filterJaml').value = data.filterJaml;
      }
      renderStatus(); renderTable();
    }
  } catch {}
}

async function startSearch() {
  state.filterJaml = qs('#filterJaml').value;
  state.seedSource = qs('#seedSource').value;
  state.seedCount = qs('#seedCount').value || null;
  if (!state.filterJaml.trim()) return;
  try {
    state.isSearching=true; state.lastError=null; renderStatus();
    const res=await fetch(`${apiBase}/search`, {
      method:'POST', headers:{'Content-Type':'application/json'},
      body: JSON.stringify({ filterJaml: state.filterJaml.trim(), seedSource: state.seedSource||'all', seedCount: state.seedCount||null })
    });
    if (!res.ok) throw new Error((await res.json()).error || 'Failed to start search');
    const data=await res.json();
    state.currentSearchId = data.searchId;
    state.searchStatus = 'running';
    state.progressPercent = data.progressPercent || 0;
    state.results = Array.isArray(data.results) ? data.results : [];
    state.columns = data.columns || ['seed','score'];
    state.results.sort((a,b)=>(b.score||0)-(a.score||0));
    if (state.connection && state.connection.state === window.signalR.HubConnectionState.Connected) {
      await state.connection.invoke('JoinSearchGroup', state.currentSearchId);
    }
    await loadStatus();
  } catch(err){ state.lastError=err.message; state.isSearching=false; renderStatus(); }
}

async function stopSearch() {
  if (!state.currentSearchId) return;
  try {
    const res=await fetch(`${apiBase}/search/${encodeURIComponent(state.currentSearchId)}/panic-stop`, { method:'POST' });
    if (!res.ok) throw new Error((await res.json()).error || 'Failed to stop');
    state.isSearching=false; state.searchStatus='stopped';
    if (state.connection && state.connection.state === window.signalR.HubConnectionState.Connected) {
      await state.connection.invoke('LeaveSearchGroup', state.currentSearchId);
    }
    await loadStatus();
  } catch(err){ state.lastError=err.message; renderStatus(); }
}

function clearResults() {
  state.results = []; state.progressPercent=0; state.seedsSearched=0; state.seedsPerSecond=0;
  renderStatus(); renderTable();
}

function exportCsv() {
  if (!state.results.length) return;
  const headers = state.columns.join(',');
  const rows = state.results.map(r=>{
    const vals=[]; vals.push(`"${r.seed||''}"`); vals.push(r.score||0); (r.tallies||[]).forEach(t=>vals.push(t)); return vals.join(',');
  });
  const csv=[headers,...rows].join('\\n');
  const blob=new Blob([csv],{type:'text/csv'}); const url=URL.createObjectURL(blob);
  const a=document.createElement('a'); a.href=url; a.download=`motely-results-${Date.now()}.csv`; a.click(); URL.revokeObjectURL(url);
}

function initSplitter() {
  const splitter = qs('#splitter');
  const left = document.querySelector('.left-panel');
  const right = document.querySelector('.right-panel');
  const isMobile = ()=> window.innerWidth<=768;
  let pos = parseFloat(localStorage.getItem('splitPositionVanilla')) || 50;
  const apply = ()=>{ left.style.flex=`0 0 ${pos}%`; right.style.flex=`1 1 ${100-pos}%`; };
  apply();
  const start = (e)=>{
    e.preventDefault();
    const container = document.querySelector('.split-container');
    let startPos = isMobile() ? (e.touches?e.touches[0].clientY:e.clientY) : (e.touches?e.touches[0].clientX:e.clientX);
    let size = isMobile()? container.offsetHeight : container.offsetWidth;
    const move = (ev)=>{
      ev.preventDefault();
      const cur = isMobile()? (ev.touches?ev.touches[0].clientY:ev.clientY) : (ev.touches?ev.touches[0].clientX:ev.clientX);
      const delta = cur - startPos;
      const pct = (delta / size)*100;
      pos = Math.max(30, Math.min(70, pos + pct));
      apply();
      startPos = cur;
    };
    const stop = ()=>{
      localStorage.setItem('splitPositionVanilla', pos.toString());
      document.removeEventListener('mousemove', move);
      document.removeEventListener('mouseup', stop);
      document.removeEventListener('touchmove', move);
      document.removeEventListener('touchend', stop);
    };
    document.addEventListener('mousemove', move);
    document.addEventListener('mouseup', stop);
    document.addEventListener('touchmove', move,{passive:false});
    document.addEventListener('touchend', stop);
  };
  splitter.addEventListener('mousedown', start);
  splitter.addEventListener('touchstart', start);
}

function bindEvents() {
  qs('#startBtn').addEventListener('click', startSearch);
  qs('#stopBtn').addEventListener('click', stopSearch);
  qs('#clearBtn').addEventListener('click', clearResults);
  qs('#exportBtn').addEventListener('click', exportCsv);
  qs('#filterJaml').addEventListener('input', e=>{ state.filterJaml=e.target.value; renderStatus(); });
}

async function init() {
  initSplitter();
  bindEvents();
  renderStatus(); renderTable();
  await fetchSeedSources();
  await loadActive();
  setupSignalR();
  setInterval(()=> state.currentSearchId && loadStatus(), 2000);
}

document.addEventListener('DOMContentLoaded', init);

