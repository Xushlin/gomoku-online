import { contextBridge } from 'electron';

/**
 * The entire bridge between the shell and the page.
 *
 * **One read-only string, and no functions.** A function on this bridge is a
 * capability the page keeps forever; a string is a fact it reads once. The page is
 * our own Angular app today, but the bridge is what a compromised dependency inside
 * it would reach for, and "there is nothing here to call" is a much shorter thing to
 * audit than "everything here is safe to call".
 *
 * The value arrives via `additionalArguments` rather than an IPC round trip so that
 * it is available **before** Angular bootstraps — `API_BASE_URL` is read during
 * injector construction, and a promise there would mean the first request goes out
 * against the wrong address.
 */
const PREFIX = '--gewu-server=';

const server = process.argv.find((arg) => arg.startsWith(PREFIX))?.slice(PREFIX.length) ?? '';

contextBridge.exposeInMainWorld('gewuHost', Object.freeze({ apiBaseUrl: server }));
