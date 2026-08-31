/**
 * Makes the app's stylesheet apply immediately, so a strict CSP can stay strict.
 *
 * ## The bug this fixes, as the user saw it
 *
 * The window opened and the app worked — **with no styling at all**. Blue links,
 * default fonts, no layout. Angular's *component* styles still applied (they are
 * injected at runtime), so it was not obviously "no CSS"; it was every global rule
 * missing: Tailwind, the theme tokens, the board skins.
 *
 * ## Why
 *
 * Angular's production build defers the global stylesheet so it does not block
 * first paint:
 *
 * ```html
 * <link rel="stylesheet" href="styles-X.css" media="print" onload="this.media='all'">
 * <noscript><link rel="stylesheet" href="styles-X.css"></noscript>
 * ```
 *
 * It loads as `media="print"` (non-blocking), then an **inline event handler** flips
 * it to `all`. The shell's CSP says `script-src 'self'`, which refuses inline
 * handlers — so the flip never ran and the sheet stayed print-only. Measured in the
 * renderer: `styles-X.css` present with `media: "print"` and 137 rules, none of them
 * applying, and the plain `<link>` sits inside `<noscript>` so it never applies
 * either.
 *
 * ## Why rewrite the HTML instead of loosening the CSP
 *
 * `script-src 'unsafe-inline'` would fix it by permitting *every* inline script, and
 * this is a page that holds a session token. Hashing the handler needs
 * `'unsafe-hashes'` plus a hash that changes with the build.
 *
 * The deferral is a **latency** optimisation, and there is no latency here: the file
 * is on local disk, read by the protocol handler. So the honest fix is to remove the
 * dependency on the inline handler rather than to permit it. The web build keeps the
 * optimisation untouched — this runs only in the shell, at serve time.
 */

/** The exact pattern Angular emits. Measured against the real build output. */
const DEFERRED_LINK = /<link([^>]*?)\s+media="print"\s+onload="this\.media='all'"([^>]*)>/g;

/**
 * Strips `media="print"` and the inline `onload` from deferred stylesheet links.
 *
 * Leaves everything else byte-for-byte alone, and is idempotent — a second pass
 * finds nothing to change.
 */
export function undeferStylesheets(html: string): string {
  return html.replace(DEFERRED_LINK, '<link$1$2>');
}

/** Does this HTML still contain a stylesheet that would need an inline handler? */
export function hasDeferredStylesheet(html: string): boolean {
  return /onload="this\.media='all'"/.test(html);
}
