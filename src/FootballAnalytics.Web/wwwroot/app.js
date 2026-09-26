"use strict";
import { selectableCompetitions } from "./competition-options.mjs";
const $ = id => document.getElementById(id);
const state = { seasons: [], page: 1, displayedSeasonId: null, listVersion: 0, detailVersion: 0, catalogVersion: 0 };
const statuses = ["Programado", "En juego", "Finalizado", "Aplazado", "Cancelado"];
const dateFormat = new Intl.DateTimeFormat("es", { dateStyle: "medium", timeStyle: "short", timeZone: "UTC" });
const date = value => `${dateFormat.format(new Date(value))} UTC`;
const value = number => number == null ? "—" : new Intl.NumberFormat("es").format(number);
function node(tag, text, className) {
  const element = document.createElement(tag);
  if (text != null) element.textContent = text;
  if (className) element.className = className;
  return element;
}
function notice(text = "", error = false) { $("notice").textContent = text; $("notice").classList.toggle("error", error); }
async function get(path) {
  const response = await fetch(`/data/${path}`, { headers: { Accept: "application/json" } });
  if (!response.ok) throw new Error(response.status === 404 ? "El partido ya no está disponible." : "No se pudieron cargar los datos. Comprueba que el servicio esté disponible y vuelve a intentarlo.");
  return response.json();
}
function resetDetail() {
  state.detailVersion++;
  $("detail").setAttribute("aria-busy", "false");
  const title = node("h2", "Elige un partido"); title.id = "detail-title"; title.tabIndex = -1;
  $("detail").replaceChildren(node("p", "UNA MIRADA MÁS CERCA", "eyebrow"), title,
    node("p", "Consulta el resultado y las estadísticas históricas disponibles de cada equipo.", "muted"), node("div", "↗", "empty-symbol"));
}
async function loadCatalog() {
  const version = ++state.catalogVersion;
  state.listVersion++; resetDetail();
  state.displayedSeasonId = null;
  const previousCompetition = $("competition").value;
  const previousSeason = $("season").value;
  $("competition").disabled = $("season").disabled = true;
  $("matches").replaceChildren(); $("matches").setAttribute("aria-busy", "false");
  $("pagination").hidden = true; $("coverage").hidden = true;
  $("identity-warning").hidden = true;
  notice("Cargando competiciones…");
  try {
    const rows = await get("competitions");
    if (version !== state.catalogVersion) return;
    // Only eligible seasons enter the selection state. Source names remain untouched in the API.
    state.seasons = rows.filter(row => row.isSelectable === true);
    $("competition").replaceChildren(); $("season").replaceChildren();
    const competitions = new Map(selectableCompetitions(rows).map(row => [row.competitionId, row]));
    for (const [id, row] of competitions) {
      $("competition").add(new Option(row.competitionName, id));
    }
    const unavailable = new Set(rows.filter(row => row.isSelectable !== true).map(row => row.competitionId)).size;
    if (unavailable) {
      const countLabel = unavailable === 1 ? "1 competición" : `${unavailable} competiciones`;
      $("identity-warning").textContent = `${countLabel} con identidad pendiente de verificación. Los nombres almacenados no son válidos para seleccionar; los registros se conservan sin cambios.`;
      $("identity-warning").hidden = false;
    }
    if (!rows.length) {
      $("competition").add(new Option("Sin competiciones almacenadas", ""));
      $("season").add(new Option("Sin temporadas", ""));
      notice("Todavía no hay temporadas en el archivo. Esta vista sólo consulta los datos existentes."); return;
    }
    if (!state.seasons.length) {
      $("competition").selectedIndex = -1;
      const placeholder = new Option("Sin competiciones seleccionables", "", true, true);
      placeholder.disabled = true; $("competition").prepend(placeholder);
      $("season").add(new Option("Sin temporadas seleccionables", ""));
      notice("Las identidades almacenadas requieren verificación antes de explorar sus partidos."); return;
    }
    $("competition").value = competitions.get(previousCompetition)?.isSelectable === true
      ? previousCompetition : state.seasons[0].competitionId;
    $("competition").disabled = false;
    populateSeasons(previousSeason); await loadMatches(1);
  } catch (error) {
    if (version !== state.catalogVersion) return;
    $("competition").replaceChildren(new Option("Competiciones no disponibles", ""));
    $("season").replaceChildren(new Option("Temporadas no disponibles", ""));
    notice(error.message, true);
  }
}
function populateSeasons(preferred) {
  $("season").replaceChildren();
  for (const row of state.seasons.filter(x => x.competitionId === $("competition").value))
    $("season").add(new Option(row.seasonName, row.seasonId));
  if ([...$("season").options].some(x => x.value === preferred)) $("season").value = preferred;
  $("season").disabled = !$("season").options.length;
}
async function loadMatches(page) {
  const version = ++state.listVersion;
  $("matches").setAttribute("aria-busy", "true");
  const season = state.seasons.find(x => x.seasonId === $("season").value);
  if (!season) { $("matches").setAttribute("aria-busy", "false"); return; }
  // Retain the displayed page during pagination, but never show another season's rows.
  if (state.displayedSeasonId !== season.seasonId) {
    resetDetail(); $("matches").replaceChildren(); $("pagination").hidden = true;
  }
  $("coverage").replaceChildren(); $("coverage").hidden = false;
  for (const [number, label] of [[season.matchCount, "partidos almacenados"], [season.statisticsMatchCount, "con estadísticas"]]) {
    const item = node("div"); item.append(node("strong", value(number)), node("span", label)); $("coverage").append(item);
  }
  notice("Cargando partidos…");
  try {
    const result = await get(`matches?seasonId=${encodeURIComponent(season.seasonId)}&page=${page}`);
    if (version !== state.listVersion) return;
    resetDetail(); $("matches").replaceChildren();
    state.displayedSeasonId = season.seasonId;
    state.page = result.page;
    notice(result.items.length ? "" : "No hay partidos almacenados para esta temporada.");
    for (const match of result.items) {
      const card = node("button", null, "match-card"); card.type = "button";
      card.setAttribute("aria-pressed", "false"); card.setAttribute("aria-controls", "detail");
      const meta = node("div", null, "match-meta"); meta.append(node("span", date(match.kickoffUtc)), node("span", statuses[match.status] ?? "Estado desconocido"));
      const teams = node("div", null, "teams");
      teams.append(node("span", match.homeTeam), node("span", value(match.homeScore), "score"), node("span", match.awayTeam), node("span", value(match.awayScore), "score"));
      const bottom = node("div", null, "match-bottom"); bottom.append(node("span", match.hasStatistics ? "● Estadísticas disponibles" : "Sin estadísticas", match.hasStatistics ? "stats-yes" : ""), node("span", "Ver detalle →"));
      card.append(meta, teams, bottom); card.addEventListener("click", () => loadDetail(match.id, card)); $("matches").append(card);
    }
    $("pagination").hidden = result.page === 1 && !result.hasMore;
    $("previous").disabled = result.page <= 1; $("next").disabled = !result.hasMore;
    $("page-label").textContent = `Página ${result.page}`;
  } catch (error) {
    if (version !== state.listVersion) return;
    notice(error.message, true);
    const retry = node("button", "Reintentar página", "secondary"); retry.type = "button";
    retry.addEventListener("click", () => loadMatches(page));
    $("notice").append(node("span", " "), retry);
  }
  finally { if (version === state.listVersion) $("matches").setAttribute("aria-busy", "false"); }
}
async function loadDetail(id, card) {
  const version = ++state.detailVersion;
  for (const item of document.querySelectorAll(".match-card")) item.setAttribute("aria-pressed", String(item === card));
  $("detail").setAttribute("aria-busy", "true");
  const loading = node("h2", "Cargando partido…"); loading.id = "detail-title";
  $("detail").replaceChildren(loading);
  try {
    const detail = await get(`matches/${id}`);
    if (version !== state.detailVersion) return;
    const match = detail.match;
    const title = node("h2", "Detalle del partido"); title.id = "detail-title"; title.tabIndex = -1;
    const score = node("div", null, "detail-score");
    score.append(node("span", match.homeTeam, "team-name"), node("strong", `${value(match.homeScore)} : ${value(match.awayScore)}`, "big-score"), node("span", match.awayTeam, "team-name"));
    $("detail").replaceChildren(node("p", "EL PARTIDO EN CIFRAS", "eyebrow"), title, score,
      node("p", `${statuses[match.status] ?? "Estado desconocido"} · ${date(match.kickoffUtc)}`, "match-time"),
      node("p", "Datos históricos: se muestra la última observación almacenada de cada fuente. No indica que estas cifras estuvieran disponibles antes del partido. — significa dato no disponible; 0 es un valor registrado.", "context"));
    if (!detail.snapshots.length) $("detail").append(node("p", "No hay estadísticas almacenadas para este partido. El resultado y el calendario pueden estar disponibles por separado.", "muted"));
    for (const snapshot of detail.snapshots) renderSnapshot(snapshot);
    const back = node("button", "← Volver a los partidos", "secondary back-to-matches");
    back.type = "button";
    back.addEventListener("click", () => { card.focus(); card.scrollIntoView({ block: "center" }); });
    $("detail").append(back);
    title.focus({ preventScroll: true });
    if (window.matchMedia("(max-width: 900px)").matches) $("detail").scrollIntoView({ block: "start" });
  } catch (error) {
    if (version !== state.detailVersion) return;
    const title = node("h2", "No se pudo abrir el partido"); title.id = "detail-title";
    const retry = node("button", "Reintentar", "secondary"); retry.type = "button"; retry.addEventListener("click", () => loadDetail(id, card));
    $("detail").replaceChildren(title, node("p", error.message, "muted"), retry);
  } finally { if (version === state.detailVersion) $("detail").setAttribute("aria-busy", "false"); }
}
function renderSnapshot(snapshot) {
  const section = node("section", null, "snapshot");
  section.append(node("h3", snapshot.provider === "statsbomb-open" ? "StatsBomb Open Data" : snapshot.provider));
  section.append(node("p", `Incorporado: ${date(snapshot.ingestedAtUtc)} · Disponibilidad registrada: ${date(snapshot.availableAtUtc)}`, "provenance"));
  const table = node("table"); table.append(node("caption", "Estadísticas por equipo"));
  const head = node("thead"); const header = node("tr");
  const home = snapshot.teams.find(x => x.isHome), away = snapshot.teams.find(x => !x.isHome);
  for (const label of ["Métrica", home?.teamName ?? "Local (sin datos)", away?.teamName ?? "Visitante (sin datos)"]) { const cell = node("th", label); cell.scope = "col"; header.append(cell); }
  head.append(header); table.append(head); const body = node("tbody");
  for (const [key, label] of [["goals", "Goles"], ["shots", "Tiros"], ["shotsOnTarget", "Tiros a puerta"], ["possessionPercentage", "Posesión (%)"], ["corners", "Córners"], ["offsides", "Fueras de juego"], ["fouls", "Faltas"], ["yellowCards", "Tarjetas amarillas"], ["redCards", "Tarjetas rojas"]]) {
    const row = node("tr"), cell = node("th", label); cell.scope = "row";
    row.append(cell, node("td", value(home?.[key])), node("td", value(away?.[key]))); body.append(row);
  }
  table.append(body); const scroll = node("div", null, "table-scroll"); scroll.append(table); section.append(scroll);
  if (snapshot.provider === "statsbomb-open") section.append(node("p", "Fuente: StatsBomb Open Data. Las métricas siguen las definiciones de sus eventos.", "provenance"));
  $("detail").append(section);
}
$("competition").addEventListener("change", () => { populateSeasons(); loadMatches(1); });
$("season").addEventListener("change", () => loadMatches(1));
$("previous").addEventListener("click", () => loadMatches(state.page - 1));
$("next").addEventListener("click", () => loadMatches(state.page + 1));
$("refresh").addEventListener("click", loadCatalog);
loadCatalog();
