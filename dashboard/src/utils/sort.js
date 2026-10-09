// Sorting helpers for lists the dashboard shows in pickers and tile grids.

const collator = new Intl.Collator(undefined, { sensitivity: 'base', numeric: true });

/**
 * Returns a copy of items sorted alphabetically (case-insensitive, natural
 * numbers) by the label returned from getLabel, which defaults to the name and
 * falls back to the id.
 */
export function sortByName(items, getLabel = (x) => x?.name || x?.id || x?.Id || '') {
  return [...(items || [])].sort((a, b) => collator.compare(String(getLabel(a)), String(getLabel(b))));
}
