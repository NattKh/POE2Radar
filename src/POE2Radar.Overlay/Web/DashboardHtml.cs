namespace POE2Radar.Overlay.Web;

public static class DashboardHtml
{
    public const string Page = """
<!DOCTYPE html>
<html><head><meta charset="utf-8"><title>Radar Dashboard</title>
<style>
*{box-sizing:border-box;margin:0;padding:0}
body{background:#1a1a24;color:#e0e0e0;font-family:'Segoe UI',sans-serif;padding:16px;max-width:900px;margin:0 auto}
h1{font-size:18px;color:#78b4ff;margin-bottom:8px}
h2{font-size:14px;color:#78b4ff;margin:12px 0 6px}
.status{background:#252530;padding:8px 12px;border-radius:6px;margin-bottom:12px;font-size:13px;color:#aaa}
.status span{color:#fff}
.tabs{display:flex;gap:4px;margin-bottom:8px;flex-wrap:wrap}
.tab{padding:6px 16px;background:#252530;border:none;color:#aaa;cursor:pointer;border-radius:6px 6px 0 0;font-size:13px}
.tab.active{background:#2a2a3a;color:#78b4ff}
.panel{display:none;background:#2a2a3a;border-radius:0 6px 6px 6px;padding:12px}
.panel.active{display:block}
input[type=text]{background:#1e1e28;border:1px solid #444;color:#fff;padding:4px 8px;border-radius:4px;font-size:13px}
input[type=color]{width:36px;height:24px;border:1px solid #555;background:#1e1e28;cursor:pointer;vertical-align:middle;border-radius:3px}
input[type=number]{background:#1e1e28;border:1px solid #444;color:#fff;padding:4px 8px;border-radius:4px;width:80px;font-size:13px}
input[type=range]{width:150px;vertical-align:middle}
.search{margin-bottom:8px;display:flex;gap:8px;align-items:center}
.search input[type=text]{width:250px}
.filter-btns{display:flex;gap:4px;flex-wrap:wrap;margin-bottom:8px}
.filter-btn{padding:3px 10px;background:#333;border:1px solid #555;color:#ccc;cursor:pointer;border-radius:4px;font-size:12px}
.filter-btn.active{background:#3a5080;border-color:#78b4ff;color:#fff}
table{width:100%;border-collapse:collapse;font-size:12px}
th{text-align:left;padding:4px 8px;background:#1e1e28;color:#78b4ff;position:sticky;top:0}
td{padding:4px 8px;border-bottom:1px solid #333}
tr:hover{background:#333}
tr.watched{background:#2a3a2a}
.scrollbox{max-height:450px;overflow-y:auto}
.btn{padding:3px 10px;border:none;border-radius:4px;cursor:pointer;font-size:12px}
.btn-add{background:#2a5a2a;color:#8f8}.btn-add:hover{background:#3a7a3a}
.btn-rm{background:#5a2a2a;color:#f88}.btn-rm:hover{background:#7a3a3a}
.btn-save{background:#3a5080;color:#9cf;padding:6px 20px;font-size:13px}.btn-save:hover{background:#4a60a0}
.watched-item{display:flex;align-items:center;gap:8px;padding:6px 8px;background:#252530;border-radius:4px;margin-bottom:4px}
.watched-item .pattern{flex:1;font-family:monospace;font-size:11px;color:#999;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.watched-item .label{color:#afc;font-size:13px;font-weight:bold;min-width:100px}
.watched-item .swatch{width:20px;height:20px;border-radius:3px;border:1px solid #555;flex-shrink:0}
.add-form{display:flex;gap:6px;align-items:center;padding:8px;background:#252530;border-radius:6px;margin-bottom:8px;flex-wrap:wrap}
.meta-short{max-width:300px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;font-family:monospace;font-size:11px}
.cat{padding:2px 6px;border-radius:3px;font-size:11px;font-weight:bold}
.cat-Monster{background:#4a2020;color:#f55}.cat-Player{background:#1a3a4a;color:#5cf}
.cat-Npc{background:#4a4a10;color:#fd5}.cat-Chest{background:#4a3010;color:#f90}
.cat-Transition{background:#1a4a2a;color:#6f9}.cat-Other{background:#333;color:#aaa}
.rarity-Magic{color:#79a8ff}.rarity-Rare{color:#ffd926}.rarity-Unique{color:#ff7300}
.auto-refresh{font-size:12px;color:#666;margin-left:auto}
.db-path{font-family:monospace;font-size:11px;color:#bbb}
.db-cat{color:#78b4ff;font-size:11px}
.count{background:#333;padding:2px 6px;border-radius:10px;font-size:11px;color:#aaa;margin-left:4px}
.setting-row{display:flex;align-items:center;gap:10px;margin-bottom:6px;min-height:28px}
.setting-row label{width:160px;font-size:13px;color:#ccc;flex-shrink:0}
.setting-row .val{font-size:12px;color:#78b4ff;width:50px;text-align:right}
.section{background:#252530;border-radius:6px;padding:10px 14px;margin-bottom:10px}
.section h3{font-size:13px;color:#78b4ff;margin-bottom:8px}
.saved{color:#5f5;font-size:12px;opacity:0;transition:opacity 0.3s}
.saved.show{opacity:1}
</style></head><body>
<h1>Radar Dashboard</h1>
<div class="status" id="status">Connecting...</div>

<div class="tabs">
  <button class="tab active" onclick="showTab('entities')">Live Entities</button>
  <button class="tab" onclick="showTab('watched')">Watched</button>
  <button class="tab" onclick="showTab('database')">Database</button>
  <button class="tab" onclick="showTab('settings')">Radar Settings</button>
  <button class="tab" onclick="showTab('rules')">Auto-Skills</button>
  <button class="tab" onclick="showTab('pathing')">Pathing</button>
  <button class="tab" onclick="showTab('landmarks')">Landmarks</button>
</div>

<!-- LIVE ENTITIES -->
<div class="panel active" id="tab-entities">
  <div class="search">
    <input type="text" id="search" placeholder="Search metadata..." oninput="filterEntities()">
    <label><input type="checkbox" id="aliveOnly" onchange="refresh()"> Alive only</label>
    <span class="auto-refresh">Auto-refresh 2s</span>
  </div>
  <div class="filter-btns" id="catFilters"></div>
  <div class="scrollbox"><table><thead>
    <tr><th>Cat</th><th>Rarity</th><th>Metadata</th><th>HP</th><th>Dist</th><th></th></tr>
  </thead><tbody id="entityBody"></tbody></table></div>
</div>

<!-- WATCHED -->
<div class="panel" id="tab-watched">
  <h2>Add Custom Watch</h2>
  <div class="add-form">
    <input type="text" id="addPattern" placeholder="Metadata pattern" style="width:220px">
    <input type="text" id="addLabel" placeholder="Radar nickname" style="width:130px">
    <input type="color" id="addColor" value="#ff5555">
    <button class="btn btn-add" onclick="addWatched()">Add</button>
  </div>
  <p style="font-size:11px;color:#666;margin-bottom:8px">The nickname is displayed on the radar overlay next to the entity dot.</p>
  <div style="display:flex;gap:8px;margin-bottom:10px;align-items:center">
    <h2 style="margin:0">Current Watched</h2>
    <button class="btn btn-save" onclick="exportWatched()" style="margin-left:auto">Export JSON</button>
    <button class="btn btn-add" onclick="$('importFile').click()">Import JSON</button>
    <input type="file" id="importFile" accept=".json" style="display:none" onchange="importWatched(this)">
    <span class="saved" id="importMsg">Imported!</span>
  </div>
  <div id="watchedList"></div>
</div>

<!-- DATABASE -->
<div class="panel" id="tab-database">
  <div class="search">
    <input type="text" id="dbSearch" placeholder="Search all game entities..." style="width:350px" oninput="filterDb()">
    <label style="font-size:12px;color:#aaa"><input type="checkbox" id="dbHideJunk" checked onchange="filterDb()"> Hide junk</label>
    <span id="dbCount" class="count"></span>
  </div>
  <div class="filter-btns" id="dbCatFilters"></div>
  <div class="scrollbox"><table><thead>
    <tr><th>Category</th><th>Path</th><th></th></tr>
  </thead><tbody id="dbBody"></tbody></table></div>
</div>

<!-- RADAR SETTINGS -->
<div class="panel" id="tab-settings">
  <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:10px">
    <h2 style="margin:0">Radar Settings</h2>
    <div><span class="saved" id="savedMsg">Saved!</span> <button class="btn btn-save" onclick="saveSettings()">Save</button></div>
  </div>
  <div id="settingsBody"></div>
</div>

<!-- AUTO-SKILLS -->
<div class="panel" id="tab-rules">
  <div style="display:flex;align-items:center;gap:10px;margin-bottom:10px">
    <h2 style="margin:0">Auto-Skill Rules</h2>
    <button class="btn btn-save" id="rulesToggle" onclick="toggleRules()">Loading...</button>
    <span style="font-size:11px;color:#888">F8 also toggles in-game</span>
  </div>
  <p style="font-size:12px;color:#aaa;margin-bottom:10px">
    Each rule: if ALL conditions are true → press the key. Rules checked every tick. Cooldown prevents spam.
  </p>
  <div id="rulesList"></div>
  <div class="section" style="margin-top:10px">
    <h3>Add Rule</h3>
    <div style="display:flex;gap:6px;flex-wrap:wrap;align-items:center">
      <input type="text" id="ruleAddName" placeholder="Name" style="width:100px">
      <label style="font-size:12px;color:#ccc">Key:</label>
      <input type="text" id="ruleAddKey" placeholder="e.g. Q, 1, R" style="width:50px">
      <label style="font-size:12px;color:#ccc">CD:</label>
      <input type="number" id="ruleAddCd" value="2" min="0.1" max="30" step="0.5" style="width:50px">
      <label style="font-size:12px;color:#ccc">HP&lt;</label>
      <input type="number" id="ruleAddHp" placeholder="-" min="0" max="100" style="width:50px">
      <label style="font-size:12px;color:#ccc">Mana&lt;</label>
      <input type="number" id="ruleAddMana" placeholder="-" min="0" max="100" style="width:50px">
      <label style="font-size:12px;color:#ccc">Enemies&ge;</label>
      <input type="number" id="ruleAddEnemies" placeholder="-" min="0" max="50" style="width:50px">
      <button class="btn btn-add" onclick="addRule()">Add</button>
    </div>
  </div>
</div>

<!-- PATHING -->
<div class="panel" id="tab-pathing">
  <p style="font-size:12px;color:#aaa;margin-bottom:10px">
    The radar auto-paths to the <b>nearest</b> entity matching any enabled target below.<br>
    Press <b>F7</b> in-game to cycle through targets manually.
  </p>
  <div class="add-form">
    <input type="text" id="pathAddPattern" placeholder="Metadata pattern" style="width:200px">
    <input type="text" id="pathAddLabel" placeholder="Label" style="width:120px">
    <button class="btn btn-add" onclick="addPathTarget()">Add</button>
    <button class="btn" style="background:#2a4a5a;color:#5cf;margin-left:auto" onclick="cyclePathTarget()">Cycle Next (F7)</button>
  </div>
  <div id="pathingList" style="margin-top:8px"></div>
</div>

<!-- LANDMARKS -->
<div class="panel" id="tab-landmarks">
  <div class="search"><input type="text" id="lmSearch" placeholder="Search landmarks..." style="width:300px" oninput="filterLandmarks()"></div>
  <div class="scrollbox"><table><thead>
    <tr><th>Name</th><th>Path</th><th>Tiles</th><th>Dist</th></tr>
  </thead><tbody id="lmBody"></tbody></table></div>
</div>

<script>
let entities=[],watched=[],landmarks=[],db=[],settings={},catFilter='',dbCatFilter='';
const $=id=>document.getElementById(id);
const esc=s=>s.replace(/'/g,"\\'").replace(/"/g,'&quot;');

function showTab(name){
  document.querySelectorAll('.tab').forEach(t=>t.classList.remove('active'));
  document.querySelectorAll('.panel').forEach(p=>p.classList.remove('active'));
  [...document.querySelectorAll('.tab')].find(t=>t.textContent.toLowerCase().includes(name.slice(0,4))||t.getAttribute('onclick')?.includes(name))?.classList.add('active');
  $('tab-'+name).classList.add('active');
  if(name==='watched')refreshWatched();
  if(name==='rules')refreshRules();
  if(name==='pathing')refreshPathing();
  if(name==='landmarks')refreshLandmarks();
  if(name==='database'&&db.length===0)loadDb();
  if(name==='settings')loadSettings();
}

// ── LIVE ENTITIES ──
async function refresh(){
  try{
    const s=await(await fetch('/state')).json();
    $('status').innerHTML=s.inGame
      ?`<span>${s.areaCode}</span> (lvl ${s.areaLevel}) | HP ${s.hpPct.toFixed(0)}% | Entities: ${s.entityCount}`
      :'Waiting for in-game...';
    const alive=$('aliveOnly').checked?'&alive=true':'';
    entities=await(await fetch('/entities?limit=1000'+alive)).json();
    renderEntities();
  }catch(e){$('status').textContent='Connection lost';}
}
function renderEntities(){
  const search=$('search').value.toLowerCase();
  const cats=[...new Set(entities.map(e=>e.category))];
  $('catFilters').innerHTML=['All',...cats].map(c=>
    `<button class="filter-btn ${catFilter===(c==='All'?'':c)?'active':''}" onclick="setCat('${c==='All'?'':c}')">${c}</button>`).join('');
  const f=entities.filter(e=>(!catFilter||e.category===catFilter)&&(!search||e.metadata.toLowerCase().includes(search)));
  $('entityBody').innerHTML=f.map(e=>`<tr class="${e.watched?'watched':''}">
    <td><span class="cat cat-${e.category}">${e.category}</span></td>
    <td><span class="rarity-${e.rarity}">${e.rarity}</span></td>
    <td class="meta-short" title="${e.metadata}">${e.metadata}</td>
    <td>${e.hpMax>0?e.hpCur+'/'+e.hpMax:'-'}</td><td>${e.dist}</td>
    <td style="white-space:nowrap">
      ${e.watched?`<button class="btn btn-rm" onclick="rmByMeta('${esc(e.metadata)}')">-</button>`
                 :`<button class="btn btn-add" onclick="quickWatch('${esc(e.metadata)}')">Watch</button>`}
      <button class="btn" style="background:#2a4a5a;color:#5cf" onclick="navigateTo('${esc(e.metadata)}')">Nav</button>
    </td>
  </tr>`).join('');
}
function setCat(c){catFilter=c;renderEntities();}
function filterEntities(){renderEntities();}

async function navigateTo(meta){
  const short=meta.split('/').pop().replace(/@\d+$/,'');
  await fetch('/api/settings',{method:'POST',headers:{'Content-Type':'application/json'},
    body:JSON.stringify({showPath:true,pathTarget:short})});
}

// ── WATCHED ──
async function quickWatch(meta){
  const parts=meta.split('/');const def=parts[parts.length-1].replace(/@\d+$/,'');
  const nick=prompt('Nickname for radar:',def);if(nick===null)return;
  await doAdd(meta,nick||def,$('addColor').value);
}
async function addWatched(){
  const p=$('addPattern').value.trim(),l=$('addLabel').value.trim(),c=$('addColor').value;
  if(!p)return;await doAdd(p,l||p.split('/').pop(),c);$('addPattern').value='';$('addLabel').value='';
}
async function doAdd(pattern,label,color,size=7){
  await fetch('/api/watched',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({pattern,label,color,enabled:true,size})});
  refresh();refreshWatched();
}
async function rmByMeta(meta){const w=watched.find(w=>meta.includes(w.pattern));if(w)await rmWatched(w.pattern);}
async function rmWatched(pattern){await fetch('/api/watched?pattern='+encodeURIComponent(pattern),{method:'DELETE'});refreshWatched();refresh();}
async function refreshWatched(){
  watched=await(await fetch('/api/watched')).json();
  $('watchedList').innerHTML=watched.map(w=>
    `<div class="watched-item">
      <input type="color" value="${w.color}" onchange="editWatched('${esc(w.pattern)}',{color:this.value})" title="Color">
      <input type="text" value="${w.label}" style="width:120px;background:#1e1e28;border:1px solid #444;color:#afc;border-radius:3px;padding:2px 6px;font-size:13px;font-weight:bold"
        onchange="editWatched('${esc(w.pattern)}',{label:this.value})" title="Nickname">
      <input type="number" value="${w.size??7}" min="1" max="30" step="0.5" style="width:55px;background:#1e1e28;border:1px solid #444;color:#78b4ff;border-radius:3px;padding:2px 4px;font-size:12px"
        onchange="editWatched('${esc(w.pattern)}',{size:parseFloat(this.value)})" title="Dot size">
      <div class="pattern" title="${w.pattern}">${w.pattern}</div>
      <label style="font-size:11px;color:#888;white-space:nowrap"><input type="checkbox" ${w.enabled?'checked':''} onchange="editWatched('${esc(w.pattern)}',{enabled:this.checked})"> on</label>
      <button class="btn btn-rm" onclick="rmWatched('${esc(w.pattern)}')">X</button>
    </div>`
  ).join('')||'<div style="color:#666;padding:8px">No watched entities yet.</div>';
}
async function editWatched(pattern,changes){
  const w=watched.find(w=>w.pattern===pattern);if(!w)return;
  const updated={...w,...changes};
  await fetch('/api/watched',{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify(updated)});
  refreshWatched();
}
function exportWatched(){
  const blob=new Blob([JSON.stringify(watched,null,2)],{type:'application/json'});
  const a=document.createElement('a');a.href=URL.createObjectURL(blob);
  a.download='watched_entities.json';a.click();URL.revokeObjectURL(a.href);
}
async function importWatched(input){
  if(!input.files[0])return;
  const text=await input.files[0].text();
  try{
    const r=await fetch('/api/watched/import',{method:'POST',headers:{'Content-Type':'application/json'},body:text});
    const res=await r.json();
    if(res.ok){$('importMsg').textContent=`Imported ${res.imported} entries!`;$('importMsg').classList.add('show');setTimeout(()=>$('importMsg').classList.remove('show'),2000);}
    else alert('Import error: '+res.error);
    refreshWatched();refresh();
  }catch(e){alert('Invalid JSON file');}
  input.value='';
}

// ── DATABASE ──
const JUNK_PATTERNS=['/attachments','monstermods','microtransactions','/timelines/','stashskins','/fx/','/mat/','/ao/','/epk/','/graph/','/audio/','/pet/','/clone/','playersummoned','essencemoddaemons','tormentedspirits','/daemon/','bossroomminimapicon','/environment/','hairstyles','/outfits/','/runemarked'];
function isJunk(p){const l=p.toLowerCase();return JUNK_PATTERNS.some(j=>l.includes(j));}

async function loadDb(){$('dbCount').textContent='Loading...';db=await(await fetch('/api/database')).json();$('dbCount').textContent=db.length+' entities';filterDb();}
function filterDb(){
  const s=($('dbSearch')?.value||'').toLowerCase();const cats=new Set();const hj=$('dbHideJunk')?.checked;
  const f=db.filter(p=>{if(hj&&isJunk(p))return false;if(s&&!p.toLowerCase().includes(s))return false;const c=getCat(p);cats.add(c);return!dbCatFilter||c===dbCatFilter;});
  $('dbCatFilters').innerHTML=['All',...[...cats].sort()].map(c=>
    `<button class="filter-btn ${dbCatFilter===(c==='All'?'':c)?'active':''}" onclick="setDbCat('${c==='All'?'':c}')">${c}</button>`).join('');
  const show=f.slice(0,200);
  $('dbBody').innerHTML=show.map(p=>{const isW=watched.some(w=>p.includes(w.pattern));
    return`<tr class="${isW?'watched':''}"><td><span class="db-cat">${getCat(p)}</span></td>
    <td class="db-path" title="${p}">${p}</td>
    <td>${isW?`<button class="btn btn-rm" onclick="rmByMeta('${esc(p)}')">-</button>`
             :`<button class="btn btn-add" onclick="dbWatch('${esc(p)}')">Watch</button>`}</td></tr>`;
  }).join('')+(f.length>200?`<tr><td colspan=3 style="color:#666">Showing 200/${f.length}. Narrow search.</td></tr>`:'');
  $('dbCount').textContent=f.length+' matches';
}
function setDbCat(c){dbCatFilter=c;filterDb();}
function getCat(p){const parts=p.split('/');return parts.length>=2?parts[1]:'?';}
async function dbWatch(path){const def=path.split('/').pop();const nick=prompt('Nickname for radar:',def);if(nick===null)return;await doAdd(path,nick||def,'#ff5555');filterDb();}

// ── SETTINGS ──
const settingsDef = [
  {section:'Visibility',items:[
    {key:'showMonsters',label:'Normal Monsters',type:'bool'},
    {key:'showRareMonsters',label:'Rare Monsters',type:'bool'},
    {key:'showUniqueMonsters',label:'Unique Monsters',type:'bool'},
    {key:'showNpcs',label:'NPCs',type:'bool'},
    {key:'showChests',label:'Chests',type:'bool'},
    {key:'showTransitions',label:'Transitions',type:'bool'},
    {key:'showPlayers',label:'Other Players',type:'bool'},
    {key:'showLandmarks',label:'Landmarks',type:'bool'},
    {key:'showNameplates',label:'HP Nameplates',type:'bool'},
    {key:'showTerrain',label:'Terrain',type:'bool'},
    {key:'showStatusBar',label:'Status Bar',type:'bool'},
    {key:'showWatchedLabels',label:'Watched Labels',type:'bool'},
  ]},
  {section:'Dot Sizes',items:[
    {key:'monsterDotSize',label:'Normal Monster',type:'num',min:1,max:30,step:0.5},
    {key:'magicDotSize',label:'Magic Monster',type:'num',min:1,max:30,step:0.5},
    {key:'rareDotSize',label:'Rare Monster',type:'num',min:1,max:30,step:0.5},
    {key:'uniqueDotSize',label:'Unique Monster',type:'num',min:1,max:30,step:0.5},
    {key:'npcDotSize',label:'NPC',type:'num',min:1,max:30,step:0.5},
    {key:'chestDotSize',label:'Chest',type:'num',min:1,max:30,step:0.5},
    {key:'transitionDotSize',label:'Transition',type:'num',min:1,max:30,step:0.5},
    {key:'playerDotSize',label:'Player',type:'num',min:1,max:30,step:0.5},
    {key:'watchedDotSize',label:'Watched Entity',type:'num',min:1,max:30,step:0.5},
  ]},
  {section:'Outline',items:[
    {key:'dotOutlineWidth',label:'Dot Outline Width',type:'num',min:0,max:10,step:0.5},
    {key:'dotOutlineColor',label:'Dot Outline Color',type:'color'},
    {key:'landmarkOutlineWidth',label:'Landmark Outline Width',type:'num',min:0,max:10,step:0.5},
  ]},
  {section:'Font Sizes (no limit — scale up for 4K)',items:[
    {key:'statusFontSize',label:'Status Bar',type:'num',min:6,max:72,step:1},
    {key:'landmarkFontSize',label:'Landmarks',type:'num',min:6,max:72,step:1},
    {key:'transitionFontSize',label:'Transitions',type:'num',min:6,max:72,step:1},
    {key:'chestFontSize',label:'Chests',type:'num',min:6,max:72,step:1},
    {key:'watchedFontSize',label:'Watched Labels',type:'num',min:6,max:72,step:1},
    {key:'nameplateFontSize',label:'HP Nameplates',type:'num',min:6,max:72,step:1},
  ]},
  {section:'Colors',items:[
    {key:'monsterColor',label:'Normal Monster',type:'color'},
    {key:'magicColor',label:'Magic Monster',type:'color'},
    {key:'rareColor',label:'Rare Monster',type:'color'},
    {key:'uniqueColor',label:'Unique Monster',type:'color'},
    {key:'npcColor',label:'NPC',type:'color'},
    {key:'chestColor',label:'Chest',type:'color'},
    {key:'transitionColor',label:'Transition',type:'color'},
    {key:'playerColor',label:'Player',type:'color'},
    {key:'landmarkColor',label:'Landmark',type:'color'},
    {key:'watchedColor',label:'Watched Entity',type:'color'},
  ]},
  {section:'Terrain',items:[
    {key:'terrainOpacity',label:'Opacity',type:'num',min:0,max:1,step:0.05},
    {key:'terrainColor',label:'Color',type:'color'},
  ]},
  {section:'Calibration',items:[
    {key:'offsetX',label:'Offset X',type:'num',min:-50,max:50,step:0.5},
    {key:'offsetY',label:'Offset Y',type:'num',min:-50,max:50,step:0.5},
    {key:'scaleMul',label:'Scale',type:'num',min:0.3,max:3,step:0.02},
  ]},
  {section:'Minimap',items:[
    {key:'showMinimap',label:'Show Minimap (when big map closed)',type:'bool'},
    {key:'minimapSize',label:'Size (px)',type:'num',min:100,max:500,step:10},
    {key:'minimapScale',label:'Zoom',type:'num',min:0.1,max:2,step:0.05},
    {key:'minimapOpacity',label:'Opacity',type:'num',min:0.2,max:1,step:0.05},
    {key:'minimapPosition',label:'Corner',type:'text'},
  ]},
  {section:'Exploration Fog',items:[
    {key:'showExplorationFog',label:'Show Unexplored Fog',type:'bool'},
    {key:'fogOpacity',label:'Fog Darkness',type:'num',min:0.1,max:0.9,step:0.05},
  ]},
  {section:'Junk Filter',items:[
    {key:'hideJunkEntities',label:'Hide Junk (attachments, mods, fx, cosmetics)',type:'bool'},
  ]},
  {section:'Pathfinding',items:[
    {key:'showPath',label:'Enable Pathfinding',type:'bool'},
    {key:'pathTarget',label:'Target Pattern',type:'text'},
    {key:'pathColor',label:'Path Color',type:'color'},
    {key:'pathWidth',label:'Path Width',type:'num',min:0.5,max:8,step:0.5},
  ]},
  {section:'Auto-Flask',items:[
    {key:'hpThreshold',label:'HP Threshold %',type:'num',min:5,max:95,step:5},
    {key:'manaThreshold',label:'Mana Threshold %',type:'num',min:5,max:95,step:5},
  ]},
];

async function loadSettings(){
  settings=await(await fetch('/api/settings')).json();
  let html='';
  for(const sec of settingsDef){
    html+=`<div class="section"><h3>${sec.section}</h3>`;
    for(const item of sec.items){
      const v=settings[item.key]??'';
      html+=`<div class="setting-row"><label>${item.label}</label>`;
      if(item.type==='bool')
        html+=`<input type="checkbox" ${v?'checked':''} onchange="setSetting('${item.key}',this.checked)">`;
      else if(item.type==='color')
        html+=`<input type="color" value="${v}" onchange="setSetting('${item.key}',this.value)">`;
      else if(item.type==='text')
        html+=`<input type="text" value="${v||''}" style="width:250px" onchange="setSetting('${item.key}',this.value)" placeholder="e.g. AreaTransition, Waypoint">`;
      else if(item.type==='num')
        html+=`<input type="range" min="${item.min}" max="${item.max}" step="${item.step}" value="${v}"
          oninput="setSetting('${item.key}',parseFloat(this.value));this.nextElementSibling.textContent=this.value">
          <span class="val">${v}</span>`;
      html+=`</div>`;
    }
    html+=`</div>`;
  }
  $('settingsBody').innerHTML=html;
}
function setSetting(key,val){settings[key]=val;}
async function saveSettings(){
  await fetch('/api/settings',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(settings)});
  $('savedMsg').classList.add('show');setTimeout(()=>$('savedMsg').classList.remove('show'),1500);
}

// ── AUTO-SKILLS ──
const VK_NAMES={0x31:'1',0x32:'2',0x33:'3',0x34:'4',0x35:'5',0x51:'Q',0x57:'W',0x45:'E',0x52:'R',0x54:'T'};
function vkName(k){return VK_NAMES[k]||`0x${k.toString(16).toUpperCase()}`;}
function parseKey(s){
  s=s.trim().toUpperCase();
  for(const[k,v]of Object.entries(VK_NAMES))if(v===s)return parseInt(k);
  if(s.length===1)return s.charCodeAt(0);
  if(s.startsWith('0X'))return parseInt(s,16);
  return 0x51;
}

async function refreshRules(){
  const data=await(await fetch('/api/rules')).json();
  $('rulesToggle').textContent=data.enabled?'ON':'OFF';
  $('rulesToggle').style.background=data.enabled?'#2a5a2a':'#5a2a2a';
  $('rulesList').innerHTML=data.rules.map((r,i)=>
    `<div class="watched-item" style="flex-wrap:wrap">
      <label><input type="checkbox" ${r.enabled?'checked':''} onchange="updateRule(${i},{enabled:this.checked})"></label>
      <input type="text" value="${r.name}" style="width:90px;background:#1e1e28;border:1px solid #444;color:#afc;border-radius:3px;padding:2px 6px;font-size:13px;font-weight:bold"
        onchange="updateRule(${i},{name:this.value})">
      <span style="color:#78b4ff;font-size:12px">Key: ${vkName(r.key)}</span>
      <span style="color:#aaa;font-size:11px">CD: ${r.cooldownSec}s</span>
      ${r.hpBelow?`<span style="color:#f88;font-size:11px">HP&lt;${r.hpBelow}%</span>`:''}
      ${r.manaBelow?`<span style="color:#88f;font-size:11px">Mana&lt;${r.manaBelow}%</span>`:''}
      ${r.enemiesNearby?`<span style="color:#ff8;font-size:11px">Enemies&ge;${r.enemiesNearby}</span>`:''}
      <button class="btn btn-rm" style="margin-left:auto" onclick="deleteRule(${i})">X</button>
    </div>`
  ).join('')||'<div style="color:#666;padding:8px">No rules. Add one below.</div>';
}
async function toggleRules(){await fetch('/api/rules/toggle');refreshRules();}
async function updateRule(i,changes){
  const data=await(await fetch('/api/rules')).json();
  const rule={...data.rules[i],...changes};
  await fetch('/api/rules',{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify({index:i,rule})});
  refreshRules();
}
async function deleteRule(i){await fetch('/api/rules?index='+i,{method:'DELETE'});refreshRules();}
async function addRule(){
  const name=$('ruleAddName').value||'Skill';
  const key=parseKey($('ruleAddKey').value||'Q');
  const cd=parseFloat($('ruleAddCd').value)||2;
  const hp=$('ruleAddHp').value?parseFloat($('ruleAddHp').value):null;
  const mana=$('ruleAddMana').value?parseFloat($('ruleAddMana').value):null;
  const enemies=$('ruleAddEnemies').value?parseInt($('ruleAddEnemies').value):null;
  await fetch('/api/rules',{method:'POST',headers:{'Content-Type':'application/json'},
    body:JSON.stringify({name,key,enabled:true,cooldownSec:cd,hpBelow:hp,manaBelow:mana,enemiesNearby:enemies})});
  $('ruleAddName').value='';$('ruleAddKey').value='';refreshRules();
}

// ── PATHING ──
async function refreshPathing(){
  const data=await(await fetch('/api/pathing')).json();
  const targets=data.targets, cur=data.current;
  $('pathingList').innerHTML=targets.map((t,i)=>
    `<div class="watched-item" style="${i===cur?'border-left:3px solid #5cf':'border-left:3px solid transparent'}">
      <label style="font-size:11px;color:#888;white-space:nowrap"><input type="checkbox" ${t.enabled?'checked':''} onchange="editPathTarget('${esc(t.pattern)}',{enabled:this.checked})"> </label>
      <input type="text" value="${t.label}" style="width:120px;background:#1e1e28;border:1px solid #444;color:#afc;border-radius:3px;padding:2px 6px;font-size:13px;font-weight:bold"
        onchange="editPathTarget('${esc(t.pattern)}',{label:this.value})">
      <div class="pattern" title="${t.pattern}">${t.pattern}</div>
      ${i===cur?'<span style="color:#5cf;font-size:11px;font-weight:bold">ACTIVE</span>':''}
      <button class="btn btn-rm" onclick="rmPathTarget('${esc(t.pattern)}')">X</button>
    </div>`
  ).join('')||'<div style="color:#666;padding:8px">No pathing targets. Add patterns above.</div>';
}
async function addPathTarget(){
  const p=$('pathAddPattern').value.trim(),l=$('pathAddLabel').value.trim();
  if(!p)return;
  await fetch('/api/pathing',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({pattern:p,label:l||p,enabled:true})});
  $('pathAddPattern').value='';$('pathAddLabel').value='';refreshPathing();
}
async function editPathTarget(pattern,changes){
  await fetch('/api/pathing',{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify({pattern,...changes})});
  refreshPathing();
}
async function rmPathTarget(pattern){
  await fetch('/api/pathing?pattern='+encodeURIComponent(pattern),{method:'DELETE'});refreshPathing();
}
async function cyclePathTarget(){
  await fetch('/api/pathing/cycle');refreshPathing();
}

// ── LANDMARKS ──
async function refreshLandmarks(){landmarks=await(await fetch('/landmarks')).json();filterLandmarks();}
function filterLandmarks(){
  const s=($('lmSearch')?.value||'').toLowerCase();
  $('lmBody').innerHTML=landmarks.filter(l=>!s||l.name.toLowerCase().includes(s)||l.path.toLowerCase().includes(s))
    .map(l=>`<tr><td>${l.name}</td><td class="meta-short" title="${l.path}">${l.path}</td><td>${l.tiles}</td><td>${l.dist}</td></tr>`).join('');
}

refresh();refreshWatched();setInterval(refresh,2000);
</script></body></html>
""";
}
