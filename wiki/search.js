const input = document.querySelector('#search');
const results = document.querySelector('#search-results');
const lang = document.documentElement.lang;
const normalize = value => value.toLocaleLowerCase(lang).normalize('NFKC');
let request;
let generation = 0;

if (matchMedia('(max-width: 44rem)').matches)
	document.querySelector('.nav-shell').open = false;

input.disabled = false;
function closeSearch() {
	generation++;
	results.hidden = true;
}

async function updateSearch() {
	const current = ++generation;
	const terms = normalize(input.value.trim()).split(/\s+/).filter(Boolean);
	if (terms.length === 0) {
		results.hidden = true;
		return;
	}
	request ??= fetch(`/${lang}/search.json`).then(response => {
		if (!response.ok)
			throw new Error('Search index unavailable');
		return response.json();
	});
	let index;
	try {
		index = await request;
	} catch {
		request = undefined;
		return;
	}
	if (current !== generation)
		return;
	const matches = [];
	for (const entry of index) {
		const title = normalize(entry.title);
		const group = normalize(entry.group);
		const text = normalize(entry.text);
		let score = 0;
		for (const term of terms) {
			const weight = title.includes(term) ? 8 : group.includes(term) ? 4 : text.includes(term) ? 1 : 0;
			if (!weight) {
				score = 0;
				break;
			}
			score += weight;
		}
		if (score)
			matches.push({ ...entry, score });
	}
	results.replaceChildren();
	for (const entry of matches.sort((a, b) => b.score - a.score).slice(0, 12)) {
		const item = document.createElement('li');
		const link = document.createElement('a');
		link.href = entry.url;
		link.textContent = entry.title;
		const group = document.createElement('small');
		group.textContent = entry.group;
		link.append(group);
		item.append(link);
		results.append(item);
	}
	if (!matches.length) {
		const item = document.createElement('li');
		const text = document.createElement('span');
		text.textContent = input.parentElement.dataset.empty;
		item.append(text);
		results.append(item);
	}
	results.hidden = false;
}

input.addEventListener('input', updateSearch);
input.addEventListener('focus', updateSearch);
document.addEventListener('click', event => {
	if (!input.parentElement.contains(event.target))
		closeSearch();
});
document.addEventListener('focusin', event => {
	if (!input.parentElement.contains(event.target))
		closeSearch();
});
document.addEventListener('keydown', event => {
	if (event.key === '/' && !event.altKey && !event.metaKey && !event.ctrlKey &&
	    !event.target.closest('input, textarea, [contenteditable]')) {
		event.preventDefault();
		input.focus();
	}
	if (event.key === 'Escape' && input.parentElement.contains(event.target)) {
		input.focus();
		closeSearch();
	}
	if (event.key === 'ArrowDown' && event.target === input && !results.hidden) {
		event.preventDefault();
		results.querySelector('a')?.focus();
	}
});
