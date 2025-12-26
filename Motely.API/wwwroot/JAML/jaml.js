// JAML UI v2.0 - Docking System (2024-12-24)
console.log('🎄 JAML UI v2.0 - Docking System loaded');

// Global state
let colorModeActive = false;
let currentLeftTab = 'jaml'; // Track which left panel tab is active

// Go home - navigate to landing page
function goHome() {
  window.location.href = '/';
}

// Status helper - wait for DOM to be ready
let statusEl = null;
function initStatus() {
  statusEl = document.getElementById('status') || document.querySelector('.results-status');
}
function setStatus(msg) { 
  if (!statusEl) initStatus();
  if (statusEl) statusEl.textContent = msg; 
}

// ==========================================
// Left Panel Tab Switching
// ==========================================
function switchLeftTab(tabId) {
  currentLeftTab = tabId;
  
  // Update tab buttons
  document.querySelectorAll('.left-tab').forEach(btn => {
    btn.classList.toggle('active', btn.dataset.tab === tabId);
  });
  
  // Update tab content
  document.querySelectorAll('.left-tab-content').forEach(content => {
    const isActive = content.id === `tab${tabId.charAt(0).toUpperCase() + tabId.slice(1)}`;
    content.classList.toggle('active', isActive);
    content.style.display = isActive ? 'flex' : 'none';
  });
  
  // If switching to JAML tab and Monaco is active, relayout
  if (tabId === 'jaml' && monacoMode && window.jamlEditor) {
    setTimeout(() => window.jamlEditor.layout(), 100);
  }
  
  setStatus(`Switched to ${tabId === 'jaml' ? 'JAML Editor' : 'Blueprint Analyzer'}`);
}

function analyzeSeedInBlueprint() {
  const seedInput = document.getElementById('blueprintSeedInput');
  const iframe = document.getElementById('blueprintFrame');
  
  if (!seedInput || !iframe) return;
  
  const seed = seedInput.value.trim();
  if (!seed) {
    setStatus('Enter a seed to analyze');
    return;
  }
  
  // Blueprint uses hash-based routing: https://miaklwalker.github.io/Blueprint/#/seed/SEEDVALUE
  iframe.src = `https://miaklwalker.github.io/Blueprint/#/seed/${encodeURIComponent(seed)}`;
  setStatus(`Analyzing seed: ${seed}`);
}

function openBlueprintExternal() {
  const seedInput = document.getElementById('blueprintSeedInput');
  const seed = seedInput?.value.trim();
  
  if (seed) {
    window.open(`https://miaklwalker.github.io/Blueprint/#/seed/${encodeURIComponent(seed)}`, '_blank');
  } else {
    window.open('https://miaklwalker.github.io/Blueprint/', '_blank');
  }
}

// ==========================================
// Collapsible Panel System
// ==========================================
const collapsibleState = new Map(); // Track state per grabber ID

function initCollapsibleGrabber(grabberId, contentId, options = {}) {
  // This function now only initializes state - drag is handled by initDockingSystem
  const grabber = document.getElementById(grabberId);
  const content = document.getElementById(contentId);
  
  if (!grabber || !content) {
    console.warn(`Collapsible grabber: ${grabberId} or content: ${contentId} not found`);
    return;
  }
  
  // Initialize state for collapse tracking
  collapsibleState.set(grabberId, {
    collapsed: false,
    savedHeight: content.offsetHeight || 200
  });
  
}

function toggleCollapse(grabberId, contentId) {
  const grabber = document.getElementById(grabberId);
  const content = document.getElementById(contentId);
  const state = collapsibleState.get(grabberId);
  
  if (!grabber || !content || !state) return;
  
  state.collapsed = !state.collapsed;
  
  if (state.collapsed) {
    // Save current height before collapsing
    state.savedHeight = content.offsetHeight;
    content.style.flex = '0 0 0px';
    content.style.minHeight = '0';
    content.style.overflow = 'hidden';
    content.classList.add('collapsed');
    grabber.classList.add('collapsed');
  } else {
    // Restore height
    content.style.flex = `0 0 ${state.savedHeight || 200}px`;
    content.style.minHeight = '';
    content.style.overflow = '';
    content.classList.remove('collapsed');
    grabber.classList.remove('collapsed');
    
    // Lazy-load Blueprint iframe when expanded
    if (contentId === 'blueprintSection') {
      loadBlueprintIfNeeded();
    }
  }
  
  // Relayout Monaco if needed
  if (window.jamlEditor) {
    setTimeout(() => window.jamlEditor.layout(), 100);
  }
}

// Toggle section by section ID (called from tab clicks)
// The tab is on the grab-bar BEFORE the section, clicking it collapses the section BELOW
function toggleSection(sectionId) {
  const section = document.getElementById(sectionId);
  if (!section) return;
  // #region agent log
  fetch('http://127.0.0.1:7245/ingest/0f8c8e54-800c-41d4-af5e-2f54bfd2e135',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({sessionId:'debug-session',runId:'run1',hypothesisId:'H2',location:'jaml.js:toggleSection',message:'toggleSection called',data:{sectionId,collapsed:section.classList.contains('collapsed')},timestamp:Date.now()})}).catch(()=>{});
  // #endregion
  
  // Find the grabber BEFORE this section
  let grabberId = null;
  
  // Map sections to their grabbers (grabber comes BEFORE the section)
  // Filter section uses section-tab, not grab-bar
  const sectionToGrabber = {
    'jamlEditorSection': null, // Uses section-tab, not grab-bar
    'blueprintSection': 'blueprintGrabber',
    'resultsSection': 'resultsGrabber'
  };
  
  // Special handling for Filter section with section-tab
  if (sectionId === 'jamlEditorSection') {
    const section = document.getElementById('jamlEditorSection');
    if (!section) return;
    
    // Initialize state if needed
    if (!collapsibleState.has('jamlEditorSection')) {
      collapsibleState.set('jamlEditorSection', {
        collapsed: false,
        savedHeight: section.offsetHeight || 200
      });
    }
    
    const state = collapsibleState.get('jamlEditorSection');
    state.collapsed = !state.collapsed;
    // #region agent log
    fetch('http://127.0.0.1:7245/ingest/0f8c8e54-800c-41d4-af5e-2f54bfd2e135',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({sessionId:'debug-session',runId:'run1',hypothesisId:'H2',location:'jaml.js:toggleSection:jamlEditorSection',message:'toggled jamlEditorSection',data:{collapsed:state.collapsed,savedHeight:state.savedHeight},timestamp:Date.now()})}).catch(()=>{});
    // #endregion
    
    if (state.collapsed) {
      state.savedHeight = section.offsetHeight;
      section.style.flex = '0 0 0px';
      section.style.minHeight = '0';
      section.style.overflow = 'hidden';
      section.classList.add('collapsed');
    } else {
      section.style.flex = `0 0 ${state.savedHeight || 200}px`;
      section.style.minHeight = '';
      section.style.overflow = '';
      section.classList.remove('collapsed');
    }
    
    if (window.jamlEditor) {
      setTimeout(() => window.jamlEditor.layout(), 100);
    }
    return;
  }
  
  grabberId = sectionToGrabber[sectionId];
  if (!grabberId) return;
  
  // Use existing toggleCollapse logic - but we need to find the section ABOVE the grabber
  const grabber = document.getElementById(grabberId);
  if (!grabber) return;
  
  // For collapse, we collapse the section BELOW the grabber (the one the tab is named after)
  // But toggleCollapse expects (grabberId, contentId) where contentId is the section to collapse
  // Initialize state if needed
  if (!collapsibleState.has(grabberId)) {
    collapsibleState.set(grabberId, {
      collapsed: false,
      savedHeight: section.offsetHeight || 200
    });
  }
  
  toggleCollapse(grabberId, sectionId);
}

// [REMOVED] initCollapsibleDrag - replaced by initDockingSystem

// Lazy-load Blueprint iframe when section is expanded
function loadBlueprintIfNeeded() {
  const iframe = document.getElementById('blueprintFrame');
  if (iframe && iframe.dataset.src && !iframe.src.includes('miaklwalker')) {
    iframe.src = iframe.dataset.src;
  }
}

// ==========================================
// DOCKING SYSTEM - Drag tabs to resize, detach, and dock panels
// ==========================================
const dockingState = {
  isDragging: false,
  dragStartX: 0,
  dragStartY: 0,
  activeTab: null,
  activeSection: null,
  activeWrapper: null,
  dropZones: [],
  dragHint: null,
  hideBasket: null,
  rightColumnCollapsed: false,
  poppedPanels: [],
  hiddenPanels: [] // Panels hidden in the basket
};

// Create drop zone elements
function createDropZones() {
  // Remove existing drop zones
  document.querySelectorAll('.drop-zone').forEach(el => el.remove());
  
  const appLayout = document.querySelector('.app-layout');
  if (!appLayout) return;
  
  // Right-side drop zone (for revealing 2-column layout from left)
  const rightZone = document.createElement('div');
  rightZone.className = 'drop-zone drop-zone-right';
  rightZone.id = 'dropZoneRight';
  rightZone.dataset.position = 'right';
  appLayout.appendChild(rightZone);
  
  // Left-side drop zone (for moving from right column to left)
  const leftZone = document.createElement('div');
  leftZone.className = 'drop-zone drop-zone-left';
  leftZone.id = 'dropZoneLeft';
  leftZone.dataset.position = 'left';
  appLayout.appendChild(leftZone);
  
  // Bottom drop zone for each column
  const leftHalf = document.querySelector('.left-half .half-content');
  const rightHalf = document.querySelector('.right-half .half-content');
  
  if (leftHalf) {
    const leftBottomZone = document.createElement('div');
    leftBottomZone.className = 'drop-zone drop-zone-bottom';
    leftBottomZone.id = 'dropZoneLeftBottom';
    leftBottomZone.dataset.position = 'left-bottom';
    leftHalf.appendChild(leftBottomZone);
  }
  
  if (rightHalf) {
    const rightBottomZone = document.createElement('div');
    rightBottomZone.className = 'drop-zone drop-zone-bottom';
    rightBottomZone.id = 'dropZoneRightBottom';
    rightBottomZone.dataset.position = 'right-bottom';
    rightHalf.appendChild(rightBottomZone);
  }
  
  dockingState.dropZones = document.querySelectorAll('.drop-zone');
}

// Create drag hint element
function createDragHint() {
  if (dockingState.dragHint) return;
  
  const hint = document.createElement('div');
  hint.className = 'drag-hint';
  hint.innerHTML = '<span class="drag-hint-arrow">→→</span> PULL to DETACH <span class="drag-hint-arrow">→→</span>';
  document.body.appendChild(hint);
  dockingState.dragHint = hint;
}

// Create hide basket element (drops down from settings gear)
function createHideBasket() {
  if (dockingState.hideBasket) return;
  
  const basket = document.createElement('div');
  basket.className = 'hide-basket';
  basket.id = 'hideBasket';
  basket.innerHTML = `
    <div class="hide-basket-icon">📥</div>
    <div class="hide-basket-label">Hide for later</div>
    <div class="hide-basket-count" id="hideBasketCount">0</div>
  `;
  document.body.appendChild(basket);
  dockingState.hideBasket = basket;
  
  // Click to show hidden panels menu
  basket.addEventListener('click', toggleHiddenPanelsMenu);
}

// Toggle hidden panels menu
function toggleHiddenPanelsMenu() {
  let menu = document.getElementById('hiddenPanelsMenu');
  
  if (menu) {
    menu.remove();
    return;
  }
  
  if (dockingState.hiddenPanels.length === 0) {
    setStatus('No hidden panels');
    return;
  }
  
  menu = document.createElement('div');
  menu.className = 'hidden-panels-menu';
  menu.id = 'hiddenPanelsMenu';
  
  dockingState.hiddenPanels.forEach((panel, i) => {
    const item = document.createElement('div');
    item.className = 'hidden-panel-item';
    item.innerHTML = `
      <span class="hidden-panel-color" style="background: ${panel.color}"></span>
      <span class="hidden-panel-name">${panel.name}</span>
      <button class="hidden-panel-restore" data-index="${i}">↩ Restore</button>
    `;
    item.querySelector('.hidden-panel-restore').onclick = (e) => {
      e.stopPropagation();
      restoreHiddenPanel(i);
      menu.remove();
    };
    menu.appendChild(item);
  });
  
  document.body.appendChild(menu);
  
  // Position below settings gear
  const topTab = document.querySelector('.top-center-tab');
  if (topTab) {
    const rect = topTab.getBoundingClientRect();
    menu.style.top = `${rect.bottom + 8}px`;
    menu.style.right = '20px';
  }
  
  // Close on outside click
  setTimeout(() => {
    document.addEventListener('click', function closeMenu(e) {
      if (!menu.contains(e.target)) {
        menu.remove();
        document.removeEventListener('click', closeMenu);
      }
    });
  }, 100);
}

// Hide a panel in the basket
function hidePanel(wrapper, section) {
  const tab = wrapper.querySelector('.section-tab');
  const tabText = tab?.textContent?.trim() || 'Panel';
  const tabColor = tab?.classList.contains('section-tab-purple') ? 'var(--balatro-purple)' :
                   tab?.classList.contains('section-tab-blue') ? 'var(--balatro-blue)' :
                   tab?.classList.contains('section-tab-red') ? 'var(--balatro-red)' :
                   tab?.classList.contains('section-tab-green') ? 'var(--balatro-green)' :
                   'var(--panel-bg)';
  
  dockingState.hiddenPanels.push({
    element: wrapper,
    sectionId: section.id,
    name: tabText,
    color: tabColor,
    parentColumn: wrapper.closest('.left-half') ? 'left' : 'right'
  });
  
  wrapper.style.display = 'none';
  updateHideBasketCount();
  setStatus(`Hidden: ${tabText}`);
  
  if (window.jamlEditor) {
    setTimeout(() => window.jamlEditor.layout(), 100);
  }
}

