import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { load } from 'cheerio';
import GithubSlugger from 'github-slugger';
import MarkdownIt from 'markdown-it';
import { groups, references } from './catalog.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const out = path.join(here, 'dist');
const repo = 'https://github.com/ELT17604/Paperang-Studio';
const git = (...args) => execFileSync('git', ['-C', here, ...args], { maxBuffer: 20 * 1024 * 1024 });
const snapshot = path.join(here, 'snapshot');
const manifest = path.join(snapshot, 'source.json');
const archived = existsSync(manifest);
const { revision, paths } = archived ? JSON.parse(readFileSync(manifest, 'utf8')) : {
	revision: git('rev-parse', 'HEAD').toString().trim(),
	paths: git('ls-tree', '-r', '--full-tree', '--name-only', '-z', 'HEAD').toString().split('\0').filter(Boolean),
};
const files = new Set(paths);
const usedFiles = new Set();
function source(file) {
	usedFiles.add(file);
	return archived ? readFileSync(path.join(snapshot, file)) : git('show', `${revision}:${file}`);
}
const markdown = new MarkdownIt({ html: true, linkify: true });
const langs = ['en', 'zh-CN'];
const topics = groups.flatMap(group => group[3].map(entry => ({ group, entry })));
const localized = (pair, lang) => pair[lang === 'en' ? 0 : 1];
const esc = value => String(value).replace(/[&<>"']/g, char => ({
	'&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;',
})[char]);
const route = (lang, slug = '') => `/${lang}/${slug ? slug + '/' : ''}`;
const sourceUrl = file => `${repo}/blob/${revision}/${file.split('/').map(encodeURIComponent).join('/')}`;
const write = (file, contents) => {
	const target = path.join(out, file);
	mkdirSync(path.dirname(target), { recursive: true });
	writeFileSync(target, contents);
};

function versionsFor(entry) {
	const [, , , base, alternate, original = 'en'] = entry;
	if (!base)
		return {};
	const chinese = alternate ?? (base.endsWith('.zh-CN.md') ? base : base.replace(/\.md$/, '.zh-CN.md'));
	return {
		en: original === 'en' && !base.endsWith('.zh-CN.md') && files.has(base) ? base : undefined,
		'zh-CN': original === 'zh-CN' ? base : files.has(chinese) ? chinese : undefined,
	};
}

const knownDocs = new Map();
for (const { entry } of topics) {
	for (const [lang, file] of Object.entries(versionsFor(entry))) {
		if (file)
			knownDocs.set(file, { slug: entry[0], lang });
	}
}
const uncovered = [...files].filter(file => file.endsWith('.md') && !file.startsWith('wiki/') && !knownDocs.has(file));
if (uncovered.length)
	throw new Error(`Add these documents to catalog.mjs: ${uncovered.join(', ')}`);

rmSync(out, { recursive: true, force: true });
for (const file of ['style.css', 'search.js'])
	write(file, readFileSync(path.join(here, file)));
const asset = file => {
	const url = `/files/${file}`;
	write(url.slice(1), source(file));
	return url;
};
const icon = asset('assets/AppIcon.png');
const refUrls = new Map(references.map(([file]) => [file, asset(file)]));
const registry = JSON.parse(source('VERSIONS.json'));
const missingLinks = new Set();

