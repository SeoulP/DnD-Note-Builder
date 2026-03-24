---
name:
uses: 0
uses_remaining: 0
type: Class Feature
class: Fighter
species: ""
effect: You have a mind for tactics on and off the battlefield. When you fail an ability check, you can expend a use of your Second Wind to push yourself toward success. Rather than regaining Hit Points, you roll 1d10 and add the number rolled to the ability check, potentially turning it into a success. If the check still fails, this use of Second Wind isn’t expended.
effect_height: 181px
trigger: Fail an Ability Check
cost: 1 Second Wind
level: 1
action: None / Passive
---

```dataviewjs
// ── Options ──────────────────────────────────────────────────
const TYPES     = ["Class Feature","Maneuver","Feat – Origin","Feat – General","Feat – Fighting Style","Feat – Epic Boon","Species Trait"];
const ACTIONS   = ["None / Passive","Action","Bonus Action","Reaction","Free"];
const DURATIONS = ["Instantaneous","Until end of your turn","Until start of your next turn","Until end of target's turn","1 minute","1 hour","8 hours","Until Long Rest","Concentration"];
const RANGES    = ["Self","Touch","5 ft","30 ft","60 ft","120 ft","Sight","Unlimited"];
const CLASSES   = ["Barbarian","Bard","Cleric","Druid","Fighter","Monk","Paladin","Ranger","Rogue","Sorcerer","Warlock","Wizard"];
const SPECIES   = ["Aasimar","Dragonborn","Dwarf","Elf","Gnome","Goliath","Halfling","Human","Orc","Tiefling"];

const fm      = dv.current() ?? {};
const file    = app.workspace.getActiveFile();
const folder  = file.parent.path;
const root    = dv.container;
root.empty();

// ── Frontmatter ───────────────────────────────────────────────
// ── Debounced Save ────────────────────────────────────────────
const saveTimers = {};
function save(field, value) {
  clearTimeout(saveTimers[field]);
  saveTimers[field] = setTimeout(async () => {
    await app.fileManager.processFrontMatter(file, fm => { fm[field] = value; });
  }, 1000); // wait 1 second after last change before writing
}
// ── DOM Helpers ───────────────────────────────────────────────
function section(label) {
  const h = document.createElement("h3");
  h.textContent = label;
  Object.assign(h.style, { margin: "16px 0 4px", color: "#b05050", borderBottom: "1px solid #b05050", paddingBottom: "2px" });
  root.appendChild(h);
}

function row(label, input) {
  const tr  = document.createElement("tr");
  const tdL = document.createElement("td");
  const tdR = document.createElement("td");
  tdL.textContent = label;
  Object.assign(tdL.style, { width: "160px", fontWeight: "bold", padding: "4px 8px", verticalAlign: "top" });
  Object.assign(tdR.style, { padding: "4px 8px", borderBottom: "1px solid #333" });
  tdR.appendChild(input);
  tr.appendChild(tdL);
  tr.appendChild(tdR);
  return tr;
}

function table(...rows) {
  const t = document.createElement("table");
  t.style.width = "100%";
  t.style.borderCollapse = "collapse";
  rows.forEach(r => t.appendChild(r));
  root.appendChild(t);
}

// ── Input Builders ────────────────────────────────────────────
function mkDropdown(field, options, current) {
  const sel = document.createElement("select");
  Object.assign(sel.style, { width: "100%", padding: "4px", borderRadius: "4px", border: "1px solid #555", background: "#1e1e1e", color: "#ddd" });
  const placeholder = document.createElement("option");
  placeholder.value = "";
  placeholder.textContent = "— select —";
  sel.appendChild(placeholder);
  options.forEach(o => {
    const opt = document.createElement("option");
    opt.value = o;
    opt.textContent = o;
    if (o === current) opt.selected = true;
    sel.appendChild(opt);
  });
  sel.addEventListener("change", () => save(field, sel.value));
  return sel;
}

function mkText(field, current, placeholder = "") {
  const input = document.createElement("input");
  input.type        = "text";
  input.value       = current ?? "";
  input.placeholder = placeholder;
  Object.assign(input.style, { width: "100%", padding: "4px", borderRadius: "4px", border: "1px solid #555", background: "#1e1e1e", color: "#ddd" });
  input.addEventListener("change", () => save(field, input.value));
  return input;
}

function mkNumber(field, current) {
  const input = document.createElement("input");
  input.type        = "number";
  input.value       = current ?? "";
  input.placeholder = "—";
  Object.assign(input.style, { width: "80px", padding: "4px", borderRadius: "4px", border: "1px solid #555", background: "#1e1e1e", color: "#ddd" });
  input.addEventListener("change", () => save(field, parseInt(input.value) || 0));
  return input;
}

function mkTextarea(field, current, placeholder = "") {
  const ta = document.createElement("textarea");
  ta.value       = current ?? "";
  ta.placeholder = placeholder;
  ta.rows        = 3;
  Object.assign(ta.style, { width: "100%", padding: "4px", borderRadius: "4px", border: "1px solid #555", background: "#1e1e1e", color: "#ddd", resize: "vertical" });

  // Restore saved height
  const heightKey = field + "_height";
  if (fm[heightKey]) ta.style.height = fm[heightKey];

  // Save content on change
  ta.addEventListener("change", () => save(field, ta.value));

  // Save height on resize (uses ResizeObserver)
  const ro = new ResizeObserver(() => {
    const h = ta.style.height;
    if (h && h !== fm[heightKey]) save(heightKey, h);
  });
  ro.observe(ta);

  return ta;
}

function mkUses(total, remaining) {
  total     = parseInt(total)     || 0;
  remaining = parseInt(remaining) ?? total;

  const wrap     = document.createElement("div");
  const boxWrap  = document.createElement("div");
  const numInput = document.createElement("input");

  Object.assign(wrap.style,    { display: "flex", alignItems: "center", gap: "12px", flexWrap: "wrap" });
  Object.assign(boxWrap.style, { display: "flex", alignItems: "center", gap: "4px", flexWrap: "wrap" });

  numInput.type        = "number";
  numInput.min         = "0";
  numInput.value       = total || "";
  numInput.placeholder = "—";
  Object.assign(numInput.style, { width: "60px", padding: "4px", borderRadius: "4px", border: "1px solid #555", background: "#1e1e1e", color: "#ddd" });

  function buildBoxes(count, rem) {
    while (boxWrap.firstChild) boxWrap.removeChild(boxWrap.firstChild); // ← plain DOM clear
    for (let i = 0; i < count; i++) {
      const cb = document.createElement("input");
      cb.type    = "checkbox";
      cb.checked = i >= rem;
      Object.assign(cb.style, { width: "18px", height: "18px", cursor: "pointer" });
      cb.addEventListener("change", async () => {
        const boxes = Array.from(boxWrap.querySelectorAll("input[type=checkbox]"));
        await save("uses_remaining", boxes.filter(b => !b.checked).length);
      });
      boxWrap.appendChild(cb);
    }
  }

  buildBoxes(total, remaining);

  numInput.addEventListener("change", async () => {
    const newTotal = parseInt(numInput.value) || 0;
    await save("uses", newTotal);
    await save("uses_remaining", newTotal);
    buildBoxes(newTotal, newTotal);
  });

  wrap.appendChild(numInput);
  wrap.appendChild(boxWrap);
  return wrap;
}

// ── Fuzzy Feat Picker ─────────────────────────────────────────
function mkFeatPicker(current) {
  const wrap = document.createElement("div");
  Object.assign(wrap.style, { display: "flex", flexDirection: "column", gap: "6px" });

  // Parse existing links from frontmatter array
  let links = Array.isArray(current) ? [...current] : current ? [current] : [];

  const linkList = document.createElement("div");
  Object.assign(linkList.style, { display: "flex", flexDirection: "column", gap: "4px" });

  function renderLinks() {
    linkList.empty();
    links.forEach((link, i) => {
      const item = document.createElement("div");
      Object.assign(item.style, { display: "flex", alignItems: "center", gap: "6px" });

      const name = document.createElement("span");
      name.textContent = link;
      Object.assign(name.style, { color: "#7d9fd4", flex: "1" });

      const remove = document.createElement("button");
      remove.textContent = "×";
      Object.assign(remove.style, { background: "none", border: "none", color: "#888", cursor: "pointer", fontSize: "16px", lineHeight: "1" });
      remove.addEventListener("click", async () => {
        links.splice(i, 1);
        await save("requires_feats", links);
        renderLinks();
      });

      item.appendChild(name);
      item.appendChild(remove);
      linkList.appendChild(item);
    });
  }

  renderLinks();

  const addBtn = document.createElement("button");
  addBtn.textContent = "+ Add Feat";
  Object.assign(addBtn.style, { width: "fit-content", padding: "4px 10px", borderRadius: "4px", border: "1px solid #555", background: "#1e1e1e", color: "#ddd", cursor: "pointer" });

  addBtn.addEventListener("click", () => {
    // Get all notes in the same folder
    const siblings = app.vault.getMarkdownFiles()
      .filter(f => f.parent.path === folder && f.path !== file.path);

    // Open fuzzy suggester
    const modal = new (class extends obsidian.FuzzySuggestModal {
      getItems()            { return siblings; }
      getItemText(f)        { return f.basename; }
      async onChooseItem(f) {
        const link = "[[" + f.basename + "]]";
        if (!links.includes(link)) {
          links.push(link);
          await save("requires_feats", links);
          renderLinks();
        }
      }
    })(app);

    modal.open();
  });

  wrap.appendChild(linkList);
  wrap.appendChild(addBtn);
  return wrap;
}

// ── Type-conditional rows ─────────────────────────────────────
let conditionalRows = {};

function mkTypeDropdown() {
  const sel = mkDropdown("type", TYPES, fm.type);
  sel.addEventListener("change", () => updateConditional(sel.value));
  return sel;
}

function updateConditional(type) {
  const { classRow, speciesRow, prereqRow, featRow } = conditionalRows;
  classRow.style.display   = ["Class Feature","Maneuver"].includes(type)                                              ? "" : "none";
  speciesRow.style.display = type === "Species Trait"                                                                 ? "" : "none";
  prereqRow.style.display  = ["Feat – Origin","Feat – General","Feat – Fighting Style","Feat – Epic Boon"].includes(type) ? "" : "none";
  featRow.style.display    = ["Feat – Origin","Feat – General","Feat – Fighting Style","Feat – Epic Boon"].includes(type) ? "" : "none";
}

// ── Render ────────────────────────────────────────────────────
section("📋 Identity");

const classRow   = row("Class",         mkDropdown("class",         CLASSES, fm.class));
const speciesRow = row("Species",        mkDropdown("species",       SPECIES, fm.species));
const prereqRow  = row("Prerequisite",   mkText("prerequisite",      fm.prerequisite, "e.g. STR 13+, Level 4…"));
const featRow    = row("Requires Feats", mkFeatPicker(fm.requires_feats));

conditionalRows = { classRow, speciesRow, prereqRow, featRow };

table(
  row("Type",  mkTypeDropdown()),
  row("Level", mkNumber("level", fm.level)),
  classRow,
  speciesRow,
  prereqRow,
  featRow,
);

// Set initial visibility
updateConditional(fm.type ?? "");

section("⚡ Activation");
table(
  row("Action",   mkDropdown("action",  ACTIONS,   fm.action)),
  row("Trigger",  mkText("trigger", fm.trigger, "e.g. When you hit a creature…")),
  row("Cost",     mkText("cost",    fm.cost,    "e.g. 1 Superiority Die…")),
  row("Uses",     mkUses(fm.uses,   fm.uses_remaining)),
  row("Recovery", mkText("recovery", fm.recovery, "e.g. Short Rest, Long Rest, Dawn…")),
);

section("🎯 Mechanics");
table(
  row("Range",    mkDropdown("range",    RANGES,    fm.range)),
  row("Duration", mkDropdown("duration", DURATIONS, fm.duration)),
  row("Scaling",  mkText("scaling", fm.scaling, "e.g. d8 → d10 at level 10…")),
);

section("📖 Details");
table(
  row("Effect", mkTextarea("effect", fm.effect, "What does this feature do?")),
  row("Save",   mkText("save",       fm.save,   "e.g. STR DC 8 + Prof + STR mod — drop held object")),
  row("Notes",  mkTextarea("notes",  fm.notes,  "Interactions, rulings, reminders…")),
);
```