// Restore a hidden panel
function restoreHiddenPanel(index) {
  const panel = dockingState.hiddenPanels[index];
  if (!panel) return;
  
  // Find target column
  const targetColumn = panel.parentColumn === 'left' 
    ? document.querySelector('.left-half .half-content')
    : document.querySelector('.right-half .half-content');
  
  if (targetColumn && panel.element) {
    panel.element.style.display = '';
    targetColumn.appendChild(panel.element);
    
    // Re-init docking
    setTimeout(() => initPanelDocking(panel.sectionId), 100);
  }
  
  dockingState.hiddenPanels.splice(index, 1);
  updateHideBasketCount();
  setStatus(`Restored: ${panel.name}`);
  
  if (window.jamlEditor) {
    setTimeout(() => window.jamlEditor.layout(), 100);
  }
}

// Update hide basket count badge
function updateHideBasketCount() {
  const countEl = document.getElementById('hideBasketCount');
  if (countEl) {
    countEl.textContent = dockingState.hiddenPanels.length;
    countEl.style.display = dockingState.hiddenPanels.length > 0 ? 'flex' : 'none';
  }
}

// Position drag hint relative to touch/mouse, offset based on column
function positionDragHint(x, y, isRightColumn = false) {
  if (!dockingState.dragHint) return;
  
  if (isRightColumn) {
    // Left column tabs: hint to the left and up
    dockingState.dragHint.style.left = `${x - 160}px`;
    dockingState.dragHint.innerHTML = '<span class="drag-hint-arrow">←←</span> PULL to MOVE <span class="drag-hint-arrow">←←</span>';
  } else {
    // Right column tabs: hint to the right and up  
    dockingState.dragHint.style.left = `${x + 16}px`;
    dockingState.dragHint.innerHTML = '<span class="drag-hint-arrow">→→</span> PULL to DETACH <span class="drag-hint-arrow">→→</span>';
  }
  dockingState.dragHint.style.top = `${y - 32}px`;
}

// Show/hide the hide basket during drag
function showHideBasket(show) {
  if (!dockingState.hideBasket) createHideBasket();
  dockingState.hideBasket.classList.toggle('visible', show);
}

// Check if over hide basket
function isOverHideBasket(x, y) {
  if (!dockingState.hideBasket) return false;
  const rect = dockingState.hideBasket.getBoundingClientRect();
  return x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom;
}

// Show/hide drop zones based on drag position
function updateDropZones(dragX, tabRect, halfContent, isRightColumn = false) {
  if (!halfContent) return;
  
  const screenCenter = window.innerWidth / 2;
  
  if (isRightColumn) {
    // Right column: dragging LEFT reveals left drop zone
    const threshold = screenCenter - (screenCenter - tabRect.left) / 2;
    const pastThreshold = dragX < threshold;
    
    const leftZone = document.getElementById('dropZoneLeft');
    if (leftZone) {
      if (pastThreshold) {
        leftZone.classList.add('visible');
        halfContent.classList.add('squeeze-right');
      } else {
        leftZone.classList.remove('visible');
        halfContent.classList.remove('squeeze-right');
      }
    }
  } else {
    // Left column: dragging RIGHT reveals right drop zone
    const threshold = (tabRect.left + screenCenter) / 2;
    const pastThreshold = dragX > threshold;
    
    const rightZone = document.getElementById('dropZoneRight');
    if (rightZone) {
      if (pastThreshold && !dockingState.rightColumnCollapsed) {
        rightZone.classList.add('visible');
        halfContent.classList.add('squeeze-left');
      } else {
        rightZone.classList.remove('visible');
        halfContent.classList.remove('squeeze-left');
      }
    }
  }
  
  // Always show bottom drop zones when dragging
  const leftBottomZone = document.getElementById('dropZoneLeftBottom');
  const rightBottomZone = document.getElementById('dropZoneRightBottom');
  if (leftBottomZone) leftBottomZone.classList.add('visible');
  if (rightBottomZone && !dockingState.rightColumnCollapsed) rightBottomZone.classList.add('visible');
}

// Check if position is over a drop zone
function getDropZoneAt(x, y) {
  for (const zone of dockingState.dropZones) {
    if (!zone.classList.contains('visible')) continue;
    const rect = zone.getBoundingClientRect();
    if (x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom) {
      return zone;
    }
  }
  return null;
}

// Handle drop on a zone
function handleDrop(zone, section, wrapper) {
  const position = zone.dataset.position;
  
  if (position === 'right') {
    // Move panel to right column
    const rightHalf = document.querySelector('.right-half .half-content');
    if (rightHalf && wrapper) {
      rightHalf.appendChild(wrapper);
      // Re-initialize drag for moved panel
      setTimeout(() => initPanelDocking(section.id), 100);
      setStatus(`Moved to right column`);
    }
  } else if (position === 'left') {
    // Move panel to left column
    const leftHalf = document.querySelector('.left-half .half-content');
    if (leftHalf && wrapper) {
      leftHalf.appendChild(wrapper);
      setTimeout(() => initPanelDocking(section.id), 100);
      setStatus(`Moved to left column`);
    }
  } else if (position === 'left-bottom' || position === 'right-bottom') {
    // Move panel to bottom of respective column
    const targetHalf = position.startsWith('left') 
      ? document.querySelector('.left-half .half-content')
      : document.querySelector('.right-half .half-content');
    if (targetHalf && wrapper) {
      targetHalf.appendChild(wrapper);
      setTimeout(() => initPanelDocking(section.id), 100);
      setStatus(`Moved to ${position.startsWith('left') ? 'left' : 'right'} column bottom`);
    }
  }
  
  // Clean up
  hideAllDropZones();
  showHideBasket(false);
  if (window.jamlEditor) {
    setTimeout(() => window.jamlEditor.layout(), 100);
  }
}

// Hide all drop zones
function hideAllDropZones() {
  dockingState.dropZones.forEach(zone => {
    zone.classList.remove('visible', 'hover');
  });
  document.querySelectorAll('.half-content').forEach(el => {
    el.classList.remove('squeeze-left', 'squeeze-right');
  });
  if (dockingState.dragHint) {
    dockingState.dragHint.classList.remove('visible');
  }
  if (dockingState.hideBasket) {
    dockingState.hideBasket.classList.remove('hover');
  }
}

// Initialize docking for a specific panel
function initPanelDocking(sectionId) {
  const section = document.getElementById(sectionId);
  const wrapper = section?.closest('.section-with-tab');
  const tab = wrapper?.querySelector('.section-tab');
  if (!section || !tab) return;
  
  // Use Interact.js for smooth drag/resize
  if (typeof interact === 'undefined') {
    console.warn('Interact.js not loaded');
    return;
  }
  
  const topTabHeight = 48;
  const halfContent = wrapper.closest('.half-content');
  
  // Detect which column this panel is in
  const isRightColumn = !!wrapper.closest('.right-half');
  
  // Track cumulative horizontal movement
  let cumulativeX = 0;
  let startTabRect = null;
  let detachThresholdMet = false;
  
  interact(tab)
    .draggable({
      // Allow both X and Y movement
      listeners: {
        start(event) {
          tab.classList.add('dragging');
          dockingState.isDragging = true;
          dockingState.activeTab = tab;
          dockingState.activeSection = section;
          dockingState.activeWrapper = wrapper;
          dockingState.dragStartX = event.clientX;
          dockingState.dragStartY = event.clientY;
          cumulativeX = 0;
          detachThresholdMet = false;
          startTabRect = tab.getBoundingClientRect();
          
          wrapper.style.transition = 'none';
          
          // Show drag hint with correct direction
          createDragHint();
          positionDragHint(event.clientX, event.clientY, isRightColumn);
          dockingState.dragHint.classList.add('visible');
          
          // Show hide basket
          showHideBasket(true);
        },
        move(event) {
          const deltaY = event.dy;
          const deltaX = event.dx;
          cumulativeX += deltaX;
          
          // Update drag hint position with column awareness
          positionDragHint(event.clientX, event.clientY, isRightColumn);
          
          // Vertical resize (existing behavior)
          const currentHeight = section.offsetHeight;
          const currentTop = parseInt(wrapper.style.top) || 0;
          
          let newHeight = currentHeight - deltaY;
          newHeight = Math.max(100, newHeight);
          
          let newTop = currentTop - deltaY;
          const maxTop = 0;
          const minTop = -topTabHeight;
          newTop = Math.max(minTop, Math.min(maxTop, newTop));
          
          // Apply vertical changes
          wrapper.style.top = `${newTop}px`;
          wrapper.style.position = newTop < 0 ? 'relative' : '';
          wrapper.style.zIndex = newTop < 0 ? '1001' : '';
          
          section.style.height = `${newHeight}px`;
          section.style.flex = `0 0 ${newHeight}px`;
          
          // Add drag-up indicator
          if (deltaY < 0) {
            tab.classList.add('drag-up');
          } else {
            tab.classList.remove('drag-up');
          }
          
          // Check for detach threshold based on column
          const detachThreshold = 40; // pixels to drag before detach hint changes
          if (isRightColumn) {
            // Right column: drag LEFT to detach
            detachThresholdMet = cumulativeX < -detachThreshold;
          } else {
            // Left column: drag RIGHT to detach
            detachThresholdMet = cumulativeX > detachThreshold;
          }
          
          // Horizontal: check for drop zone reveal
          if (startTabRect && Math.abs(cumulativeX) > 20) {
            updateDropZones(event.clientX, startTabRect, halfContent, isRightColumn);
            
            // Check if over drop zone
            const zone = getDropZoneAt(event.clientX, event.clientY);
            dockingState.dropZones.forEach(z => z.classList.remove('hover'));
            if (zone) {
              zone.classList.add('hover');
            }
            
            // Check if over hide basket
            if (isOverHideBasket(event.clientX, event.clientY)) {
              dockingState.hideBasket.classList.add('hover');
            } else {
              dockingState.hideBasket?.classList.remove('hover');
            }
          }
          
          // Throttle Monaco layout updates
          if (window.jamlEditor && !window.jamlLayoutTimeout) {
            window.jamlLayoutTimeout = setTimeout(() => {
              window.jamlEditor.layout();
              window.jamlLayoutTimeout = null;
            }, 100);
          }
        },
        end(event) {
          tab.classList.remove('dragging', 'drag-up');
          dockingState.isDragging = false;
          wrapper.style.transition = '';
          
          // Check if dropped on hide basket
          if (isOverHideBasket(event.clientX, event.clientY)) {
            hidePanel(wrapper, section);
            hideAllDropZones();
            showHideBasket(false);
            return;
          }
          
          // Check if dropped on a zone
          const zone = getDropZoneAt(event.clientX, event.clientY);
          if (zone && zone.classList.contains('visible')) {
            handleDrop(zone, section, wrapper);
          }
          
          showHideBasket(false);
          
          hideAllDropZones();
          
          // Final layout update
          if (window.jamlEditor) {
            setTimeout(() => window.jamlEditor.layout(), 50);
          }
        }
      }
    });
  
  console.log(`initPanelDocking: Initialized for ${sectionId}`);
}

// Initialize splitter to allow collapsing right column
function initSplitterCollapse() {
  const splitter = document.getElementById('panelSplitter1');
  const topTab = document.querySelector('.top-center-tab');
  const rightHalf = document.querySelector('.right-half');
  const leftHalf = document.querySelector('.left-half');
  const container = document.querySelector('.full-split');
  
  if (!splitter || !topTab || !rightHalf || !container) return;
  
  if (typeof interact === 'undefined') return;
  
  // Enhance existing splitter behavior
  interact(topTab)
    .draggable({
      axis: 'x',
      ignoreFrom: '.tab-icon-btn',
      listeners: {
        start(event) {
          if (event.target.classList.contains('tab-icon-btn') || 
              event.target.closest('button.tab-icon-btn')) {
            event.stopImmediatePropagation();
            return;
          }
          topTab.classList.add('dragging');
          document.body.style.cursor = 'ew-resize';
        },
        move(event) {
          const containerRect = container.getBoundingClientRect();
          const currentWidth = leftHalf.offsetWidth;
          let newWidth = currentWidth + event.dx;
          
          const maxWidth = containerRect.width - 8; // 8px from right edge
          const minWidth = 200;
          
          newWidth = Math.max(minWidth, Math.min(maxWidth, newWidth));
          
          // Check if near right edge (within 8px)
          const distanceFromRight = containerRect.width - newWidth;
          
          if (distanceFromRight <= 8) {
            // Collapse right column
            leftHalf.style.flex = '1';
            rightHalf.classList.add('collapsed-to-edge');
            splitter.classList.add('at-edge');
            topTab.classList.add('single-column');
            dockingState.rightColumnCollapsed = true;
            
            // Pop panels to bottom
            popPanelsToBottom();
          } else {
            // Normal resize
            const pct = (newWidth / containerRect.width) * 100;
            leftHalf.style.flex = `0 0 ${pct}%`;
            
            if (dockingState.rightColumnCollapsed) {
              rightHalf.classList.remove('collapsed-to-edge');
              splitter.classList.remove('at-edge');
              topTab.classList.remove('single-column');
              dockingState.rightColumnCollapsed = false;
              restorePoppedPanels();
            }
          }
          
          if (window.jamlEditor) {
            window.jamlEditor.layout();
          }
        },
        end(event) {
          topTab.classList.remove('dragging');
          document.body.style.cursor = '';
          
          if (window.jamlEditor) {
            setTimeout(() => window.jamlEditor.layout(), 50);
          }
        }
      }
    });
}

