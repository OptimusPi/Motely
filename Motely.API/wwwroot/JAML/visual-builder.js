// Visual YAML Builder - Dropdown-based editor for JAML
// Mobile-friendly with conditional dropdowns

const itemTypes = {
  'Joker': ['Blueprint', 'Brainstorm', 'Perkeo', 'LuckyCat', 'WeeJoker', 'Showman', 'HangingChad', 'FacelessJoker', 'Egg', 'Any', 'AnyRare', 'AnyUncommon', 'AnyCommon'],
  'SoulJoker': ['Perkeo', 'WeeJoker', 'Any'],
  'Tarot': ['The Fool', 'The Magician', 'The High Priestess', 'The Empress', 'The Emperor', 'The Hierophant', 'The Lovers', 'The Chariot', 'Strength', 'The Hermit', 'Wheel of Fortune', 'Justice', 'The Hanged Man', 'Death', 'Temperance', 'The Devil', 'The Tower', 'The Star', 'The Moon', 'The Sun', 'Judgement', 'The World'],
  'Voucher': ['Telescope', 'Observatory', 'Any'],
  'Planet': ['Mercury', 'Venus', 'Earth', 'Mars', 'Jupiter', 'Saturn', 'Uranus', 'Neptune', 'Pluto', 'Any'],
  'Tag': ['Common', 'Uncommon', 'Rare', 'Negative', 'Polychrome', 'Foil', 'Holo', 'Any'],
  'Boss': ['The Head', 'The Mouth', 'The Eye', 'The Hand', 'The Heart', 'The Arm', 'The Leg', 'The Foot', 'Any'],
  'PlayingCard': ['Any']
};

const decks = ['Red', 'Blue', 'Yellow', 'Green', 'Black', 'Magic', 'Nebula', 'Ghost', 'Abandoned', 'Checkered', 'Zodiac', 'Painted', 'Anaglyph', 'Plasma', 'Erratic', 'Challenge'];
const stakes = ['White', 'Red', 'Green', 'Black', 'Blue', 'Purple', 'Orange', 'Gold'];
const editions = ['Foil', 'Holo', 'Polychrome', 'Negative'];
const antes = [1, 2, 3, 4, 5, 6, 7, 8];

let visualBuilderData = {
  name: '',
  author: '',
  description: '',
  deck: 'Red',
  stake: 'White',
  must: [],
  should: [],
  mustNot: []
};

function toggleVisualBuilder() {
  const visualBuilder = document.getElementById('visualBuilder');
  const plainEditor = document.getElementById('filterJaml');
  const monacoEditor = document.getElementById('monacoEditor');
  
  if (!visualBuilder || !plainEditor) return;
  
  if (visualBuilder.style.display === 'none' || !visualBuilder.style.display) {
    // Switch to visual builder
    plainEditor.style.display = 'none';
    if (monacoEditor) monacoEditor.style.display = 'none';
    if (window.jamlEditor) window.jamlEditor.getDomNode().style.display = 'none';
    
    visualBuilder.style.display = 'block';
    loadJamlIntoVisualBuilder();
    renderVisualBuilder();
  } else {
    // Switch back to text editor
    visualBuilder.style.display = 'none';
    plainEditor.style.display = 'block';
    generateJamlFromVisualBuilder();
  }
}

// Make it globally accessible
window.toggleVisualBuilder = toggleVisualBuilder;

function loadJamlIntoVisualBuilder() {
  const jamlText = document.getElementById('filterJaml').value;
  if (!jamlText.trim()) {
    visualBuilderData = {
      name: '',
      author: '',
      description: '',
      deck: 'Red',
      stake: 'White',
      must: [],
      should: [],
      mustNot: []
    };
    return;
  }
  
  try {
    const parsed = jsyaml.load(jamlText);
    visualBuilderData = {
      name: parsed.name || '',
      author: parsed.author || '',
      description: parsed.description || '',
      deck: parsed.deck || 'Red',
      stake: parsed.stake || 'White',
      must: parsed.must || [],
      should: parsed.should || [],
      mustNot: parsed.mustNot || []
    };
  } catch (e) {
    console.error('Failed to parse JAML:', e);
    // Keep defaults
  }
}

