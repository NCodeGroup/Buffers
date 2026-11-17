// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

// What the site's components share: the evidence data, the links to it, and the small markup its text fields use.
// A test or source line is linked at the commit evidence.json names, so the line a link opens never moves; a site
// sample is linked on the default branch, where it lands with the page that shows it.
import evidence from '@site/components/data/evidence.json';
import leaks from '@site/components/data/leaks.json';

export { evidence, leaks };

export type Kind = 'asserted' | 'construction' | 'documented' | 'observed' | 'illustration' | 'process';

export interface Ref {
  kind?: Kind;
  test?: string;
  sample?: string;
  source?: string;
  external?: string;
}

export interface Link {
  kind: Kind;
  /** What the link names: a test, a file and lines, a page's title. Shown as a value when `code` is true. */
  label: string;
  /** Where the reference lives, shown before the label: the test class, the file, the site. */
  context?: string;
  href: string;
  code: boolean;
  /** The words a screen reader hears before the label, such as "Test". */
  what: string;
  quote?: string;
  read?: string;
}

const base = import.meta.env.BASE_URL.replace(/\/?$/, '/');
const repo = evidence.repository;
const pinned = `${repo}/blob/${evidence.commit}`;
const branch = `${repo}/blob/main`;

/** A site-absolute path (`/x/`) as the browser needs it, under the site's base path. */
export function sitePath(path: string): string {
  return base + path.replace(/^\//, '');
}

function fail(message: string): never {
  throw new Error(`[site evidence] ${message}`);
}

export function testLink(name: string): Link {
  const test = (evidence.tests as Record<string, { file: string; line: number }>)[name];
  if (!test) fail(`no test "${name}" in docs/site/_components/data/evidence.json`);
  const [type, method] = name.split('.');
  return {
    kind: 'asserted',
    label: method,
    context: type,
    href: `${pinned}/${test.file}#L${test.line}`,
    code: true,
    what: 'Test',
  };
}

export function sampleLink(key: string): Link {
  const sample = (evidence.samples as Record<string, { file: string }>)[key];
  if (!sample) fail(`no sample "${key}" in docs/site/_components/data/evidence.json`);
  return {
    kind: 'asserted',
    label: key,
    context: 'Site sample',
    href: `${branch}/${sample.file}`,
    code: true,
    what: 'Sample test',
  };
}

export function sourceLink(id: string): Link {
  const source = (
    evidence.sources as Record<string, { file: string; lines?: number[]; anchor?: string; branch?: string; kind?: Kind; label?: string }>
  )[id];
  if (!source) fail(`no source "${id}" in docs/site/_components/data/evidence.json`);
  const file = source.file.split('/').pop() ?? source.file;
  const [from, to] = source.lines ?? [];
  const lines = from === undefined ? '' : from === to ? `:${from}` : `:${from}–${to}`;
  const fragment = from === undefined ? (source.anchor ? `#${source.anchor}` : '') : from === to ? `#L${from}` : `#L${from}-L${to}`;
  return {
    // A line of the library is By construction; a build or workflow file is A process; a design record is Documented.
    kind: source.kind ?? 'construction',
    label: source.label ?? `${file}${lines}`,
    context: 'Source',
    // A file that is not on the default branch at the pinned commit yet, such as a decision record of the site, names
    // its branch instead.
    href: `${source.branch ? `${repo}/blob/${source.branch}` : pinned}/${source.file}${fragment}`,
    code: true,
    what: 'Source',
  };
}

export function externalLink(id: string): Link {
  const page = (evidence.external as Record<string, { title: string; url: string; read: string; quote?: string; kind?: Kind; site?: string }>)[id];
  if (!page) fail(`no external page "${id}" in docs/site/_components/data/evidence.json`);
  return {
    // By construction when the library delegates to what the page documents; Documented when it states .NET's behavior.
    kind: page.kind ?? 'construction',
    label: page.title,
    context: page.site ?? 'Microsoft Learn',
    href: page.url,
    code: false,
    what: 'Documentation',
    quote: page.quote,
    read: page.read,
  };
}

export function resolve(ref: Ref): Link {
  const link = ref.test
    ? testLink(ref.test)
    : ref.sample
      ? sampleLink(ref.sample)
      : ref.source
        ? sourceLink(ref.source)
        : ref.external
          ? externalLink(ref.external)
          : fail(`a reference names no test, sample, source or external page: ${JSON.stringify(ref)}`);
  return ref.kind ? { ...link, kind: ref.kind } : link;
}

export type Claim = (typeof evidence.claims)[number];

export function claim(id: string): Claim {
  return evidence.claims.find((c) => c.id === id) ?? fail(`no claim "${id}" in evidence.json`);
}

export function claimHref(id: string): string {
  return sitePath(`/evidence/guarantees/#${claim(id).anchor}`);
}

export type Leak = (typeof leaks.leaks)[number];

export function leak(id: string): Leak {
  return leaks.leaks.find((l) => l.id === id) ?? fail(`no leak card "${id}" in leaks.json`);
}

export function leakHref(id: string): string {
  return sitePath(`/concepts/how-secrets-leak/#${leak(id).anchor}`);
}

export function kindLabel(kind: string): string {
  return (evidence.kinds as Record<string, { label: string }>)[kind]?.label ?? fail(`no kind "${kind}"`);
}

const escape = (text: string) =>
  text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

/** The text fields' markup: `code`, and [text](href), where a site-absolute href gets the base path. */
export function inline(text: string): string {
  return escape(text)
    .replace(/`([^`]+)`/g, '<code>$1</code>')
    .replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, (_, label: string, href: string) => {
      const target = href.startsWith('/') ? sitePath(href) : href;
      return `<a href="${target}">${label}</a>`;
    });
}