// Pop right-side panels to bottom tabs when column collapses
function popPanelsToBottom() {
  const rightContent = document.querySelector('.right-half .half-content');
  if (!rightContent) return;
  
  // Store panel references
  const panels = rightContent.querySelectorAll('.section-with-tab');
  dockingState.poppedPanels = Array.from(panels).map(p => ({
    element: p,
    sectionId: p.querySelector('.panel-section')?.id
  }));
  
  // Create popped panels bar if doesn't exist
  let poppedBar = document.querySelector('.popped-panels');
  if (!poppedBar) {
    poppedBar = document.createElement('div');
    poppedBar.className = 'popped-panels';
    document.body.appendChild(poppedBar);
  }
  
  // Add tabs for each popped panel
  poppedBar.innerHTML = '';
  dockingState.poppedPanels.forEach((p, i) => {
    const tab = p.element.querySelector('.section-tab');
    const tabText = tab?.textContent?.trim() || `Panel ${i + 1}`;
    const tabColor = tab?.classList.contains('section-tab-purple') ? 'var(--balatro-purple)' :
                     tab?.classList.contains('section-tab-blue') ? 'var(--balatro-blue)' :
                     tab?.classList.contains('section-tab-red') ? 'var(--balatro-red)' :
                     tab?.classList.contains('section-tab-green') ? 'var(--balatro-green)' :
                     'var(--panel-bg)';
    
    const poppedTab = document.createElement('div');
    poppedTab.className = 'popped-panel-tab';
    poppedTab.style.borderTopColor = tabColor;
    poppedTab.textContent = tabText;
    poppedTab.dataset.index = i;
    poppedTab.onclick = () => expandPoppedPanel(i);
    poppedBar.appendChild(poppedTab);
    
    // Hide original panel
    p.element.style.display = 'none';
  });
}

// Restore popped panels when column expands
function restorePoppedPanels() {
  dockingState.poppedPanels.forEach(p => {
    p.element.style.display = '';
  });
  
  // Remove popped panels bar
  const poppedBar = document.querySelector('.popped-panels');
  if (poppedBar) poppedBar.remove();
  
  dockingState.poppedPanels = [];
}

// Expand a popped panel (show it temporarily)
function expandPoppedPanel(index) {
  const panel = dockingState.poppedPanels[index];
  if (!panel) return;
  
  // Toggle active state
  document.querySelectorAll('.popped-panel-tab').forEach((t, i) => {
    t.classList.toggle('active', i === index);
  });
  
  // Show/hide panels
  dockingState.poppedPanels.forEach((p, i) => {
    if (i === index) {
      p.element.style.display = '';
      p.element.style.position = 'fixed';
      p.element.style.bottom = '60px';
      p.element.style.right = '0';
      p.element.style.width = '400px';
      p.element.style.maxHeight = '50vh';
      p.element.style.zIndex = '1000';
      p.element.style.boxShadow = '0 -4px 16px rgba(0,0,0,0.4)';
    } else {
      p.element.style.display = 'none';
    }
  });
}

// Main initialization for docking system
function initDockingSystem() {
  createDropZones();
  createDragHint();
  
  // Initialize docking for all panels
  initPanelDocking('jamlEditorSection');
  initPanelDocking('blueprintSection');
  initPanelDocking('resultsSection');
  
  // Initialize splitter collapse behavior
  initSplitterCollapse();
  
  console.log('Docking system initialized');
}

// Legacy function name for compatibility
function initFilterSectionDrag() {
  // Now handled by initDockingSystem
}

// Editor helpers
let monacoMode = false; // Track current editor mode

function toggleMonaco() {
  const mono = document.getElementById('monacoEditor');
  const plain = document.getElementById('filterJaml');
  const visual = document.getElementById('visualBuilder');
  const toggleBtn = document.getElementById('monacoToggle');
  
  if (!mono || !plain) {
    console.warn('Editor elements not found');
    return;
  }
  
  monacoMode = !monacoMode;
  
  if (monacoMode) {
    // Switch to Monaco - hide visual builder
    if (visual) visual.style.display = 'none';
    // Ensure Monaco editor is initialized
    if (!window.jamlEditor) {
      setStatus('Initializing Monaco editor...');
      // Monaco should already be initialized, but if not, we'll show plain editor
      if (typeof monaco === 'undefined' || typeof require === 'undefined') {
        setStatus('Monaco editor not available - using plain editor');
        monacoMode = false;
        return;
      }
    }
    
    // Sync content from plain editor to Monaco
    if (window.jamlEditor) {
      const plainValue = plain.value || '';
      window.jamlEditor.setValue(plainValue);
    }
    
    mono.style.display = 'block';
    plain.style.display = 'none';
    if (toggleBtn) toggleBtn.classList.add('active');
    if (window.jamlEditor) {
      setTimeout(() => window.jamlEditor.layout(), 100);
    }
    setStatus('Switched to Monaco editor');
  } else {
    // Switch to plain editor
    // Sync content from Monaco to plain editor
    if (window.jamlEditor) {
      const monacoValue = window.jamlEditor.getValue() || '';
      plain.value = monacoValue;
    }
    
    mono.style.display = 'none';
    plain.style.display = 'block';
    if (toggleBtn) toggleBtn.classList.remove('active');
    plain.focus();
    setStatus('Switched to plain editor');
  }
}

function setEditorMode(mode) {
  const mono = document.getElementById('monacoEditor');
  const plain = document.getElementById('filterJaml');
  const monoBtn = document.getElementById('monacoBtn');
  const plainBtn = document.getElementById('plainBtn');

  if (mode === 'monaco') { 
    mono.style.display = 'block'; 
    plain.style.display = 'none'; 
    if (monoBtn) monoBtn.classList.add('active');
    if (plainBtn) plainBtn.classList.remove('active');
    monacoMode = true;
  } else { 
    mono.style.display = 'none'; 
    plain.style.display = 'block'; 
    if (monoBtn) monoBtn.classList.remove('active');
    if (plainBtn) plainBtn.classList.add('active');
    monacoMode = false;
  }
}

function getJamlValue() { 
  if (monacoMode && window.jamlEditor) {
    return window.jamlEditor.getValue() || '';
  }
  const plain = document.getElementById('filterJaml');
  return plain ? (plain.value || '') : '';
}

function setJamlValue(val) {
  const value = val || '';
  if (monacoMode && window.jamlEditor) {
    window.jamlEditor.setValue(value);
  }
  const plain = document.getElementById('filterJaml');
  if (plain) {
    plain.value = value;
  }
  // Update columns from filter config when JAML changes (unless formatting)
  if (!isFormatting) {
    updateColumnsFromFilter();
  }
}

// Format JAML without invalidating filter
function formatJaml() {
  isFormatting = true;
  const jaml = getJamlValue().trim();
  
  if (!jaml) {
    isFormatting = false;
    return;
  }
  
  try {
    // Simple formatting: normalize indentation, clean up whitespace
    // For better formatting, could use a YAML library
    let formatted = jaml;
    
    // If Monaco is active, use Monaco's format document
    if (monacoMode && window.jamlEditor) {
      window.jamlEditor.getAction('editor.action.formatDocument').run();
      isFormatting = false;
      return;
    }
    
    // Basic formatting for plain editor
    // Split into lines, normalize indentation
    const lines = formatted.split('\n');
    formatted = lines.map(line => {
      // Preserve empty lines
      if (line.trim() === '') return '';
      // Normalize leading spaces (convert to 2 spaces per level)
      const match = line.match(/^(\s*)(.*)$/);
      if (match) {
        const indent = match[1];
        const content = match[2];
        // Count spaces and convert to consistent 2-space indentation
        const level = Math.floor(indent.length / 2);
        return '  '.repeat(level) + content;
      }
      return line;
    }).join('\n');
    
    setJamlValue(formatted);
    isFormatting = false;
    setStatus('JAML formatted');
  } catch (e) {
    isFormatting = false;
    setStatus(`Format error: ${e.message}`);
  }
}

// Hash filter structure (ignoring labels, comments, whitespace)
function hashFilterStructure(jaml) {
  if (!jaml) return '';
  
  try {
    // Remove comments
    let cleaned = jaml.replace(/#.*$/gm, '');
    
    // Remove label fields (they don't affect structure)
    cleaned = cleaned.replace(/label:\s*[^\n]+/gi, '');
    
    // Normalize whitespace
    cleaned = cleaned.replace(/\s+/g, ' ').trim();
    
    // Extract structural elements: should clauses with type, value, antes (not labels)
    // Simple hash - just use cleaned string length + first/last chars for now
    // In production, could use proper hash function
    return cleaned.length.toString() + cleaned.substring(0, 50) + cleaned.substring(Math.max(0, cleaned.length - 50));
  } catch (e) {
    console.warn('Failed to hash filter structure:', e);
    return jaml; // Fallback to full JAML
  }
}

// Invalidate filter and export to Fertilizer if needed
async function invalidateFilter() {
  if (!currentSearchId || results.length === 0) {
    isFilterInvalidated = true;
    renderResults();
    return;
  }
  
  try {
    setStatus('Exporting results to Fertilizer...');
    
    // Call fertilizer export endpoint (synchronous/blocking)
    const r = await fetch(`/search/${encodeURIComponent(currentSearchId)}/export-to-fertilizer`, {
      method: 'POST'
    });
    
    if (r.ok) {
      const data = await r.json();
      setStatus(`Exported ${data.exported || 0} seeds to Fertilizer`);
      isFilterInvalidated = true;
      renderResults();
    } else {
      const error = await r.json();
      setStatus(`Fertilizer export failed: ${error.error || 'Unknown error'}`);
      // Don't invalidate if export failed
    }
  } catch (e) {
    setStatus(`Fertilizer export error: ${e.message}`);
    // Don't invalidate if export failed
  }
}

// Parse JAML and update columns based on filter config
async function updateColumnsFromFilter() {
  // Skip invalidation check if formatting
  if (isFormatting) {
    isFormatting = false;
    return;
  }
  
  const jaml = getJamlValue().trim();
  if (!jaml) {
    // Default columns if no filter
    columns = ['seed', 'score'];
    lastValidFilterHash = null;
    isFilterInvalidated = false;
    renderResults();
    return;
  }
  
  // Calculate current filter structure hash
  const currentHash = hashFilterStructure(jaml);
  
  // Check if structure changed (not just labels)
  if (lastValidFilterHash !== null && currentHash !== lastValidFilterHash) {
    // Structure changed - invalidate
    await invalidateFilter();
  } else if (lastValidFilterHash === null) {
    // First time loading - set as valid
    lastValidFilterHash = currentHash;
    isFilterInvalidated = false;
  }
  
  try {
    // Call API to get column names from filter config
    const r = await fetch('/filters/columns', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ filterJaml: jaml })
    });
    
    if (r.ok) {
      const data = await r.json();
      if (data.columns && Array.isArray(data.columns)) {
        const newColumns = data.columns;
        
        // Check if column structure changed
        const columnsChanged = JSON.stringify(newColumns) !== JSON.stringify(lastColumnStructure);
        
        columns = newColumns;
        lastColumnStructure = [...newColumns];
        
        // If structure didn't change but we're updating columns, it's just label changes
        if (!columnsChanged && currentHash === lastValidFilterHash) {
          // Just label updates - don't invalidate
          isFilterInvalidated = false;
        }
        
        renderResults(); // Re-render to show new headers
      }
    }
  } catch (e) {
    console.warn('Failed to get columns from filter:', e);
    // Keep current columns on error
  }
}

// Tabs
function switchTab(name, btn) {
  document.querySelectorAll('.tab-content').forEach(e => e.classList.remove('active'));
  document.getElementById(name + '-tab').classList.add('active');
  document.querySelectorAll('.tab').forEach(b => b.classList.remove('active'));
  if (btn) btn.classList.add('active');
}

// Splitter - Full height vertical divider
function initSplitter() {
  const splitter = document.getElementById('panelSplitter1');
  const left = document.querySelector('.left-half');
  const container = document.querySelector('.full-split');
  
  if (!splitter || !left || !container) {
    console.warn('initSplitter: Missing elements', { splitter: !!splitter, left: !!left, container: !!container });
    return;
  }
  
  console.log('initSplitter: Initialized');
  let dragging = false;


  const endDrag = () => {
    if (!dragging) return;
    dragging = false;
    splitter.classList.remove('dragging');
    document.body.style.cursor = '';
    document.body.style.userSelect = '';
    if (window.jamlEditor) window.jamlEditor.layout();
  };

  const onDrag = (e) => {
    if (!dragging) return;
    
    const clientX = e.type.startsWith('touch') ? e.touches[0].clientX : e.clientX;
    const rect = container.getBoundingClientRect();
    const splitterWidth = splitter.offsetWidth;
    
    // Calculate delta from start position
    const deltaX = clientX - startX;
    let newW = startLeftWidth + deltaX;
    const maxW = rect.width - splitterWidth;
    
    // Snap to edge if within 10px
    if (newW < 10) newW = 10;
    if (newW > maxW - 10) newW = maxW - 10;
    
    // Clamp
    newW = Math.max(10, Math.min(maxW - 10, newW));
    
    // Apply using flex-basis
    const pct = (newW / rect.width) * 100;
    left.style.flex = `0 0 ${pct}%`;
    
    if (e.type.startsWith('touch')) e.preventDefault();
  };
  
  const startDrag = (e) => {
    dragging = true;
    splitter.classList.add('dragging');
    document.body.style.cursor = 'ew-resize';
    document.body.style.userSelect = 'none';
    
    // Store starting width
    startLeftWidth = left.offsetWidth;
    
    if (e.type === 'touchstart') e.preventDefault();
  };

  splitter.addEventListener('mousedown', startDrag);
  splitter.addEventListener('touchstart', startDrag, { passive: false });
  document.addEventListener('mouseup', endDrag);
  document.addEventListener('touchend', endDrag);
  document.addEventListener('touchcancel', endDrag);

  document.addEventListener('mousemove', onDrag);
  document.addEventListener('touchmove', onDrag, { passive: false });
}