function generateJamlFromVisualBuilder() {
  const jaml = {
    name: visualBuilderData.name || 'New Filter',
    deck: visualBuilderData.deck,
    stake: visualBuilderData.stake
  };
  
  if (visualBuilderData.author) jaml.author = visualBuilderData.author;
  if (visualBuilderData.description) jaml.description = visualBuilderData.description;
  
  // Convert clauses to proper JAML format
  const convertClauses = (clauses) => {
    return clauses.filter(c => c.value).map(clause => {
      const result = {};
      const type = clause.type || 'Joker';
      if (type === 'Joker') result.joker = clause.value;
      else if (type === 'Tarot') result.tarot = clause.value;
      else if (type === 'Voucher') result.voucher = clause.value;
      else if (type === 'SoulJoker') result.soulJoker = clause.value;
      else if (type === 'Planet') result.planet = clause.value;
      else if (type === 'Tag') result.tag = clause.value;
      else if (type === 'Boss') result.boss = clause.value;
      else if (type === 'PlayingCard') result.playingCard = clause.value;
      
      if (clause.edition) result.edition = clause.edition;
      if (clause.antes && clause.antes.length > 0) result.antes = clause.antes;
      return result;
    });
  };
  
  const must = convertClauses(visualBuilderData.must);
  const should = convertClauses(visualBuilderData.should);
  const mustNot = convertClauses(visualBuilderData.mustNot);
  
  if (must.length > 0) jaml.must = must;
  if (should.length > 0) jaml.should = should;
  if (mustNot.length > 0) jaml.mustNot = mustNot;
  
  const yamlText = jsyaml.dump(jaml, { lineWidth: -1, noRefs: true });
  document.getElementById('filterJaml').value = yamlText;
  
  // Sync to Monaco if open
  if (window.jamlEditor) {
    window.jamlEditor.setValue(yamlText);
  }
}

function renderVisualBuilder() {
  const container = document.getElementById('visualBuilder');
  container.innerHTML = `
    <div class="visual-builder-content">
      <div class="vb-section">
        <h3>Filter Info</h3>
        <div class="vb-field">
          <label>Name</label>
          <input type="text" id="vb-name" value="${escapeHtml(visualBuilderData.name)}" placeholder="Filter Name" onchange="updateVisualField('name', this.value)">
        </div>
        <div class="vb-field">
          <label>Author</label>
          <input type="text" id="vb-author" value="${escapeHtml(visualBuilderData.author)}" placeholder="Your Name" onchange="updateVisualField('author', this.value)">
        </div>
        <div class="vb-field">
          <label>Description</label>
          <input type="text" id="vb-description" value="${escapeHtml(visualBuilderData.description)}" placeholder="Optional description" onchange="updateVisualField('description', this.value)">
        </div>
        <div class="vb-field-row">
          <div class="vb-field">
            <label>Deck</label>
            <select id="vb-deck" onchange="updateVisualField('deck', this.value)">
              ${decks.map(d => `<option value="${d}" ${d === visualBuilderData.deck ? 'selected' : ''}>${d}</option>`).join('')}
            </select>
          </div>
          <div class="vb-field">
            <label>Stake</label>
            <select id="vb-stake" onchange="updateVisualField('stake', this.value)">
              ${stakes.map(s => `<option value="${s}" ${s === visualBuilderData.stake ? 'selected' : ''}>${s}</option>`).join('')}
            </select>
          </div>
        </div>
      </div>
      
      <div class="vb-section">
        <div class="vb-section-header">
          <h3>Must Have</h3>
          <button class="vb-add-btn" onclick="addClause('must')">+ Add</button>
        </div>
        <div id="vb-must-list" class="vb-clause-list">
          ${renderClauseList('must', visualBuilderData.must)}
        </div>
      </div>
      
      <div class="vb-section">
        <div class="vb-section-header">
          <h3>Should Have (Scored)</h3>
          <button class="vb-add-btn" onclick="addClause('should')">+ Add</button>
        </div>
        <div id="vb-should-list" class="vb-clause-list">
          ${renderClauseList('should', visualBuilderData.should)}
        </div>
      </div>
      
      <div class="vb-section">
        <div class="vb-section-header">
          <h3>Must Not Have</h3>
          <button class="vb-add-btn" onclick="addClause('mustNot')">+ Add</button>
        </div>
        <div id="vb-mustNot-list" class="vb-clause-list">
          ${renderClauseList('mustNot', visualBuilderData.mustNot)}
        </div>
      </div>
    </div>
  `;
}

function renderClauseList(category, clauses) {
  if (clauses.length === 0) {
    return '<div class="vb-empty">No items yet. Click + Add to add one.</div>';
  }
  
  return clauses.map((clause, index) => renderClause(category, index, clause)).join('');
}

