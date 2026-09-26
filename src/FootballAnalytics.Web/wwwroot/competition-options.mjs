// Selection is a presentation concern; retain the full API catalogue for diagnostics.
export function selectableCompetitions(rows) {
  return [...new Map(rows.filter(row => row.isSelectable === true)
    .map(row => [row.competitionId, row])).values()];
}