// Initialize top grabber to slide tray up/down
function initTopGrabber() {
  const topGrabber = document.getElementById('topGrabber');
  const topTray = document.getElementById('topTray');
  if (!topGrabber || !topTray) {
    console.warn('initTopGrabber: Missing elements', { topGrabber: !!topGrabber, topTray: !!topTray });
    return;
  }
  console.log('initTopGrabber: Initialized');
  
  let isDragging = false;
  let startY = 0;
  let startHeight = 0;
  
  topGrabber.addEventListener('mousedown', (e) => {
    isDragging = true;
    startY = e.clientY;
    startHeight = topTray.offsetHeight;
    document.body.style.cursor = 'ns-resize';
    document.body.style.userSelect = 'none';
    topGrabber.classList.add('dragging');
    e.preventDefault();
  });
  
  topGrabber.addEventListener('touchstart', (e) => {
    isDragging = true;
    startY = e.touches[0].clientY;
    startHeight = topTray.offsetHeight;
    document.body.style.cursor = 'ns-resize';
    document.body.style.userSelect = 'none';
    topGrabber.classList.add('dragging');
    e.preventDefault();
  }, { passive: false });
  
  document.addEventListener('mousemove', (e) => {
    if (!isDragging) return;
    const deltaY = e.clientY - startY;
    const newH = Math.max(0, Math.min(window.innerHeight * 0.6, startHeight + deltaY));
    topTray.style.cssText = `flex: 0 0 ${newH}px !important; height: ${newH}px !important; overflow: ${newH < 30 ? 'hidden' : 'visible'} !important; opacity: ${newH < 30 ? '0.3' : '1'} !important;`;
  });
  
  document.addEventListener('touchmove', (e) => {
    if (!isDragging) return;
    const deltaY = e.touches[0].clientY - startY;
    const newH = Math.max(0, Math.min(window.innerHeight * 0.6, startHeight + deltaY));
    topTray.style.cssText = `flex: 0 0 ${newH}px !important; height: ${newH}px !important; overflow: ${newH < 30 ? 'hidden' : 'visible'} !important; opacity: ${newH < 30 ? '0.3' : '1'} !important;`;
    e.preventDefault();
  }, { passive: false });
  
  const endDrag = () => {
    if (!isDragging) return;
    isDragging = false;
    document.body.style.cursor = '';
    document.body.style.userSelect = '';
    topGrabber.classList.remove('dragging');
  };
  
  document.addEventListener('mouseup', endDrag);
  document.addEventListener('touchend', endDrag);
  document.addEventListener('touchcancel', endDrag);
  
  // Toggle collapse on click (one-click collapse)
  let isCollapsed = false;
  const toggleCollapse = (e) => {
    // Only toggle on click, not during drag
    if (isDragging) return;
    
    e.preventDefault();
    e.stopPropagation();
    
    isCollapsed = !isCollapsed;
    
    if (isCollapsed) {
      // Collapse: hide content, show only status
      const trayContent = topTray.querySelector('.tray-content');
      const statusBar = topTray.querySelector('.tray-status');
      const controls = topTray.querySelector('.tray-row');
      
      if (controls) controls.style.display = 'none';
      if (statusBar) {
        statusBar.style.borderTop = 'none';
        statusBar.style.paddingTop = '8px';
      }
      
      topTray.style.flex = '0 0 40px';
      topTray.style.height = '40px';
      topTray.style.minHeight = '40px';
      topTray.style.maxHeight = '40px';
      topTray.style.overflow = 'hidden';
    } else {
      // Expand: show everything
      const controls = topTray.querySelector('.tray-row');
      const statusBar = topTray.querySelector('.tray-status');
      
      if (controls) controls.style.display = 'flex';
      if (statusBar) {
        statusBar.style.borderTop = '1px solid var(--border-color)';
        statusBar.style.paddingTop = '4px';
      }
      
      topTray.style.flex = '';
      topTray.style.height = '';
      topTray.style.minHeight = '';
      topTray.style.maxHeight = '';
      topTray.style.overflow = '';
    }
  };
  
  console.log('Top grabber initialized');
}

// Data
let savedFilters = [];
let seedSources = [];
let currentSearchId = null;
let currentFilterId = null; // Track currently selected filter for URL/sharing
let signalRConnection = null;
let columns = ['seed','score'];
let results = [];
let sortCol = 'score';
let sortAsc = false;
let resultsTable = null; // Tabulator instance
let searchState = 'START'; // START | RUNNING
let isSettingDropdownProgrammatically = false; // Flag to prevent onchange from firing when setting dropdown programmatically
// Polling removed - using SignalR for real-time updates
let lastValidFilterHash = null; // Hash of last valid filter structure
let isFilterInvalidated = false; // Flag indicating filter structure changed
let lastColumnStructure = []; // Array of column names from last valid filter
let isFormatting = false; // Flag to track when format button is used
let allSearches = new Map(); // Track all searches: searchId -> { status, progress, speed, searched, found }

async function loadHealth() {
  try {
    const r = await fetch('/health');
    if (!r.ok) throw new Error('health not ok');
    setStatus('Ready');
    return true;
  } catch { setStatus('Offline'); return false; }
}

