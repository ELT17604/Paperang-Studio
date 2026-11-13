import assert from 'node:assert/strict';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { load } from 'cheerio';
import { groups } from './catalog.mjs';

const dist = fileURLToPath(new URL('./dist/', import.meta.url));
const files = new Set(readdirSync(dist, { recursive: true }).filter(file => statSync(path.join(dist, file)).isFile()));
const pages = new Map([...files].filter(file => file.endsWith('.html')).map(file => [file, load(readFileSync(path.join(dist, file), 'utf8'))]));
const origin = 'https://wiki.local';
const errors = new Set();

for (const [file, $] of pages) {
	const url = new URL('/' + file, origin);
	assert.equal($('main h1').length, 1, file);
	$('a[href], img[src], link[href], script[src]').each((_, node) => {
		const target = new URL($(node).attr('href') ?? $(node).attr('src'), url);
		if (target.origin !== origin)
			return;
		const name = decodeURI(target.pathname.slice(1));
		const destination = name.endsWith('/') || !name ? name + 'index.html' : name;
		if (!files.has(destination)) {
			errors.add(`${file} → ${target.pathname}`);
			return;
		}
		const id = decodeURIComponent(target.hash.slice(1));
		if (id && pages.has(destination) && !pages.get(destination)('[id]').toArray().some(el => el.attribs.id === id))
			errors.add(`${file} → ${target.pathname}#${id}`);
	});
}

for (const lang of ['en', 'zh-CN']) {
	const index = JSON.parse(readFileSync(path.join(dist, lang, 'search.json'), 'utf8'));
	const topics = groups.flatMap(group => group[3]);
	assert.equal(index.length, topics.length);
	for (const [slug] of topics) {
		const article = pages.get(`${lang}/${slug}/index.html`);
		const other = lang === 'en' ? 'zh-CN' : 'en';
		assert.equal(article('html').attr('lang'), lang);
		assert.equal(article('a[hreflang]').attr('href'), `/${other}/${slug}/`);
		assert.ok(index.some(entry => entry.url === `/${lang}/${slug}/` && entry.text.length > 60));
	}
}

assert.equal(errors.size, 0, [...errors].join('\n'));
console.log(`Checked ${pages.size} pages, internal routes, section anchors and bilingual search indexes.`);
