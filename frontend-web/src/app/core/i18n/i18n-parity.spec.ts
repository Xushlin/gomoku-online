import { describe, expect, it } from 'vitest';
import en from '../../../../public/i18n/en.json';
import zhCN from '../../../../public/i18n/zh-CN.json';
import { SUPPORTED_LOCALES } from './supported-locales';

/**
 * Locale-file parity guard. Every supported locale must expose the exact same
 * key set — a key present in one file but not another surfaces in production
 * as a raw `header.x.y` string for some users only, which no component test
 * catches. Runs against the real shipped JSON, not fixtures.
 *
 * Adding a locale? Import its JSON above and add it to TREES — the
 * "covers every supported locale" test fails until you do.
 */
interface TranslationTree {
  [key: string]: string | TranslationTree;
}

const TREES: Record<string, TranslationTree> = {
  en: en as TranslationTree,
  'zh-CN': zhCN as TranslationTree,
};

function flattenKeys(tree: TranslationTree, prefix = ''): Map<string, string> {
  const out = new Map<string, string>();
  for (const [key, value] of Object.entries(tree)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (typeof value === 'string') {
      out.set(path, value);
    } else {
      for (const [k, v] of flattenKeys(value, path)) out.set(k, v);
    }
  }
  return out;
}

describe('i18n locale parity', () => {
  const locales = Object.entries(TREES).map(([locale, tree]) => ({
    locale,
    keys: flattenKeys(tree),
  }));
  const [reference, ...rest] = locales;

  it('covers every supported locale', () => {
    expect(Object.keys(TREES).sort()).toEqual([...SUPPORTED_LOCALES].sort());
  });

  it.each(rest.map((entry) => [entry.locale, entry] as const))(
    `%s has the same key set as ${reference.locale}`,
    (_, entry) => {
      const refKeys = [...reference.keys.keys()].sort();
      const otherKeys = [...entry.keys.keys()].sort();
      expect(otherKeys).toEqual(refKeys);
    },
  );

  it.each(locales.map((entry) => [entry.locale, entry] as const))(
    '%s has no empty translations',
    (_, entry) => {
      const empties = [...entry.keys.entries()]
        .filter(([, value]) => value.trim() === '')
        .map(([key]) => key);
      expect(empties).toEqual([]);
    },
  );

  it.each([
    'header.sound.volume',
    'header.sound-pack.minimal',
    'header.board-skin.midnight',
  ])('both locales translate %s', (key) => {
    for (const entry of locales) {
      expect(entry.keys.get(key), `${entry.locale} missing ${key}`).toBeTruthy();
    }
  });
});