// Helper function to normalize filter name to filter ID (matches backend SanitizeFilterFileStem)
function normalizeToFilterId(name) {
  if (!name) return '';
  // Replace spaces with underscores
  let normalized = name.trim().replace(/\s+/g, '_');
  // Replace incompatible filename characters with underscores
  // Invalid chars: < > : " / \ | ? * and control chars
  normalized = normalized.replace(/[<>:"/\\|?*\x00-\x1F]/g, '_');
  return normalized;
}

async function loadFilters(autoSelect = true) {
  const dd = document.getElementById('filterSelect') || document.getElementById('filtersDropdown');
  if (!dd) {
    console.error('loadFilters: filterSelect or filtersDropdown element not found');
    return;
  }
  // Preserve current selection before rebuilding dropdown (using filterId now)
  const currentFilterId = dd.value;
  dd.innerHTML = '<option>Loading...</option>';
  try {
    const r = await fetch('/filters');
    if (!r.ok) throw new Error('filters not ok');
    const data = await r.json();
    savedFilters = data.filters || data || [];
    
    if (savedFilters.length === 0) {
      dd.innerHTML = '<option>No filters found</option>';
      return;
    }

    dd.innerHTML = '';
    
    // Group filters by author
    const grouped = {};
    savedFilters.forEach((f) => {
      const author = f.author || 'Default';
      if (!grouped[author]) grouped[author] = [];
      grouped[author].push(f);
    });
    
    // Track hidden authors
    const hiddenAuthors = JSON.parse(localStorage.getItem('hiddenFilterAuthors') || '[]');
    
    // Sort authors: Default first, then alphabetically, hidden at end
    const sortedAuthors = Object.keys(grouped).sort((a, b) => {
      const aHidden = hiddenAuthors.includes(a);
      const bHidden = hiddenAuthors.includes(b);
      if (aHidden && !bHidden) return 1;
      if (!aHidden && bHidden) return -1;
      if (a === 'Default') return -1;
      if (b === 'Default') return 1;
      return a.localeCompare(b);
    });
    
    // Create optgroups with eye emoji (will replace with lucide icons in custom dropdown later)
    sortedAuthors.forEach(author => {
      const isHidden = hiddenAuthors.includes(author);
      const group = document.createElement('optgroup');
      const authorLabel = author === 'Default' ? '(Default)' : `author: ${author}`;
      group.label = isHidden ? `👁️‍🗨️ ${authorLabel}` : `👁️ ${authorLabel}`;
      group.dataset.author = author;
      group.dataset.hidden = isHidden ? 'true' : 'false';
      
      // Sort filters within group by name
      grouped[author].sort((a, b) => (a.name || '').localeCompare(b.name || ''));
      
      grouped[author].forEach((f) => {
        const filterId = f.filterId || (f.filePath ? normalizeToFilterId(f.filePath.replace(/\.(jaml|yaml|yml)$/i, '')) : '');
        const displayName = f.name || filterId || 'Unnamed';
        const fileName = f.filePath ? f.filePath.replace(/\.(jaml|yaml|yml)$/i, '') : '';
        
        const opt = document.createElement('option');
        opt.value = filterId;
        opt.textContent = (displayName !== fileName && fileName) ? `${displayName} (${fileName})` : displayName;
        group.appendChild(opt);
      });
      
      dd.appendChild(group);
    });
    
    // Add toggle function for author visibility (will be used by custom dropdown)
    window.toggleAuthorVisibility = function(author) {
      const hidden = JSON.parse(localStorage.getItem('hiddenFilterAuthors') || '[]');
      const idx = hidden.indexOf(author);
      if (idx >= 0) {
        hidden.splice(idx, 1);
      } else {
        hidden.push(author);
      }
      localStorage.setItem('hiddenFilterAuthors', JSON.stringify(hidden));
      loadFilters(false); // Reload without auto-select
    };
    dd.onchange = async () => {
      // Don't trigger if we're setting the dropdown programmatically
      if (isSettingDropdownProgrammatically) return;
      const filterId = dd.value;
      // Load filter but DON'T auto-start search - only Start Search button should start searches
      await selectFilterByFilterId(filterId, false);
    };
    
    // Restore previous selection if it's still valid, otherwise auto-select first
    if (currentFilterId && savedFilters.some(f => (f.filterId || '') === currentFilterId)) {
      // Restore the previous selection without triggering onchange
      isSettingDropdownProgrammatically = true;
      dd.value = currentFilterId;
      Promise.resolve().then(() => { isSettingDropdownProgrammatically = false; });
    } else if (autoSelect && savedFilters.length > 0) {
      // If first filter exists, select and load (but don't auto-start search on initial load)
      const firstFilterId = savedFilters[0].filterId || (savedFilters[0].filePath ? normalizeToFilterId(savedFilters[0].filePath.replace(/\.(jaml|yaml|yml)$/i, '')) : '');
      if (firstFilterId) {
        await selectFilterByFilterId(firstFilterId, false);
      }
    }
  } catch (e) {
    dd.innerHTML = '<option>Offline / Error</option>';
    setStatus('Failed to load filters: ' + e.message);
  }
}

// Select filter by filterId (preferred method)
async function selectFilterByFilterId(filterId, autoStart = true) {
  const f = savedFilters.find(f => (f.filterId || '') === filterId);
  if (!f) {
    console.warn(`Filter with filterId "${filterId}" not found`);
    return;
  }

  const dd = document.getElementById('filterSelect') || document.getElementById('filtersDropdown');
  // Set flag to prevent onchange from firing when we set the value programmatically
  isSettingDropdownProgrammatically = true;
  try {
    if (dd.value !== filterId) dd.value = filterId;
  } finally {
    // Reset flag after a microtask to ensure the value is set before onchange could fire
    Promise.resolve().then(() => { isSettingDropdownProgrammatically = false; });
  }
  
  // Track current filter and update URL
  currentFilterId = filterId;
  updateUrlWithFilter(filterId);
  
  await loadFilterContent(f, autoStart);
}

// Update URL with current filter (without reloading page)
function updateUrlWithFilter(filterId) {
  if (!filterId) return;
  const url = new URL(window.location.href);
  url.search = ''; // Clear existing params
  url.searchParams.set('filter', filterId);
  window.history.replaceState({}, '', url.toString());
}

// Legacy function for backward compatibility (uses index)
async function selectFilter(idx, autoStart = true) {
  const f = savedFilters[idx];
  if (!f) return;
  
  const filterId = f.filterId || (f.filePath ? normalizeToFilterId(f.filePath.replace(/\.(jaml|yaml|yml)$/i, '')) : '');
  if (filterId) {
    await selectFilterByFilterId(filterId, autoStart);
    return;
  }
  
  // Fallback to old behavior if no filterId
  const dd = document.getElementById('filterSelect') || document.getElementById('filtersDropdown');
  isSettingDropdownProgrammatically = true;
  try {
    if (dd.value !== idx.toString()) dd.value = idx.toString();
  } finally {
    Promise.resolve().then(() => { isSettingDropdownProgrammatically = false; });
  }
  
  await loadFilterContent(f, autoStart);
}

// Shared logic for loading filter content
async function loadFilterContent(f, autoStart = true) {
  if (f.filterJaml) {
    setJamlValue(f.filterJaml);
    // Reset invalidation when loading a filter
    const currentHash = hashFilterStructure(f.filterJaml);
    lastValidFilterHash = currentHash;
    isFilterInvalidated = false;
    
    // Update columns from filter config
    if (f.columns && Array.isArray(f.columns)) {
      columns = f.columns;
      lastColumnStructure = [...f.columns];
    } else {
      updateColumnsFromFilter();
    }
  }

  // Clear current UI results and show loading
  results = [];
  renderResults();
  
  // 1. Instant Fetch of existing results via GET
  const searchId = f.searchId;
  if (searchId) {
    try {
      setStatus('Fetching existing seeds...');
      const r = await fetch(`/search?id=${encodeURIComponent(searchId)}`);
      if (r.ok) {
        const data = await r.json();
        if (data.results && data.results.length > 0) {
          // Normalize results to ensure they have tallies array and no undefined values
          results = data.results.map(r => {
            const seed = (r.seed || r.Seed || '').toString();
            const score = (typeof r.score === 'number' ? r.score : (typeof r.Score === 'number' ? r.Score : 0));
            const tallies = Array.isArray(r.tallies) ? r.tallies : (Array.isArray(r.Tallies) ? r.Tallies : []);
            return {
              seed: (seed && seed !== 'undefined' && seed !== 'null') ? seed : '',
              score: score,
              tallies: tallies
            };
          });
          columns = data.columns || columns;
          renderResults();
          setStatus(`Found ${results.length} existing seeds`);
        }
        // Update search state based on server status
        if (data.status === 'running') {
          searchState = 'RUNNING';
          currentSearchId = searchId;
          document.getElementById('searchBtn').textContent = 'Stop Search';
          document.getElementById('searchBtn').classList.add('button-danger');
          ensureWs();
          // SignalR handles real-time updates
        } else {
          searchState = 'START';
          document.getElementById('searchBtn').textContent = 'Start Search';
          document.getElementById('searchBtn').classList.remove('button-danger');
          // SignalR handles real-time updates
        }
      }
    } catch (e) {
      console.warn('Failed to fetch existing results:', e);
    }
  }

  // 2. Only auto-start if requested (not on initial page load)
  if (autoStart) {
    if (searchState === 'RUNNING') await stopAll();
    await startSearch();
  }
}

async function selectFilterAndRun(idx) {
  await selectFilter(idx, true);
}

async function loadSeedSources() {
  const dd = document.getElementById('seedSourceDropdown');
  if (!dd) {
    console.error('loadSeedSources: seedSourceDropdown element not found');
    return;
  }
  // Preserve current selection before rebuilding dropdown
  const currentValue = dd.value || 'all';
  dd.innerHTML = '<option>Loading...</option>';
  try {
    const r = await fetch('/seed-sources');
    if (!r.ok) throw new Error('seed sources not ok');
    const data = await r.json();
    seedSources = data.sources || [];
    
    if (seedSources.length === 0) {
        dd.innerHTML = '<option value="all">All Seeds (Default)</option>';
        dd.value = currentValue === 'all' ? 'all' : 'all'; // Default to 'all' if no sources
        return;
    }

    dd.innerHTML = '';
    seedSources.forEach(src => {
      const opt = document.createElement('option'); opt.value = src.key; opt.textContent = src.label; dd.appendChild(opt);
    });
    // Restore previous selection if it exists in the new list, otherwise default to 'all'
    if (currentValue && Array.from(dd.options).some(opt => opt.value === currentValue)) {
      dd.value = currentValue;
    } else {
      dd.value = 'all';
    }
  } catch (e) {
    dd.innerHTML = '<option value="all">Offline (Default)</option>';
    dd.value = currentValue === 'all' ? 'all' : 'all'; // Default to 'all' on error
    setStatus('Failed to load sources: ' + e.message);
  }
}

function ensureWs() {
  if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) return;

  if (!signalRConnection) {
    signalRConnection = new signalR.HubConnectionBuilder()
      .withUrl('/searchHub')
      .build();
    
    // Handle result messages (can be string JSON or object)
    signalRConnection.on('Result', (message) => {
      let resultData;
      if (typeof message === 'string') {
        try {
          resultData = JSON.parse(message);
        } catch (e) {
          console.error('Failed to parse SignalR Result message:', e);
          return;
        }
      } else {
        resultData = message;
      }
      
      // Handle different message types
      if (resultData.type === 'filters_changed') {
        // Reload filters when they change (e.g., when a new filter is saved)
        loadFilters(false).catch(e => console.warn('Failed to reload filters:', e));
        return;
      } else if (resultData.type === 'result' && resultData.result) {
        // New result found
        const r = resultData.result || {};
        // Normalize result to ensure consistent property names and no undefined values
        const normalizedResult = {
          seed: (r.seed || r.Seed || '').toString(),
          score: (typeof r.score === 'number' ? r.score : (typeof r.Score === 'number' ? r.Score : 0)),
          tallies: Array.isArray(r.tallies) ? r.tallies : (Array.isArray(r.Tallies) ? r.Tallies : [])
        };
        // Ensure seed is never empty/undefined
        if (!normalizedResult.seed || normalizedResult.seed === 'undefined' || normalizedResult.seed === 'null') {
          normalizedResult.seed = '';
        }
        results.push(normalizedResult);
        columns = resultData.columns || columns;
        renderResults();
      } else if (resultData.type === 'progress') {
        // Progress update - update table for ANY search (not just current)
        const searchId = resultData.searchId;
        if (searchId) {
          const seedsPerSec = resultData.seedsPerSecond || 0;
          const seedsSearched = resultData.seedsSearched || 0;
          const seedsFound = resultData.seedsFound || 0;
          const progress = resultData.totalBatches > 0 
            ? Math.round((resultData.currentBatch / resultData.totalBatches) * 100)
            : 0;
          
          // Update progress table for this search
          updateProgressTable(searchId, {
            status: 'Running',
            progress: `${progress}%`,
            speed: seedsPerSec > 0 ? `${seedsPerSec.toFixed(0)} seeds/sec` : '0 seeds/sec',
            searched: seedsSearched.toLocaleString(),
            found: seedsFound.toString()
          });
          
          // Update status bar if this is the current search
          if (searchId === currentSearchId) {
            const statusText = `${progress}% | ${seedsPerSec.toFixed(0)} seeds/sec | ${seedsSearched.toLocaleString()} searched | ${seedsFound} found`;
            setStatus(statusText);
          }
        }
      } else if (resultData.type === 'search_completed') {
        // Search finished naturally - update table for this search
        const searchId = resultData.searchId;
        if (searchId) {
          const seedsFound = resultData.seedsFound || 0;
          const seedsSearched = resultData.seedsSearched || 0;
          const seedsPerSec = resultData.seedsPerSecond || 0;
          
          // Update progress table with final stats
          updateProgressTable(searchId, {
            status: 'Completed',
            progress: '100%',
            speed: seedsPerSec > 0 ? `${seedsPerSec.toFixed(0)} seeds/sec` : '0 seeds/sec',
            searched: seedsSearched.toLocaleString(),
            found: seedsFound.toString()
          });
          
          // Remove from table after 5 seconds
          setTimeout(() => {
            allSearches.delete(searchId);
            updateProgressTable(searchId, { status: '', progress: '', speed: '', searched: '', found: '' });
          }, 5000);
          
          // Update UI if this is the current search
          if (searchId === currentSearchId) {
            // SignalR handles updates
            searchState = 'START';
            const btn = document.getElementById('searchBtn');
            btn.textContent = 'Start Search';
            btn.classList.remove('button-danger');
            btn.disabled = false;
            setStatus(`Search completed! Found ${seedsFound} seeds from ${seedsSearched.toLocaleString()} searched`);
          }
        }
      } else if (resultData.type === 'search_failed') {
        // Search failed
        if (resultData.searchId === currentSearchId) {
          // SignalR handles updates
          searchState = 'START';
          const btn = document.getElementById('searchBtn');
          btn.textContent = 'Start Search';
          btn.classList.remove('button-danger');
          btn.disabled = false;
          setStatus(`Search failed: ${resultData.error || 'Unknown error'}`);
        }
      } else if (resultData.type === 'search_halted') {
        // Search was stopped
        if (resultData.searchId === currentSearchId) {
          // SignalR handles updates
          searchState = 'START';
          const btn = document.getElementById('searchBtn');
          btn.textContent = 'Start Search';
          btn.classList.remove('button-danger');
          btn.disabled = false;
          setStatus('Search stopped');
        }
      }
    });
    
    signalRConnection.on('Snapshot', (snapshotResults, snapshotColumns) => {
      columns = snapshotColumns || columns;
      // Normalize snapshot results to ensure consistent format
      if (snapshotResults && Array.isArray(snapshotResults)) {
        results = snapshotResults.map(r => {
          const seed = (r.seed || r.Seed || '').toString();
          const score = (typeof r.score === 'number' ? r.score : (typeof r.Score === 'number' ? r.Score : 0));
          const tallies = Array.isArray(r.tallies) ? r.tallies : (Array.isArray(r.Tallies) ? r.Tallies : []);
          return {
            seed: (seed && seed !== 'undefined' && seed !== 'null') ? seed : '',
            score: score,
            tallies: tallies
          };
        });
      } else {
        results = results || [];
      }
      renderResults();
    });
  }
  signalRConnection.start()
    .then(() => {
      // Load all running searches and join their groups
      loadAllSearches();
      if (currentSearchId) {
        signalRConnection.invoke('JoinSearchGroup', currentSearchId);
      }
    })
    .catch(err => console.error('SignalR connection error:', err));
}

// Helper function to extract value from result based on column name
function getValueFromResult(result, col, colIndex) {
  if (!result) return '';
  
  if (col === 'seed') {
    // Handle both camelCase and PascalCase, ensure never undefined
    const seed = result.seed || result.Seed || '';
    return seed !== undefined && seed !== null && seed !== 'undefined' ? seed : '';
  }
  if (col === 'score') {
    // Handle both camelCase and PascalCase
    const score = result.score || result.Score || 0;
    return (typeof score === 'number' && !isNaN(score)) ? score : 0;
  }
  // Custom tally column - access from tallies array
  // Columns are [seed, score, tally1, tally2...], so tally1 is at index 2
  if (result.tallies && colIndex >= 2) {
    const tallyIdx = colIndex - 2;
    const tallies = result.tallies || result.Tallies || [];
    return (tallies[tallyIdx] !== undefined && tallies[tallyIdx] !== null) ? tallies[tallyIdx] : 0;
  }
  return 0; // Default for missing values
}

function renderResults() {
  const container = document.getElementById('resultsGrid');
  if (!columns || columns.length === 0) {
    columns = ['seed', 'score'];
  }
  
  // Prepare data for Tabulator
  const tableData = (results || []).map(r => {
    const row = { _seed: r.seed }; // Store seed for click handler
    columns.forEach((col, idx) => {
      let val = getValueFromResult(r, col, idx);
      if (val === undefined || val === null || val === 'undefined') {
        val = col === 'seed' ? '' : 0;
      }
      row[col] = val;
    });
    return row;
  });
  
  // Define columns for Tabulator
  const tableColumns = columns.map((col, idx) => {
    const canEdit = idx >= 2; // Only should clause columns can be edited
    return {
      title: col,
      field: col,
      sorter: col === 'seed' ? 'string' : 'number',
      headerSort: true,
      headerContextMenu: canEdit ? (e, column) => {
        e.preventDefault();
        editColumnLabel(idx, col);
      } : undefined,
      formatter: (cell, formatterParams) => {
        const val = cell.getValue();
        if (col === 'seed') {
          return `<code>${val || ''}</code>`;
        }
        if (colorModeActive) {
          const cls = getColorClass(val);
          return `<span class="${cls}">${val}</span>`;
        }
        return val;
      },
      cssClass: col === 'seed' ? 'seed-column' : ''
    };
  });
  
  // Add + button column
  tableColumns.push({
    title: '+',
    field: '_add',
    formatter: () => '<button class="add-column-btn" onclick="addColumn()" title="Add new column">+</button>',
    headerSort: false,
    width: 40,
    cssClass: 'add-column-header'
  });
  
  // Destroy existing table if it exists
  if (resultsTable) {
    resultsTable.destroy();
  }
  
  // Create Tabulator instance
  resultsTable = new Tabulator(container, {
    data: tableData,
    columns: tableColumns,
    layout: 'fitColumns',
    initialSort: [{ column: sortCol, dir: sortAsc ? 'asc' : 'desc' }],
    rowClick: (e, row) => {
      const seed = row.getData()._seed;
      if (seed) analyzeSeed(seed);
    },
    cssClass: 'results-table-tabulator'
  });
  
  // Update sort when user clicks header
  resultsTable.on('columnSorted', (column) => {
    sortCol = column.getField();
    sortAsc = column.getSortDirection() === 'asc';
  });
}

function toggleColorMode() {
  colorModeActive = !colorModeActive;
  renderResults();
  setStatus(colorModeActive ? 'Color mode enabled' : 'Color mode disabled');
}

function getColorClass(val) {
  let n = parseFloat(val);
  if (isNaN(n)) return '';
  
  if (n <= 0) return 'color-mode-0';
  if (n === 1) return 'color-mode-1';
  if (n === 2) return 'color-mode-2';
  if (n === 3) return 'color-mode-3';
  if (n === 4) return 'color-mode-4';
  if (n === 5) return 'color-mode-5';
  if (n === 6) return 'color-mode-6';
  if (n === 7) return 'color-mode-7';
  if (n === 8) return 'color-mode-8';
  if (n > 8) return 'color-mode-9';
  return '';
}

function toggleSort(col) {
  console.log('Sorting by', col);
  if (sortCol === col) {
    sortAsc = !sortAsc;
  } else {
    sortCol = col;
    sortAsc = false; // Default desc for new col
  }
  if (resultsTable) {
    resultsTable.setSort(sortCol, sortAsc ? 'asc' : 'desc');
  } else {
    renderResults();
  }
}

function analyzeSeed(seed) {
  switchTab('analyze', document.querySelectorAll('.tab')[1]);
  document.getElementById('analyzeContainer').innerHTML =
    `<iframe src="https://miaklwalker.github.io/Blueprint/?seed=${seed}" style="width: 100%; height: 600px; border: none;"></iframe>`;
}

// Update progress table with real-time data for ALL searches
function updateProgressTable(searchId, data) {
  // Update settings popup table
  const settingsContainer = document.getElementById('settingsProgressTableContainer');
  const settingsTbody = document.getElementById('settingsProgressTableBody');
  const noSearchesMsg = document.getElementById('noActiveSearchesInSettings');
  
  if (!settingsTbody) return;
  
  // If data is empty, remove the search
  if (!data.status && !data.progress && !data.speed && !data.searched && !data.found) {
    allSearches.delete(searchId);
  } else {
    // Store search data
    allSearches.set(searchId, {
      status: data.status || '-',
      progress: data.progress || '0%',
      speed: data.speed || '0 seeds/sec',
      searched: data.searched || '0',
      found: data.found || '0'
    });
  }
  
  // Rebuild table with all searches
  settingsTbody.innerHTML = '';
  allSearches.forEach((searchData, id) => {
    const row = document.createElement('tr');
    const shortId = id.length > 20 ? id.substring(0, 20) + '...' : id;
    row.innerHTML = `
      <td title="${id}">${shortId}</td>
      <td>${searchData.status}</td>
      <td>${searchData.progress}</td>
      <td>${searchData.speed}</td>
      <td>${searchData.searched}</td>
      <td>${searchData.found}</td>
      <td><button onclick="stopSearch('${id}')" class="tool-btn" style="padding: 2px 6px; font-size: 11px;">Stop</button></td>
    `;
    settingsTbody.appendChild(row);
  });
  
  // Show table if we have any searches
  if (allSearches.size > 0) {
    if (settingsContainer) settingsContainer.style.display = 'block';
    if (noSearchesMsg) noSearchesMsg.style.display = 'none';
  } else {
    if (settingsContainer) settingsContainer.style.display = 'none';
    if (noSearchesMsg) noSearchesMsg.style.display = 'block';
  }
}

// Load all running searches on page load
async function loadAllSearches() {
  try {
    const r = await fetch('/search/all');
    if (r.ok) {
      const data = await r.json();
      const runningIds = data.runningSearchIds || [];
      
      // Join all search groups and load their status
      if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) {
        runningIds.forEach(id => {
          signalRConnection.invoke('JoinSearchGroup', id);
          // Load initial status
          fetch(`/search?id=${encodeURIComponent(id)}`)
            .then(res => res.json())
            .then(searchData => {
              if (searchData.status === 'running') {
                updateProgressTable(id, {
                  status: 'Running',
                  progress: `${searchData.progressPercent || 0}%`,
                  speed: `${(searchData.seedsPerSecond || 0).toFixed(0)} seeds/sec`,
                  searched: (searchData.seedsSearched || 0).toLocaleString(),
                  found: (searchData.seedsFound || 0).toString()
                });
              }
            })
            .catch(e => console.warn('Failed to load search status:', e));
        });
      }
    }
  } catch (e) {
    console.warn('Failed to load all searches:', e);
  }
}

