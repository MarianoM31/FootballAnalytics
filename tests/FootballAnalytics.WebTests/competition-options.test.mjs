import { test } from "node:test";
import assert from "node:assert/strict";
import { selectableCompetitions } from "../../src/FootballAnalytics.Web/wwwroot/competition-options.mjs";

test("selector excludes unselectable records while preserving API diagnostics and usable names", () => {
  const rows = [
    { competitionId: "unknown", competitionName: "x", isSelectable: false, identityWarning: "Identidad pendiente de verificación" },
    { competitionId: "wc", competitionName: "FIFA World Cup", isSelectable: true },
    { competitionId: "pl", competitionName: "Premier League", isSelectable: true }
  ];
  const original = structuredClone(rows);
  assert.deepEqual(selectableCompetitions(rows).map(x => x.competitionName), ["FIFA World Cup", "Premier League"]);
  assert.deepEqual(rows, original);
});

test("multiple seasons produce one competition option", () => {
  const rows = ["2018", "2022"].map(seasonName => ({
    competitionId: "wc", competitionName: "FIFA World Cup", seasonName, isSelectable: true
  }));
  assert.equal(selectableCompetitions(rows).length, 1);
});

test("empty or entirely ineligible catalogues have no competition options", () => {
  assert.deepEqual(selectableCompetitions([]), []);
  assert.deepEqual(selectableCompetitions([
    { competitionId: "x", isSelectable: false },
    { competitionId: "missing-status" }
  ]), []);
});