function renderArticle(input, file, lang) {
	const $ = load(markdown.render(input), {}, false);
	const slugger = new GithubSlugger();
	$('h1').first().remove();
	$('h1').each((_, heading) => { heading.tagName = 'h2'; });
	$('script, iframe, object, embed, style, link, meta, form').remove();
	$('*').each((_, element) => {
		for (const name of Object.keys(element.attribs ?? {})) {
			if (/^on/i.test(name))
				$(element).removeAttr(name);
		}
	});
	const headings = [];
	$('h2, h3, h4, h5, h6').each((_, el) => {
		const heading = $(el);
		const title = heading.text();
		const id = slugger.slug(title);
		heading.attr('id', id);
		if (el.tagName === 'h2' || el.tagName === 'h3')
			headings.push({ id, title, nested: el.tagName === 'h3' });
		heading.append(` <a class="anchor" href="#${esc(id)}" aria-label="${lang === 'en' ? 'Link to' : '链接到'} ${esc(title)}">#</a>`);
	});
	$('a[href], img[src]').each((_, element) => {
		const attr = element.tagName === 'img' ? 'src' : 'href';
		const value = $(element).attr(attr);
		if (/^(?:javascript|vbscript|data):/i.test(value)) {
			$(element).removeAttr(attr);
			return;
		}
		if (!file || /^(?:[a-z]+:|\/|#)/i.test(value))
			return;
		const [target, fragment] = value.split('#', 2);
		const resolved = path.posix.normalize(path.posix.join(path.posix.dirname(file), decodeURI(target)));
		const doc = knownDocs.get(resolved);
		let url;
		if (doc) {
			url = route(fragment ? doc.lang : lang, doc.slug);
		} else if (refUrls.has(resolved)) {
			url = refUrls.get(resolved);
		} else if (files.has(resolved)) {
			url = /\.(png|jpe?g|webp|svg|gif)$/i.test(resolved) ? asset(resolved) : sourceUrl(resolved);
		} else {
			const isDirectory = [...files].some(name => name.startsWith(resolved.replace(/\/$/, '') + '/'));
			if (!isDirectory)
				missingLinks.add(`${file} → ${value}`);
			url = `${repo}/${isDirectory ? 'tree' : 'blob'}/${revision}/${resolved}`;
		}
		$(element).attr(attr, url + (fragment ? '#' + fragment : ''));
	});
	$('table').wrap('<div class="table-scroll" tabindex="0"></div>');
	$('img').attr('loading', 'lazy');
	const plain = $.text().replace(/\s+/g, ' ').trim();
	return { html: $.html(), headings, plain };
}

const labels = {
	en: { title: 'Documentation', menu: 'Browse topics', search: 'Search documentation', toc: 'On this page',
		source: 'Source on GitHub', all: 'All documents', refs: 'Reference files', next: 'Next', prev: 'Previous',
		no: 'No matching documents.', fallback: 'Original document in Simplified Chinese.', home: 'Overview',
		skip: 'Skip to content', language: '简体中文', locale: 'en', links: 'GitHub' },
	'zh-CN': { title: '项目文档', menu: '浏览主题', search: '搜索文档', toc: '本页目录',
		source: '在 GitHub 查看原文', all: '全部文档', refs: '参考文件', next: '下一篇', prev: '上一篇',
		no: '没有匹配的文档。', fallback: '本文原文为英文。', home: '首页',
		skip: '跳到正文', language: 'English', locale: 'zh-CN', links: 'GitHub' },
};

const link = (lang, slug, title, active = '') =>
	`<a href="${route(lang, slug)}"${slug === active ? ' aria-current="page"' : ''}>${esc(title)}</a>`;

function page(lang, slug, title, article, toc = '') {
	const t = labels[lang];
	const other = lang === 'en' ? 'zh-CN' : 'en';
	const groupsHtml = groups.map(([id, en, zh, entries]) => {
		const open = entries.some(entry => entry[0] === slug);
		return `<details${open ? ' open' : ''}><summary>${localized([en, zh], lang)}</summary>
			<ul>${entries.map(([key, en, zh]) => `<li>${link(lang, key, localized([en, zh], lang), slug)}</li>`).join('')}</ul></details>`;
	}).join('');
	const titleTag = slug ? `${title} · Paperang Studio` : `Paperang Studio · ${t.title}`;
	const description = lang === 'en' ? 'Paperang Studio client guides, self-hosted Relay, APIs and P2 printer protocols.' : 'Paperang Studio 客户端、远程打印、API 与 Paperang P2 协议文档。';
	return `<!doctype html><html lang="${lang}"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
	<title>${esc(titleTag)}</title><meta name="description" content="${esc(description)}"><link rel="icon" href="${icon}">
	<link rel="alternate" hreflang="${other}" href="${route(other, slug)}"><link rel="stylesheet" href="/style.css"><script src="/search.js" defer></script>
	</head><body><a class="skip" href="#content">${t.skip}</a>
	<header class="top"><div class="bar"><a class="brand" href="${route(lang)}"><img src="${icon}" width="36" height="36" alt=""><span>Paperang Studio</span></a>
	<div class="search" data-empty="${t.no}"><label class="sr-only" for="search">${t.search}</label><input id="search" type="search" placeholder="${t.search}" autocomplete="off" aria-controls="search-results" disabled><kbd aria-hidden="true">/</kbd><ul id="search-results" hidden></ul></div>
	<nav class="top-links" aria-label="${t.title}"><a href="${route(other, slug)}" lang="${other}" hreflang="${other}">${t.language}</a><a href="${repo}">GitHub ↗</a></nav></div></header>
	<div class="layout"><aside class="sidebar"><details class="nav-shell" open><summary>${t.menu}</summary><nav aria-label="${t.menu}">
	${link(lang, '', t.home, slug)}${link(lang, 'index', t.all, slug)}${groupsHtml}</nav></details></aside>
	<main id="content" class="${slug === '' ? 'landing' : ''}">${article}</main>
	${toc ? `<aside class="toc" aria-label="${t.toc}"><p>${t.toc}</p>${toc}</aside>` : '<div class="rail"></div>'}</div>
	<footer class="site-footer">© 2026 ELT Group <a href="${refUrls.get('LICENSE')}">CC BY-NC-SA 4.0</a></footer></body></html>`;
}

function homepage(lang) {
	const en = lang === 'en';
	const release = registry.publishedProductRelease;
	const heroText = en ? 'Print on Paperang P2 from macOS, Android or Windows. Send notes and drawings from a browser with a self-hosted Relay.' : '在 macOS、Android 或 Windows 上编辑纸条并打印到 Paperang P2。也可通过自托管 Relay，从浏览器发送文字和手绘。';
	const sequence = [
		['first-print', 'Make your first print', '完成第一次打印', 'Install a client, connect the P2, and send a preview.', '选一个客户端，连接 P2，再把预览发送到打印机。'],
		['remote-printing', 'Set up remote printing', '搭建远程打印', 'Deploy the Relay and connect a desktop receiver.', '部署 Relay，在 MacClient 或 WinLite 上配置接收。'],
		['api-v2-032', 'Integrate with the API', '接入 API', 'Read the HTTP v2 contract, credentials and job flow.', '查看 HTTP v2 契约、凭据和任务流转。'],
	];
	const screenshots = [
		['MacClient', 'user-guide', 'doc/assets/macclient-v0.3.1-fix3-workspace.png'],
		['WinLite', 'windows', 'assets/screenshots/winlite-v0.3.0-template-editor.jpg'],
		['AndroidClient', 'android', 'assets/screenshots/androidclient-v0.3.3-drawing.png'],
	];
	return `<section class="hero"><img class="hero-icon" src="${icon}" width="64" height="64" alt="">
	<p class="eyebrow">${en ? 'Project documentation' : '项目文档'}</p><h1>Paperang Studio</h1><p class="lede">${heroText}</p>
	<div class="actions"><a class="primary" href="${route(lang, 'first-print')}">${en ? 'Start printing' : '开始打印'} →</a>
	<a href="${route(lang, 'index')}">${labels[lang].all}</a></div></section>
	<section><h2>${en ? 'From setup to integration' : '从安装到接入'}</h2><ol class="steps">
	${sequence.map(([slug, a, b, c, d], i) => `<li><span class="step">${i + 1}</span><div>${link(lang, slug, en ? a : b)}<p>${en ? c : d}</p></div></li>`).join('')}</ol></section>
	<section><h2>${en ? 'Choose your client' : '选择客户端'}</h2><div class="gallery">
	${screenshots.map(([name, slug, file]) => `<a href="${route(lang, slug)}"><img src="${asset(file)}" alt="" loading="lazy"><strong>${name} <span aria-hidden="true">↗</span></strong></a>`).join('')}</div></section>
	<section class="release-strip"><p>${en ? 'Published project release' : '已发布的项目版本'} <strong>${esc(release)}</strong></p><p>${en ? 'Compatibility baseline' : '兼容基线'} <strong>${esc(registry.baseline)}</strong></p>
	${link(lang, 'versioning', en ? 'Version policy' : '版本规则')} <span>·</span> ${link(lang, 'feature-matrix', en ? 'Feature matrix' : '功能矩阵')}</section>`;
}

function indexPage(lang) {
	const en = lang === 'en';
	return `<article class="prose"><h1>${labels[lang].all}</h1><p>${en ? 'Browse the documentation by topic, or search across articles from the top bar.' : '按主题浏览完整文档，也可以在顶部搜索正文。'}</p>
	<div class="index-grid">${groups.map(([id, a, b, entries]) => `<section id="${id}"><h2>${en ? a : b}</h2><ul>
	${entries.map(([slug, a, b]) => `<li>${link(lang, slug, en ? a : b)}</li>`).join('')}</ul></section>`).join('')}
	<section><h2>${labels[lang].refs}</h2><ul>${references.map(([file, a, b]) =>
		`<li><a href="${refUrls.get(file)}">${en ? a : b}</a></li>`).join('')}</ul></section></div></article>`;
}

for (const lang of langs) {
	const t = labels[lang];
	const search = [];
	write(`${lang}/index.html`, page(lang, '', t.home, homepage(lang)));
	write(`${lang}/index/index.html`, page(lang, 'index', t.all, indexPage(lang)));
	for (const { group, entry } of topics) {
		const [slug, en, zh] = entry;
		const title = localized([en, zh], lang);
		const localizedFiles = versionsFor(entry);
		const overlay = (slug === 'first-print' || (slug === 'android' && lang === 'en'));
		const contentLang = overlay || localizedFiles[lang] ? lang : lang === 'en' ? 'zh-CN' : 'en';
		const file = localizedFiles[contentLang];
		if (!overlay && !file) throw new Error(`Missing source for ${slug} (${lang})`);

		const input = overlay ? readFileSync(path.join(here, 'content', `${slug}.${lang}.md`), 'utf8') : source(file).toString();
		const { html, headings, plain } = renderArticle(input, overlay ? null : file, lang);
		const toc = headings.length ? `<ul>${headings.map(({ id, title, nested }) =>
			`<li${nested ? ' class="nested"' : ''}><a href="#${esc(id)}">${esc(title)}</a></li>`).join('')}</ul>` : '';
		const groupTitle = localized(group.slice(1, 3), lang);
		const entries = group[3];
		const position = entries.indexOf(entry);
		const steps = [[entries[position - 1], t.prev], [entries[position + 1], t.next]].map(([nearby, label]) =>
			nearby ? `<a href="${route(lang, nearby[0])}"><small>${label}</small>${esc(localized(nearby.slice(1, 3), lang))}</a>` : '<span></span>').join('');
		const fallback = contentLang !== lang ? `<p class="fallback">${t.fallback}</p>` : '';
		const article = `<nav class="crumbs" aria-label="${t.menu}"><a href="${route(lang)}">${t.home}</a><span>/</span><a href="${route(lang, 'index')}#${group[0]}">${groupTitle}</a></nav>
		<article class="prose"><h1>${esc(title)}</h1>${fallback}<div lang="${contentLang}">${html}</div></article>
		${file ? `<p class="source"><a href="${sourceUrl(file)}">${t.source} ↗</a></p>` : ''}<nav class="page-nav" aria-label="${t.menu}">${steps}</nav>`;
		write(`${lang}/${slug}/index.html`, page(lang, slug, title, article, toc));
		search.push({ title, group: groupTitle, url: route(lang, slug), text: plain });
	}
	write(`${lang}/search.json`, JSON.stringify(search));
}
write('index.html', page('en', '', 'Overview', homepage('en')));
write('404.html', page('en', 'index', 'Page not found', '<article class="prose"><h1>Page not found</h1><p><a href="/en/index/">Browse all documents</a> · <a href="/zh-CN/index/">浏览全部文档</a></p></article>'));
console.log(`Built ${topics.length} topics in ${langs.length} locales from ${revision.slice(0, 12)}.`);
if (missingLinks.size)
	console.warn(`Unresolved repository links:\n${[...missingLinks].join('\n')}`);
if (process.argv[2] === '--snapshot') {
	const destination = process.argv[3];
	if (!destination || archived)
		throw new Error('Specify a Site checkout directory for the documentation snapshot.');
	const snapshotPath = path.resolve(destination, 'snapshot');
	for (const file of usedFiles) {
		const target = path.join(snapshotPath, file);
		mkdirSync(path.dirname(target), { recursive: true });
		writeFileSync(target, source(file));
	}
	writeFileSync(path.join(snapshotPath, 'source.json'), JSON.stringify({ revision, paths }, null, 2) + '\n');
}
