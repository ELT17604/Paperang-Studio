/** Browser/Node fetch client. Keep the local token out of public source code. */
export class PaperangClient {
  constructor(token, baseURL = 'http://127.0.0.1:8176') {
    this.token = token;
    this.baseURL = baseURL.replace(/\/$/, '');
  }
  async request(method, path, body) {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 12000);
    try {
      const response = await fetch(this.baseURL + path, {
        method,
        headers: { Authorization: `Bearer ${this.token}`, 'Content-Type': 'application/json' },
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: controller.signal,
      });
      const value = await response.json();
      if (!response.ok) throw new Error(`Paperang ${response.status}: ${value.error?.message ?? 'Request failed'}`);
      return value;
    } finally { clearTimeout(timer); }
  }
  status() { return this.request('GET', '/v1/status'); }
  devices(all = false) { return this.request('GET', `/v1/devices${all ? '?all=true' : ''}`); }
  render(text, settings = {}, includeRaw = false) {
    return this.request('POST', '/v1/render', { text, settings, includeRaw });
  }
  printText(text, { settings = {}, allowPaper } = {}) {
    return this.request('POST', '/v1/jobs', { text, settings, allowPaper });
  }
  printImage(imageBase64, { settings = {}, allowPaper } = {}) {
    return this.request('POST', '/v1/jobs', { imageBase64, settings, allowPaper });
  }
  job(id) { return this.request('GET', `/v1/jobs/${encodeURIComponent(id)}`); }
}