function renderClause(category, index, clause) {
  const type = clause.type || clause.joker ? 'Joker' : clause.tarot ? 'Tarot' : clause.voucher ? 'Voucher' : clause.soulJoker ? 'SoulJoker' : 'Joker';
  const value = clause.value || clause.joker || clause.tarot || clause.voucher || clause.soulJoker || '';
  const edition = clause.edition || '';
  const clauseAntes = clause.antes || [];
  
  const typeOptions = Object.keys(itemTypes).map(t => 
    `<option value="${t}" ${t === type ? 'selected' : ''}>${t}</option>`
  ).join('');
  
  const valueOptions = (itemTypes[type] || []).map(v => 
    `<option value="${v}" ${v === value ? 'selected' : ''}>${v}</option>`
  ).join('');
  
  const antesCheckboxes = antes.map(a => 
    `<label class="vb-checkbox-label">
      <input type="checkbox" value="${a}" ${clauseAntes.includes(a) ? 'checked' : ''} 
             onchange="updateClauseAntes('${category}', ${index}, this.value, this.checked)">
      ${a}
    </label>`
  ).join('');
  
  return `
    <div class="vb-clause" data-category="${category}" data-index="${index}">
      <div class="vb-clause-header">
        <span class="vb-clause-type">${category}</span>
        <button class="vb-remove-btn" onclick="removeClause('${category}', ${index})">×</button>
      </div>
      <div class="vb-clause-fields">
        <div class="vb-field">
          <label>Type</label>
          <select onchange="updateClauseType('${category}', ${index}, this.value)">
            ${typeOptions}
          </select>
        </div>
        <div class="vb-field">
          <label>Item</label>
          <select id="vb-${category}-${index}-value" onchange="updateClauseValue('${category}', ${index}, this.value)">
            <option value="">Select...</option>
            ${valueOptions}
          </select>
        </div>
        ${type === 'Joker' || type === 'SoulJoker' ? `
        <div class="vb-field">
          <label>Edition</label>
          <select onchange="updateClauseField('${category}', ${index}, 'edition', this.value)">
            <option value="">Any</option>
            ${editions.map(e => `<option value="${e}" ${e === edition ? 'selected' : ''}>${e}</option>`).join('')}
          </select>
        </div>
        ` : ''}
        <div class="vb-field">
          <label>Antes</label>
          <div class="vb-checkbox-group">
            ${antesCheckboxes}
          </div>
        </div>
      </div>
    </div>
  `;
}

function updateVisualField(field, value) {
  visualBuilderData[field] = value;
  generateJamlFromVisualBuilder();
}

function addClause(category) {
  visualBuilderData[category].push({
    type: 'Joker',
    value: '',
    antes: []
  });
  renderVisualBuilder();
  generateJamlFromVisualBuilder();
}

function removeClause(category, index) {
  visualBuilderData[category].splice(index, 1);
  renderVisualBuilder();
  generateJamlFromVisualBuilder();
}

function updateClauseType(category, index, type) {
  const clause = visualBuilderData[category][index];
  clause.type = type;
  clause.value = ''; // Reset value when type changes
  
  // Update the value dropdown dynamically - NO FULL RE-RENDER
  const valueSelect = document.getElementById(`vb-${category}-${index}-value`);
  if (valueSelect) {
    const options = (itemTypes[type] || []).map(v => 
      `<option value="${v}">${v}</option>`
    ).join('');
    valueSelect.innerHTML = '<option value="">Select...</option>' + options;
    valueSelect.value = ''; // Reset selection
  }
  
  // Show/hide edition field for Joker/SoulJoker
  const clauseEl = document.querySelector(`[data-category="${category}"][data-index="${index}"]`);
  if (clauseEl) {
    const editionField = clauseEl.querySelector('.vb-field:has(select[onchange*="edition"])');
    if (editionField) {
      editionField.style.display = (type === 'Joker' || type === 'SoulJoker') ? 'block' : 'none';
    }
  }
  
  generateJamlFromVisualBuilder();
}

function updateClauseValue(category, index, value) {
  const clause = visualBuilderData[category][index];
  const type = clause.type || 'Joker';
  
  // Convert to proper format based on type
  delete clause.joker;
  delete clause.tarot;
  delete clause.voucher;
  delete clause.soulJoker;
  
  clause.type = type;
  clause.value = value;
  
  // NO RE-RENDER - just update JAML
  generateJamlFromVisualBuilder();
}

function updateClauseField(category, index, field, value) {
  const clause = visualBuilderData[category][index];
  if (value) {
    clause[field] = value;
  } else {
    delete clause[field];
  }
  generateJamlFromVisualBuilder();
}

function updateClauseAntes(category, index, ante, checked) {
  const clause = visualBuilderData[category][index];
  if (!clause.antes) clause.antes = [];
  const anteNum = parseInt(ante);
  if (checked) {
    if (!clause.antes.includes(anteNum)) clause.antes.push(anteNum);
  } else {
    clause.antes = clause.antes.filter(a => a !== anteNum);
  }
  clause.antes.sort();
  generateJamlFromVisualBuilder();
}

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

