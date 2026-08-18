/**
 * Does this element break a long unbroken word instead of pushing the page sideways?
 *
 * ### Why this is a class-name check, and what that costs
 *
 * The property that actually matters is `overflow-wrap: break-word` (or an
 * equivalent). It cannot be asserted in a unit test: **jsdom has no layout engine
 * and no Tailwind stylesheet**, so `getComputedStyle` reports nothing useful and
 * measuring `scrollWidth` always returns 0. The only thing observable here is the
 * class list — so this pins *the structure that makes the CSS work*, which is the
 * same compromise `score-leaderboard-page.spec.ts` makes for its `overflow-x-auto`
 * scroller.
 *
 * It therefore proves one thing and not another: **it catches the class being
 * deleted, and it cannot catch the stylesheet ceasing to define it.** The second
 * half needs a browser, and a browser run is evidence rather than a guard — it
 * happens when someone remembers, which is precisely how this went unguarded for so
 * long.
 *
 * ### Why a set of names rather than one
 *
 * `break-words`, `break-all` and `wrap-anywhere` all prevent the overflow; which one
 * is right depends on the content. Accepting any of them means a legitimate swap
 * does not produce a false failure, while dropping wrapping altogether still fails.
 * The assertion tracks the intent, not one spelling of it.
 */
const WRAPPING_CLASSES = ['break-words', 'break-all', 'wrap-anywhere'] as const;

/**
 * True when the element carries a utility that breaks long unbroken words.
 *
 * @param element The element that renders user-supplied text.
 */
export function wrapsLongWords(element: Element | null): boolean {
  if (!element) return false;
  return WRAPPING_CLASSES.some((name) => element.classList.contains(name));
}

/** The utilities {@link wrapsLongWords} accepts — for a message that names them. */
export const WRAPPING_UTILITIES: readonly string[] = WRAPPING_CLASSES;