function handleSearchClick() {
  const searchBtn = document.getElementById('searchBtn');
  if (!searchBtn || searchBtn.disabled) return;
  toggleSearch();
}

async function toggleSearch() {
  if (searchState === 'RUNNING') { 
    // Update UI immediately for responsiveness
    searchState = 'START';
    const btn = document.getElementById('searchBtn');
    btn.textContent = 'Stopping...';
    btn.disabled = true;
    
    await stopAll(); 
    return; 
  }
  await startSearch();
}

async function startSearch() {
  try {
    const jaml = getJamlValue().trim();
    if (!jaml) { setStatus('Enter a filter'); return; }
    
    // Reset invalidation state when starting new search
    const currentHash = hashFilterStructure(jaml);
    lastValidFilterHash = currentHash;
    isFilterInvalidated = false;
    
    // Get overrides
    const seedSource = document.getElementById('seedSourceDropdown')?.value || 'all';
    
    const r = await fetch('/search', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ 
        filterJaml: jaml, 
        seedCount: 0, 
        seedSource
      })
    });
    const data = await r.json();
    if (!r.ok) { setStatus(`Error: ${data.error || 'unknown'}`); return; }
    currentSearchId = data.searchId;
    // Normalize results to ensure they have tallies array and no undefined values
    results = (data.results || []).map(r => {
      const seed = (r.seed || r.Seed || '').toString();
      const score = (typeof r.score === 'number' ? r.score : (typeof r.Score === 'number' ? r.Score : 0));
      const tallies = Array.isArray(r.tallies) ? r.tallies : (Array.isArray(r.Tallies) ? r.Tallies : []);
      return {
        seed: (seed && seed !== 'undefined' && seed !== 'null') ? seed : '',
        score: score,
        tallies: tallies
      };
    });
    columns = data.columns || columns;
    renderResults();
    ensureWs();
    if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) {
      signalRConnection.invoke('JoinSearchGroup', currentSearchId);
    }
    
    // Load all running searches to show in table
    loadAllSearches();
    searchState = 'RUNNING';
    document.getElementById('searchBtn').textContent = 'Stop Search';
    document.getElementById('searchBtn').classList.add('button-danger');
    setStatus(`Running...`);
    
    // Show progress table and add this search
    updateProgressTable(currentSearchId, {
      status: 'Starting...',
      progress: '0%',
      speed: '0 seeds/sec',
      searched: '0',
      found: '0'
    });
    
    // SignalR handles real-time updates
    
    // Reload filters to pick up the newly saved filter (if it was auto-saved)
    setTimeout(() => {
      loadFilters(false).catch(e => console.warn('Failed to reload filters after search start:', e));
    }, 500); // Small delay to ensure filter is saved on server
  } catch (e) {
    setStatus(`Failed: ${e.message}`);
  }
}

async function stopAll() {
  // Immediate UI feedback
  setStatus('Stopping search...');
  if (currentSearchId) {
    updateProgressTable(currentSearchId, {
      status: 'Stopping...',
      progress: '-',
      speed: '-',
      searched: '-',
      found: '-'
    });
  }
  
  // SignalR handles updates
  
  try {
    const r = await fetch('/search/stop-all', { method: 'POST' });
    if (r.ok) {
      setStatus('Search stopped');
      if (currentSearchId) {
        updateProgressTable(currentSearchId, {
          status: 'Stopped',
          progress: '-',
          speed: '-',
          searched: '-',
          found: '-'
        });
        // Remove from table after a moment
        setTimeout(() => {
          allSearches.delete(currentSearchId);
          updateProgressTable(currentSearchId, { status: '', progress: '', speed: '', searched: '', found: '' });
        }, 2000);
      }
    } else {
      setStatus('Error: Failed to stop search');
    }
  } catch (e) {
    setStatus(`Error stopping: ${e.message}`);
  }
  
  // Ensure UI is in correct state
  searchState = 'START';
  currentSearchId = null;
  const btn = document.getElementById('searchBtn');
  btn.textContent = 'Start Search';
  btn.classList.remove('button-danger');
  btn.disabled = false;
}

// Polling removed - SignalR handles all real-time updates via websocket
function startStatusPolling() {
  // No-op - SignalR handles all real-time updates
}

function stopStatusPolling() {
  // No-op - SignalR handles all real-time updates
}

async function clearResults() {
  // Export to Fertilizer first if we have results
  if (currentSearchId && results.length > 0) {
    try {
      setStatus('Exporting to Fertilizer before clearing...');
      const exportR = await fetch(`/search/${encodeURIComponent(currentSearchId)}/export-to-fertilizer`, {
        method: 'POST'
      });
      
      if (exportR.ok) {
        const exportData = await exportR.json();
        setStatus(`Exported ${exportData.exported || 0} seeds to Fertilizer`);
      } else {
        const error = await exportR.json();
        if (!confirm(`Fertilizer export failed: ${error.error || 'Unknown error'}. Clear results anyway?`)) {
          return; // User cancelled
        }
      }
    } catch (e) {
      if (!confirm(`Fertilizer export error: ${e.message}. Clear results anyway?`)) {
        return; // User cancelled
      }
    }
  }
  
  if (!currentSearchId) {
    // No active search - just clear UI
    results = [];
    isFilterInvalidated = false;
    renderResults();
    setStatus('Results cleared');
    return;
  }
  
  try {
    setStatus('Clearing results...');
    // Save searchId before clearing it (needed for SignalR leave and API call)
    const searchIdToClear = currentSearchId;
    // Call API to delete the database file
    const r = await fetch(`/search/${encodeURIComponent(searchIdToClear)}/results`, {
      method: 'DELETE'
    });
    
    if (r.ok) {
      // Leave SignalR group if connected (before clearing currentSearchId)
      if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected && searchIdToClear) {
        signalRConnection.invoke('LeaveSearchGroup', searchIdToClear).catch(e => console.warn('Failed to leave search group:', e));
      }
      // Clear UI results
      results = [];
      isFilterInvalidated = false;
      // CRITICAL: Clear currentSearchId so phantom seeds don't come back
      // This ensures a new search will create a fresh searchId instead of reusing the old one
      currentSearchId = null;
      // Reset search state
      searchState = 'START';
      // SignalR handles updates
      // Update button state
      const btn = document.getElementById('searchBtn');
      if (btn) {
        btn.textContent = 'Start Search';
        btn.classList.remove('button-danger');
        btn.disabled = false;
      }
      renderResults();
      setStatus('Results cleared from database');
    } else {
      const data = await r.json();
      setStatus(`Error: ${data.error || 'Failed to clear results'}`);
    }
  } catch (e) {
    setStatus(`Error clearing results: ${e.message}`);
    // Still clear UI even if API call fails
    results = [];
    isFilterInvalidated = false;
    // Clear currentSearchId even on error to prevent phantom seeds
    currentSearchId = null;
    searchState = 'START';
    stopStatusPolling();
    const btn = document.getElementById('searchBtn');
    if (btn) {
      btn.textContent = 'Start Search';
      btn.classList.remove('button-danger');
      btn.disabled = false;
    }
    renderResults();
  }
  
  // Re-render icons in case they got cleared
  if (typeof lucide !== 'undefined') {
    lucide.createIcons();
  }
}

function exportCsv() {
  if (!results || results.length === 0) return;
  const headers = columns;
  const csv = [headers.join(','), ...results.map(r => {
    const row = [r.seed, r.score];
    if (r.tallies) r.tallies.forEach(t => row.push(t));
    return row.join(',');
  })].join('\n');
  const blob = new Blob([csv], { type: 'text/csv' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a'); a.href = url; a.download = `results_${Date.now()}.csv`; a.click();
  URL.revokeObjectURL(url);
}

async function saveFilter() {
  const jaml = getJamlValue();
  if (!jaml) { setStatus('Nothing to save'); return; }

  // Check if filter is invalidated and prompt user
  if (isFilterInvalidated && results.length > 0) {
    const response = confirm(
      'Filter structure changed. Results may be outdated.\n\n' +
      'Restart search with new filter?\n\n' +
      'OK = Yes, restart search\n' +
      'Cancel = No, just save'
    );
    
    if (response) {
      // User chose "Yes, restart search"
      // Stop current search if running
      if (currentSearchId && searchState === 'RUNNING') {
        await stopAll();
      }
      
      // Clear results
      results = [];
      isFilterInvalidated = false;
      renderResults();
      
      // Save filter first
      await saveFilterInternal();
      
      // Auto-start new search
      await startSearch();
      return;
    }
    // User chose "No, just save" - continue with save
  }
  
  await saveFilterInternal();
}

async function saveFilterInternal() {
  const jaml = getJamlValue();
  if (!jaml) { setStatus('Nothing to save'); return; }

  const dd = document.getElementById('filterSelect') || document.getElementById('filtersDropdown');
  const filterId = dd.value;
  const filter = savedFilters.find(f => (f.filterId || '') === filterId);

  let filename = filter?.filePath;

  // If it's an unsaved/temp filter, or no filter selected, ask for a name
  if (!filter || !filename || filename.startsWith('_UNSAVED_') || filename.includes('{unsaved}')) {
    showInputModal('Save Filter As', filter?.name || 'NewFilter', async (name) => {
      if (!name) return;
      
      const newFilename = name.endsWith('.jaml') ? name : `${name}.jaml`;
      await performSave(newFilename, jaml);
    });
    return;
  }

  // Regular save
  await performSave(filename, jaml);
}

async function performSave(filename, jaml) {
  try {
    const r = await fetch('/filters/update', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ filterId: filename, filterJaml: jaml })
    });

    if (!r.ok) {
      const err = await r.json();
      throw new Error(err.error || 'Save failed');
    }

    const data = await r.json();
    setStatus(`Saved ${data.filePath}`);
    
    // Reload filters to pick up the changes/new file (don't auto-select first one)
    await loadFilters(false);
    
    // Update dropdown to select the saved filter, but DON'T reload the editor content
    // (keep current edits - the file was already saved with them)
    const savedFilterId = normalizeToFilterId(data.filePath.replace(/\.(jaml|yaml|yml)$/i, ''));
    const savedFilter = savedFilters.find(f => (f.filterId || '') === savedFilterId || f.filePath === data.filePath);
    if (savedFilter) {
      const dd = document.getElementById('filterSelect') || document.getElementById('filtersDropdown');
      const filterId = savedFilter.filterId || savedFilterId;
      if (filterId) {
        isSettingDropdownProgrammatically = true;
        dd.value = filterId;
        Promise.resolve().then(() => { isSettingDropdownProgrammatically = false; });
      }
      // Update the savedFilters entry with current jaml so it matches what's saved
      savedFilter.filterJaml = jaml;
      savedFilter.filePath = data.filePath;
      if (!savedFilter.filterId) {
        savedFilter.filterId = savedFilterId;
      }
      
      // Reset invalidation state after save
      const currentHash = hashFilterStructure(jaml);
      lastValidFilterHash = currentHash;
      isFilterInvalidated = false;
      renderResults();
    }
  } catch (e) {
    setStatus(`Error: ${e.message}`);
  }
}

function shareLink() {
  if (!currentFilterId) { 
    setStatus('Save filter first to share'); 
    return; 
  }
  const url = new URL(window.location.href);
  url.search = '';
  url.searchParams.set('filter', currentFilterId);
  navigator.clipboard.writeText(url.toString());
  setStatus('Filter link copied!');
}

// Input Modal
let inputModalCallback = null;

