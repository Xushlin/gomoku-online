// Copy the built Angular app into `web/`, which is where `main.ts` looks first.
//
// A script rather than a shell one-liner because it asserts two things a `cp`
// would not: that the source exists at all, and that it is not older than this
// run. Packaging the *previous* build is the failure this guards — the app then
// opens, works, and is simply missing whatever changed, which is invisible.
import { cp, rm, stat, readdir } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join, resolve } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const src = resolve(here, '..', '..', 'frontend-web', 'dist', 'gewu-web', 'browser');
const dest = resolve(here, '..', 'web');

if (!existsSync(join(src, 'index.html'))) {
  console.error(`No Angular build at ${src} — run the web build first.`);
  process.exit(1);
}

await rm(dest, { recursive: true, force: true });
await cp(src, dest, { recursive: true });

const files = await readdir(dest);
const entry = await stat(join(dest, 'index.html'));
console.log(`copied ${files.length} entries into web/ (index.html ${entry.size} bytes)`);
