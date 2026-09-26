import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";

// Exercise the actual page loader with a small DOM stand-in and controlled HTTP responses.
const source = readFileSync(new URL("../../src/FootballAnalytics.Web/wwwroot/app.js", import.meta.url), "utf8")
  .replace(/^import .*;\r?\n/m, "")
  .replace(/loadCatalog\(\);\s*$/, "");
function harness() {
  class Element {
    children = []; hidden = false; disabled = false; attributes = {}; listeners = {};
    classList = { toggle() {} };
    set textContent(text) { this.text = text; this.children = []; }
    get textContent() { return this.text ?? ""; }
    append(...children) { this.children.push(...children); }
    replaceChildren(...children) { this.children = children; }
    setAttribute(key, value) { this.attributes[key] = value; }
    addEventListener(event, handler) { this.listeners[event] = handler; }
  }
  const elements = new Map();
  const get = id => { if (!elements.has(id)) elements.set(id, new Element()); return elements.get(id); };
  const responses = [];
  const requests = [];
  const context = vm.createContext({
    document: { getElementById: get, createElement: () => new Element() },
    fetch: async url => { requests.push(url); return responses.shift()(); },
    Intl, Date, Map, Set, encodeURIComponent
  });
  vm.runInContext(source, context);
  vm.runInContext('state.seasons = [{ seasonId: "a", matchCount: 50, statisticsMatchCount: 0 }, { seasonId: "b", matchCount: 1, statisticsMatchCount: 0 }]', context);
  get("season").value = "a";
  const page = (number, more = true) => ({ ok: true, json: async () => ({ page: number, hasMore: more, items: [
    { id: `match-${number}`, homeTeam: "Home", awayTeam: "Away", kickoffUtc: "2020-01-01T00:00:00Z", status: 2, homeScore: null, awayScore: 0 }
  ] }) });
  return { get, responses, requests, page, run: code => vm.runInContext(code, context) };
}

test("failed next page preserves rows, label and navigation; retry requests the failed page", async () => {
  const h = harness();
  h.responses.push(() => h.page(2));
  await h.run("loadMatches(2)");
  const card = h.get("matches").children[0];
  h.responses.push(() => ({ ok: false, status: 503 }));
  await h.run("loadMatches(3)");
  assert.equal(h.get("matches").children[0], card);
  assert.equal(h.get("page-label").textContent, "Página 2");
  assert.equal(h.get("pagination").hidden, false);
  assert.equal(h.get("previous").disabled, false);
  assert.equal(h.get("next").disabled, false);
  assert.equal(h.get("matches").attributes["aria-busy"], "false");
  const retry = h.get("notice").children.find(x => x.textContent === "Reintentar página");
  assert.ok(retry);
  h.responses.push(() => h.page(3, false));
  await retry.listeners.click();
  assert.equal(h.requests.at(-1), "/data/matches?seasonId=a&page=3");
  assert.notEqual(h.get("matches").children[0], card);
  assert.equal(h.get("page-label").textContent, "Página 3");
  assert.equal(h.get("next").disabled, true);
  assert.equal(h.get("notice").children.length, 0);
});

test("failed season change clears old rows and allows retry of the newly selected season", async () => {
  const h = harness();
  h.responses.push(() => h.page(1));
  await h.run("loadMatches(1)");
  h.get("season").value = "b";
  h.responses.push(() => { throw new Error("offline"); });
  await h.run("loadMatches(1)");
  assert.equal(h.get("matches").children.length, 0);
  assert.equal(h.get("pagination").hidden, true);
  const retry = h.get("notice").children.find(x => x.textContent === "Reintentar página");
  h.responses.push(() => h.page(1, false));
  await retry.listeners.click();
  assert.equal(h.requests.at(-1), "/data/matches?seasonId=b&page=1");
  assert.equal(h.get("matches").children.length, 1);
});