function showInputModal(title, initialValue, callback, message = null) {
  document.getElementById('inputModalTitle').textContent = title;
  const input = document.getElementById('inputModalValue');
  input.value = initialValue || '';
  
  // Clear errors
  clearInputError();

  const msgEl = document.getElementById('inputModalMessage');
  if (message) {
    msgEl.textContent = message;
    msgEl.style.display = 'block';
  } else {
    msgEl.style.display = 'none';
  }

  inputModalCallback = callback;
  
  const modal = document.getElementById('inputModal');
  modal.style.display = 'flex';
  
  input.focus();
  input.select();
  
  // Bind confirm button
  const confirmBtn = document.getElementById('inputModalConfirm');
  confirmBtn.onclick = () => {
    const val = input.value.trim();
    if (!val) {
      showInputError('Value cannot be empty');
      return;
    }
    // Simple sanitization check
    if (val.match(/[<>:"\/\\|?*]/)) {
      showInputError('Invalid characters in name');
      return;
    }

    if (inputModalCallback) {
      inputModalCallback(val);
      closeInputModal();
    }
  };
}

function showInputError(msg) {
  const errEl = document.getElementById('inputErrorMsg');
  const input = document.getElementById('inputModalValue');
  errEl.textContent = msg;
  errEl.style.display = 'block';
  input.setAttribute('aria-invalid', 'true');
  input.style.borderColor = 'var(--balatro-red)';
}

function clearInputError() {
  const errEl = document.getElementById('inputErrorMsg');
  const input = document.getElementById('inputModalValue');
  errEl.style.display = 'none';
  input.setAttribute('aria-invalid', 'false');
  input.style.borderColor = '';
}

function closeInputModal() {
  document.getElementById('inputModal').style.display = 'none';
  inputModalCallback = null;
}

function handleInputKey(e) {
  if (e.key === 'Enter') {
    document.getElementById('inputModalConfirm').click();
  } else if (e.key === 'Escape') {
    closeInputModal();
  }
}

// Settings
function openSettings() {
  document.getElementById('settingsModal').style.display = 'flex';
}

function closeSettings() {
  document.getElementById('settingsModal').style.display = 'none';
}

function handleBackdrop(e) { 
  if (e.target.id === 'settingsModal') closeSettings();
  if (e.target.id === 'inputModal') closeInputModal();
}

async function renameFilter() {
  const dd = document.getElementById('filterSelect') || document.getElementById('filtersDropdown');
  const filterId = dd.value;
  const filter = savedFilters.find(f => (f.filterId || '') === filterId);
  if (!filter || !filter.filterId) return;

  showInputModal('Rename Filter', filter.name, async (newName) => {
    if (!newName || newName === filter.name) return;

    try {
      // Use filterId (normalized filename) for the API call
      const r = await fetch('/filters/rename', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ filterId: filter.filterId, newName })
      });
      if (!r.ok) throw new Error('Rename failed');
      setStatus(`Renamed to ${newName}`);
      await loadFilters(false);
      // Try to select the renamed one by finding the new filterId (normalized new name)
      const newFilterId = normalizeToFilterId(newName);
      const renamedFilter = savedFilters.find(f => (f.filterId || '') === newFilterId);
      if (renamedFilter) await selectFilterByFilterId(newFilterId, false);
      closeSettings();
    } catch (e) { setStatus(e.message); }
  });
}

async function cloneFilter() {
  const dd = document.getElementById('filterSelect') || document.getElementById('filtersDropdown');
  const filterId = dd.value;
  const filter = savedFilters.find(f => (f.filterId || '') === filterId);
  if (!filter || !filter.filterId) return;

  showInputModal('Clone Filter', filter.name + ' Copy', async (newName) => {
    if (!newName) return;

    try {
      // Use filterId (normalized filename) for the API call
      const r = await fetch('/filters/clone', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ filterId: filter.filterId, newName })
      });
      if (!r.ok) throw new Error('Clone failed');
      setStatus(`Cloned to ${newName}`);
      await loadFilters(false);
      // Try to select the cloned one by finding the new filterId (normalized new name)
      const newFilterId = normalizeToFilterId(newName);
      const clonedFilter = savedFilters.find(f => (f.filterId || '') === newFilterId);
      if (clonedFilter) await selectFilterByFilterId(newFilterId, false);
      closeSettings();
    } catch (e) { setStatus(e.message); }
  });
}

async function deleteFilter(filterId = null) {
  // If no filterId provided, use the currently selected filter
  if (!filterId) {
    const dd = document.getElementById('filterSelect') || document.getElementById('filtersDropdown');
    filterId = dd.value;
  }
  const filter = savedFilters.find(f => (f.filterId || '') === filterId);
  if (!filter || !filter.filterId) return;

  if (!confirm(`Delete "${filter.name}"?`)) return;

  try {
    // Use filterId (normalized filename) for the API call
    const r = await fetch(`/filters/${encodeURIComponent(filter.filterId)}`, { method: 'DELETE' });
    if (!r.ok) throw new Error('Delete failed');
    setStatus(`Deleted ${filter.name}`);
    await loadFilters();
    populateSettingsFilters(); // Refresh the settings list
  } catch (e) { setStatus(e.message); }
}

// Toggle widget settings popup
window.toggleWidgetSettings = function() {
  const popup = document.getElementById('widgetSettingsPopup');
  if (!popup) return;
  
  const isOpen = popup.classList.contains('open');
  if (isOpen) {
    popup.classList.remove('open');
  } else {
    popup.classList.add('open');
    populateSettingsFilters();
    populateSettingsAuthors();
    refreshActiveSearches(); // Refresh running searches
  }
};

// Populate filters list in settings
function populateSettingsFilters() {
  const list = document.getElementById('filtersList');
  if (!list) return;
  
  list.innerHTML = '';
  
  if (savedFilters.length === 0) {
    const empty = document.createElement('div');
    empty.className = 'widget-item';
    empty.style.cursor = 'default';
    empty.innerHTML = '<span class="widget-item-name" style="opacity: 0.6;">No filters</span>';
    list.appendChild(empty);
    return;
  }
  
  // Group by author
  const grouped = {};
  savedFilters.forEach(f => {
    const author = f.author || 'Default';
    if (!grouped[author]) grouped[author] = [];
    grouped[author].push(f);
  });
  
  // Sort authors
  const authors = Object.keys(grouped).sort((a, b) => {
    if (a === 'Default') return -1;
    if (b === 'Default') return 1;
    return a.localeCompare(b);
  });
  
  authors.forEach(author => {
    grouped[author].sort((a, b) => (a.name || '').localeCompare(b.name || ''));
    
    grouped[author].forEach(filter => {
      const item = document.createElement('div');
      item.className = 'widget-item';
      
      const name = document.createElement('span');
      name.className = 'widget-item-name';
      name.textContent = filter.name || filter.filterId || 'Unnamed';
      if (author !== 'Default') {
        name.textContent += ` (${author})`;
      }
      
      const actions = document.createElement('div');
      actions.className = 'widget-item-actions';
      
      const deleteBtn = document.createElement('button');
      deleteBtn.className = 'widget-delete-btn';
      deleteBtn.textContent = '🗑️';
      deleteBtn.title = 'Delete filter';
      deleteBtn.onclick = (e) => {
        e.stopPropagation();
        deleteFilter(filter.filterId);
      };
      
      actions.appendChild(deleteBtn);
      item.appendChild(name);
      item.appendChild(actions);
      list.appendChild(item);
    });
  });
}

// Populate authors list in settings
function populateSettingsAuthors() {
  const list = document.getElementById('authorsList');
  if (!list) return;
  
  list.innerHTML = '';
  
  // Get all unique authors
  const authors = new Set();
  savedFilters.forEach(f => {
    authors.add(f.author || 'Default');
  });
  
  if (authors.size === 0) {
    const empty = document.createElement('div');
    empty.className = 'widget-item';
    empty.style.cursor = 'default';
    empty.innerHTML = '<span class="widget-item-name" style="opacity: 0.6;">No authors</span>';
    list.appendChild(empty);
    return;
  }
  
  const hiddenAuthors = JSON.parse(localStorage.getItem('hiddenFilterAuthors') || '[]');
  const sortedAuthors = Array.from(authors).sort((a, b) => {
    if (a === 'Default') return -1;
    if (b === 'Default') return 1;
    return a.localeCompare(b);
  });
  
  sortedAuthors.forEach(author => {
    const item = document.createElement('div');
    item.className = 'widget-item';
    
    const name = document.createElement('span');
    name.className = 'widget-item-name';
    name.textContent = author === 'Default' ? '(Default)' : author;
    
    const actions = document.createElement('div');
    actions.className = 'widget-item-actions';
    
    const eyeBtn = document.createElement('button');
    eyeBtn.className = 'widget-eye-btn';
    const isHidden = hiddenAuthors.includes(author);
    if (isHidden) {
      eyeBtn.classList.add('hidden');
      eyeBtn.textContent = '👁️‍🗨️';
      eyeBtn.title = 'Show author (click to unhide)';
    } else {
      eyeBtn.textContent = '👁️';
      eyeBtn.title = 'Hide author (click to hide)';
    }
    eyeBtn.onclick = (e) => {
      e.stopPropagation();
      toggleAuthorVisibility(author);
      populateSettingsAuthors(); // Refresh the list
    };
    
    actions.appendChild(eyeBtn);
    item.appendChild(name);
    item.appendChild(actions);
    list.appendChild(item);
  });
}

window.onMonacoReady = async function () {
  // Wait a tick to ensure DOM is fully ready
  setTimeout(() => {
    initStatus(); // Initialize status element
    initTopCenterTab(); // Initialize top center tab drag
    initSplitter();
    initTopGrabber();
    
    // In portrait mode, position JAML editor at top (covering buttons) for mobile keyboard
    // This maximizes code editing space when keyboard appears (~50% of screen)
    const positionJamlAtTop = () => {
      const isPortrait = window.matchMedia('(orientation: portrait)').matches;
      if (isPortrait) {
        const jamlSection = document.getElementById('jamlEditorSection');
        const jamlWrapper = jamlSection?.closest('.section-with-tab');
        if (jamlSection && jamlWrapper) {
          // Position to cover top buttons - tab at 0px, grab bar at 8px, section at 16px
          const topTabHeight = 48;
          jamlWrapper.style.top = `-${topTabHeight}px`;
          jamlWrapper.style.position = 'relative';
          jamlWrapper.style.zIndex = '1001';
          
          // Set initial height to fill available space plus the top tab area
          const parent = jamlWrapper.parentElement;
          if (parent) {
            const availableHeight = parent.offsetHeight;
            const totalHeight = availableHeight + topTabHeight;
            jamlSection.style.flex = `0 0 ${totalHeight}px`;
            jamlSection.style.height = `${totalHeight}px`;
          }
        }
      }
    };
    
    // Position on initial load
    positionJamlAtTop();
    
    // Reposition on orientation change
    window.addEventListener('orientationchange', () => {
      setTimeout(positionJamlAtTop, 100);
    });
    
    // Also check on resize (for responsive testing)
    let resizeTimeout;
    window.addEventListener('resize', () => {
      clearTimeout(resizeTimeout);
      resizeTimeout = setTimeout(positionJamlAtTop, 100);
    });
    
    // Initialize collapsible section tabs
    initCollapsibleSectionTabs();
    
    // Initialize the docking system (replaces old drag functions)
    initDockingSystem();
    
    // Ensure icons are rendered after DOM is ready
    if (typeof lucide !== 'undefined') {
      lucide.createIcons();
    }
    
    // Set a simple default filter if empty - just find any seed
    const filterJaml = document.getElementById('filterJaml');
    if (filterJaml && !filterJaml.value.trim()) {
      filterJaml.value = `name: Find Fun Seeds
deck: Red
stake: White
should:
  - joker: Any`;
    }
    // Ensure plain editor is visible by default (Monaco hidden)
    const mono = document.getElementById('monacoEditor');
    const plain = document.getElementById('filterJaml');
    if (mono && plain) {
      mono.style.display = 'none';
      plain.style.display = 'block';
      monacoMode = false;
    }
  }, 0);
  await loadHealth();
  await loadSeedSources();
  
  // Initial fetch only - SignalR handles updates
  refreshActiveSearches();

  // Check for shared filter link
  const urlParams = new URLSearchParams(window.location.search);
  const sharedFilterId = urlParams.get('filter');

  if (sharedFilterId) {
    // Load filters first, then select the shared one
    await loadFilters(false); // Don't auto-select first filter
    
    const filterExists = savedFilters.some(f => (f.filterId || '') === sharedFilterId);
    if (filterExists) {
      setStatus(`Loading filter: ${sharedFilterId}`);
      await selectFilterByFilterId(sharedFilterId, false); // Don't auto-start
      
      // Check if there's an active search for this filter
      // The searchId is derived from filter's deck/stake, so we check active searches
      try {
        const activeR = await fetch('/searches/active');
        if (activeR.ok) {
          const activeData = await activeR.json();
          const searches = activeData.searches || [];
          // Find a search that matches this filter
          const matchingSearch = searches.find(s => 
            s.searchId && s.searchId.startsWith(sharedFilterId + '_')
          );
          
          if (matchingSearch) {
            currentSearchId = matchingSearch.searchId;
            searchState = 'RUNNING';
            document.getElementById('searchBtn').textContent = 'Stop Search';
            document.getElementById('searchBtn').classList.add('button-danger');
            ensureWs();
            // SignalR handles updates
            
            // Load existing results
            const searchR = await fetch(`/search?id=${encodeURIComponent(matchingSearch.searchId)}`);
            if (searchR.ok) {
              const searchData = await searchR.json();
              results = (searchData.results || []).map(r => {
                const seed = (r.seed || r.Seed || '').toString();
                const score = (typeof r.score === 'number' ? r.score : (typeof r.Score === 'number' ? r.Score : 0));
                const tallies = Array.isArray(r.tallies) ? r.tallies : (Array.isArray(r.Tallies) ? r.Tallies : []);
                return {
                  seed: (seed && seed !== 'undefined' && seed !== 'null') ? seed : '',
                  score: score,
                  tallies: tallies
                };
              });
              columns = searchData.columns || columns;
              renderResults();
              setStatus(`Connected to running search: ${matchingSearch.searchId}`);
            }
          }
        }
      } catch (e) {
        console.warn('Failed to check for active searches:', e);
      }
    } else {
      // Filter not found - clean URL
      setStatus(`Filter "${sharedFilterId}" not found`);
      window.history.replaceState({}, '', window.location.pathname);
      await loadFilters(true); // Fall back to normal load
    }
  } else {
    // No filter in URL - normal load
    await loadFilters(true);
  }
};

// Legacy handler for old ?search= links (redirect to filter if possible)
async function handleLegacySearchLink() {
  const urlParams = new URLSearchParams(window.location.search);
  const sharedSearchId = urlParams.get('search');
  
  if (!sharedSearchId) return false;
  
  try {
    setStatus('Loading search...');
    const r = await fetch(`/search?id=${encodeURIComponent(sharedSearchId)}`);
    if (r.ok) {
      const data = await r.json();
      if (data.filterJaml) setJamlValue(data.filterJaml);
      
      currentSearchId = sharedSearchId;
      // Normalize results to ensure they have tallies array and no undefined values
      results = (data.results || []).map(r => {
        const seed = (r.seed || r.Seed || '').toString();
        const score = (typeof r.score === 'number' ? r.score : (typeof r.Score === 'number' ? r.Score : 0));
        const tallies = Array.isArray(r.tallies) ? r.tallies : (Array.isArray(r.Tallies) ? r.Tallies : []);
        return {
          seed: (seed && seed !== 'undefined' && seed !== 'null') ? seed : '',
          score: score,
          tallies: tallies
        };
      });
      columns = data.columns || columns;
      renderResults();
      
      if (data.status === 'running') {
        searchState = 'RUNNING';
        document.getElementById('searchBtn').textContent = 'Stop Search';
        document.getElementById('searchBtn').classList.add('button-danger');
        ensureWs();
        // SignalR handles updates
      } else {
        searchState = 'START';
        document.getElementById('searchBtn').textContent = 'Start Search';
        document.getElementById('searchBtn').classList.remove('button-danger');
        // SignalR handles updates
      }
      
      // Show performance stats if available
      if (data.seedsPerSecond !== undefined || data.seedsSearched !== undefined) {
        const seedsSearched = data.seedsSearched || 0;
        const seedsPerSec = data.seedsPerSecond || 0;
        const progress = data.progressPercent || 0;
          const seedsFound = data.seedsFound || results.length;
          if (data.status === 'running') {
            setStatus(`Searching... ${seedsSearched.toLocaleString()} seeds | ${seedsPerSec.toFixed(0)} seeds/sec | Found: ${seedsFound} | ${progress}%`);
          } else {
            setStatus(`Search completed! Found ${seedsFound} seeds from ${seedsSearched.toLocaleString()} searched`);
          }
        }
        
        // Load filters and select the matching one
        await loadFilters(false); // Pass false to NOT auto-select first filter
        
        // Find and select the filter that matches the loaded JAML
        if (data.filterJaml) {
          const loadedJaml = data.filterJaml.trim();
          const matchingFilter = savedFilters.find(f => {
            const filterJaml = (f.filterJaml || '').trim();
            return filterJaml === loadedJaml;
          });
          
          if (matchingFilter) {
            // Select the matching filter in dropdown using filterId (not index!)
            const matchingFilterId = matchingFilter.filterId || '';
            const dd = document.getElementById('filterSelect') || document.getElementById('filtersDropdown');
            if (dd && matchingFilterId) {
              isSettingDropdownProgrammatically = true;
              dd.value = matchingFilterId;
              Promise.resolve().then(() => { isSettingDropdownProgrammatically = false; });
            }
          }
        }
        
        return true;
      }
  } catch (e) {
    console.error('Failed to load search', e);
  }
  return false;
}

// New Filter function
function newFilter() {
  const today = new Date().toISOString().split('T')[0];
  const newFilterTemplate = `dateCreated: ${today}
name: New Filter
author: User

must:
  - joker: Blueprint

should:
`;
  setJamlValue(newFilterTemplate);
  setStatus('Created new filter - click Save to save it');
  
  // Clear the filter dropdown selection so it's treated as unsaved
  const dd = document.getElementById('filterSelect');
  if (dd) {
    dd.value = '';
  }
  
  // Reset invalidation for new filter
  const currentHash = hashFilterStructure(newFilterTemplate);
  lastValidFilterHash = currentHash;
  isFilterInvalidated = false;
  
  // Update columns from new filter
  updateColumnsFromFilter();
}

// Edit column label (right-click handler)
async function editColumnLabel(columnIndex, currentLabel) {
  // Column index 0 = seed, 1 = score, 2+ = should clauses
  if (columnIndex < 2) {
    alert('Seed and Score columns cannot be edited');
    return;
  }
  
  const newLabel = prompt(`Edit label for column "${currentLabel}":`, currentLabel);
  if (newLabel === null) return; // User cancelled
  
  const jaml = getJamlValue().trim();
  if (!jaml) return;
  
  // Find the corresponding should clause (columnIndex - 2)
  const shouldClauseIndex = columnIndex - 2;
  
  // Parse JAML to find should clauses and update the label
  // This is a simplified approach - in production might want more robust parsing
  try {
    // Call API to update label
    const r = await fetch('/filters/update-column-label', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ 
        filterJaml: jaml,
        columnIndex: shouldClauseIndex,
        newLabel: newLabel.trim()
      })
    });
    
    if (r.ok) {
      const data = await r.json();
      if (data.filterJaml) {
        setJamlValue(data.filterJaml);
        // Trigger invalidation since structure changed
        await invalidateFilter();
      }
    } else {
      setStatus('Failed to update column label');
    }
  } catch (e) {
    setStatus(`Error updating label: ${e.message}`);
  }
}

// Add new column
async function addColumn() {
  const columnType = prompt('Enter column type (joker, spectralCard, tarotCard, etc.):', 'joker');
  if (!columnType) return;
  
  const columnValue = prompt('Enter column value (card name):', 'Blueprint');
  if (!columnValue) return;
  
  const jaml = getJamlValue().trim();
  if (!jaml) {
    alert('No filter loaded');
    return;
  }
  
  // Add new should clause to JAML
  const newClause = `\nshould:\n  - ${columnType}: ${columnValue}`;
  
  // Simple append - in production might want smarter insertion
  const updatedJaml = jaml + newClause;
  setJamlValue(updatedJaml);
  
  // Trigger invalidation
  await invalidateFilter();
}

// ========== ACTIVE SEARCHES PANEL ==========
// Polling removed - using SignalR

// Fetch and render active searches
async function refreshActiveSearches() {
  try {
    const r = await fetch('/searches/active');
    if (!r.ok) return;
    
    const data = await r.json();
    renderActiveSearches(data.searches || []);
  } catch (e) {
    console.warn('Failed to fetch active searches:', e);
  }
}

function renderActiveSearches(searches) {
  // Update each search in the progress table
  searches.forEach(s => {
    const progressPct = s.totalBatches > 0 
      ? Math.min(100, Math.round((s.completedBatches / s.totalBatches) * 100))
      : 0;
    
    const speedStr = s.seedsPerSecond > 1000000 
      ? `${(s.seedsPerSecond / 1000000).toFixed(1)}M/s`
      : s.seedsPerSecond > 1000 
        ? `${(s.seedsPerSecond / 1000).toFixed(1)}K/s`
        : `${Math.round(s.seedsPerSecond)}/s`;
    
    updateProgressTable(s.searchId, {
      status: s.isFastLane ? '⚡ Running' : (s.inQueue ? '🔄 Queued' : '⏸️ Paused'),
      progress: `${progressPct}%`,
      speed: speedStr,
      searched: s.seedsSearched > 1000000000
        ? `${(s.seedsSearched / 1000000000).toFixed(2)}B`
        : s.seedsSearched > 1000000
          ? `${(s.seedsSearched / 1000000).toFixed(1)}M`
          : s.seedsSearched > 1000
            ? `${(s.seedsSearched / 1000).toFixed(0)}K`
            : s.seedsSearched.toString(),
      found: s.resultsFound.toString()
    });
  });
}

// Stop a search
window.stopSearch = async function(searchId) {
  try {
    const r = await fetch(`/search/${encodeURIComponent(searchId)}/stop`, { method: 'POST' });
    if (!r.ok) throw new Error('Stop failed');
    setStatus(`Stopped search ${searchId.substring(0, 8)}...`);
    updateProgressTable(searchId, { status: '', progress: '', speed: '', searched: '', found: '' });
  } catch (e) {
    setStatus(e.message);
  }
};

// Panic stop a specific search
async function panicStopSearch(searchId) {
  if (!confirm(`Stop search "${searchId}"?`)) return;
  
  try {
    setStatus(`Stopping ${searchId}...`);
    const r = await fetch(`/search/${encodeURIComponent(searchId)}/panic-stop`, { method: 'POST' });
    
    if (r.ok) {
      const data = await r.json();
      setStatus(data.message || 'Search stopped');
      refreshActiveSearches();
    } else {
      const err = await r.json();
      setStatus(`Error: ${err.error || 'Failed to stop'}`);
    }
  } catch (e) {
    setStatus(`Error: ${e.message}`);
  }
}

// Polling removed - SignalR handles active searches updates
function startActiveSearchesPolling() {
  // Initial fetch only - SignalR will handle updates
  refreshActiveSearches();
}

function stopActiveSearchesPolling() {
  // No-op - SignalR handles updates
}

// ==========================================
// Top Center Tab - Drag to resize left/right split
// ==========================================
function initTopCenterTab() {
  const topTab = document.querySelector('.top-center-tab');
  if (!topTab) {
    console.warn('initTopCenterTab: top-center-tab not found');
    return;
  }
  
  const left = document.querySelector('.left-half');
  const container = document.querySelector('.full-split');
  
  if (!left || !container) {
    console.warn('initTopCenterTab: Missing left or container elements');
    return;
  }
  
  // Use Interact.js for smooth horizontal resize
  if (typeof interact === 'undefined') {
    console.warn('Interact.js not loaded, falling back to custom drag');
    return;
  }
  
  interact(topTab)
    .draggable({
      axis: 'x',
      ignoreFrom: '.tab-icon-btn', // Don't drag when clicking buttons
      listeners: {
        start(event) {
          // Only start if not clicking a button
          if (event.target.classList.contains('tab-icon-btn') || 
              event.target.closest('button.tab-icon-btn')) {
            event.stopImmediatePropagation();
            return;
          }
          topTab.classList.add('dragging');
          document.body.style.cursor = 'ew-resize';
        },
        move(event) {
          const rect = container.getBoundingClientRect();
          const deltaX = event.dx;
          const currentWidth = left.offsetWidth;
          let newW = currentWidth + deltaX;
          const maxW = rect.width - 200; // Leave 200px for right half
          
          // Clamp to min/max
          newW = Math.max(200, Math.min(maxW, newW));
          
          // Apply using flex-basis percentage
          const pct = (newW / rect.width) * 100;
          left.style.flex = `0 0 ${pct}%`;
          
          // Update Monaco editor layout
          if (window.jamlEditor) {
            window.jamlEditor.layout();
          }
        },
        end(event) {
          topTab.classList.remove('dragging');
          document.body.style.cursor = '';
          
          // Final layout update
          if (window.jamlEditor) {
            setTimeout(() => window.jamlEditor.layout(), 50);
          }
        }
      }
    });
  
  console.log('initTopCenterTab: Initialized with Interact.js');
}

// ==========================================
// Collapsible Section Tabs
// ==========================================
// Section tabs are now DRAG HANDLES for docking system
// Drag functionality is handled by initDockingSystem
function initCollapsibleSectionTabs() {
  const sectionTabs = document.querySelectorAll('.section-tab');
  
  // Tabs are now drag handles - drag up/down to resize, drag left/right to detach
  // All drag functionality is handled by initDockingSystem
  
  // Initial position update (tabs stay in place, no teleporting)
  updateSectionTabPositions();
}

function updateSectionTabPositions() {
  // Tabs stay in their natural position - NO TELEPORTING, NO FLOATING
  // They are positioned via CSS (top: -16px, absolute positioning relative to .section-with-tab)
  // This function ENFORCES that tabs never become fixed or move
  const sections = document.querySelectorAll('.section-with-tab, .panel-section');
  sections.forEach(section => {
    const tab = section.querySelector('.section-tab');
    if (!tab) return;
    
    // FORCE tabs to stay absolute, never fixed - remove ALL inline positioning
    tab.style.position = '';
    tab.style.top = '';
    tab.style.left = '';
    tab.style.right = '';
    tab.style.bottom = '';
    tab.style.transform = '';
    // Tabs are attached via CSS, not JavaScript
  });
}

// Update positions on orientation/resize change (debounced)
let sectionTabResizeTimeout;
window.addEventListener('resize', () => {
  clearTimeout(sectionTabResizeTimeout);
  sectionTabResizeTimeout = setTimeout(() => {
    if (typeof updateSectionTabPositions === 'function') {
      updateSectionTabPositions();
    }
  }, 150);
});

window.addEventListener('orientationchange', () => {
  setTimeout(() => {
    if (typeof updateSectionTabPositions === 'function') {
      updateSectionTabPositions();
    }
  }, 300);
});
