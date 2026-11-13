import { GameError } from './policy.js';

export const RESPONSE_SCHEMA = {
  type: 'OBJECT', properties: {
    guardDialogue: { type: 'STRING' },
    emotionKey: { type: 'STRING', enum: ['Neutral', 'Thinking', 'Convinced', 'Suspicious'] },
    trustChange: { type: 'NUMBER' }, reasonTag: { type: 'STRING' },
    isDeadEnd: { type: 'BOOLEAN' }, deadEndReason: { type: 'STRING' },
    presetOptions: { type: 'ARRAY', minItems: 3, maxItems: 3, items: {
      type: 'OBJECT', properties: { summaryText: { type: 'STRING' }, fullDialogue: { type: 'STRING' } },
      required: ['summaryText', 'fullDialogue'],
    } },
  }, required: ['guardDialogue', 'emotionKey', 'trustChange', 'reasonTag', 'isDeadEnd', 'deadEndReason', 'presetOptions'],
  propertyOrdering: ['guardDialogue', 'emotionKey', 'trustChange', 'reasonTag', 'isDeadEnd', 'deadEndReason', 'presetOptions'],
};

export function plain(text) { return text.replace(/<[^>]*(?:>|$)/g, '').replace(/[<>]/g, ''); }

export function validateResponse(text) {
  const value = JSON.parse(text.replace(/^\s*```(?:json)?\s*|\s*```\s*$/g, ''));
  const bounded = (s, max, empty = false) => typeof s === 'string' && s.length <= max && (empty || s.trim());
  if (!value || !bounded(value.guardDialogue, 600) ||
      !['Neutral', 'Thinking', 'Convinced', 'Suspicious'].includes(value.emotionKey) ||
      !Number.isFinite(value.trustChange) || typeof value.isDeadEnd !== 'boolean' ||
      !bounded(value.reasonTag, 80, true) || !bounded(value.deadEndReason, 300, true) ||
      !Array.isArray(value.presetOptions) || value.presetOptions.length !== 3 ||
      value.presetOptions.some(p => !p || !bounded(p.summaryText, 40) || !bounded(p.fullDialogue, 400)))
    throw new GameError('invalid_model_response', 502);
  return {
    guardDialogue: plain(value.guardDialogue), emotionKey: value.emotionKey,
    trustChange: Math.max(-10, Math.min(60, value.trustChange)),
    reasonTag: plain(value.reasonTag), isDeadEnd: value.isDeadEnd,
    deadEndReason: plain(value.deadEndReason),
    presetOptions: value.presetOptions.map(p => ({ summaryText: plain(p.summaryText), fullDialogue: plain(p.fullDialogue) })),
  };
}

// Decode only complete JSON string characters, including escaped newlines and Unicode.
// An incomplete escape/code point waits for the next network chunk.
export function partialDialogue(json) {
  const match = /"guardDialogue"\s*:\s*"/.exec(json);
  if (!match) return '';
  let decoded = '';
  for (let i = match.index + match[0].length; i < json.length; i++) {
    const c = json[i];
    if (c === '"') break;
    if (c === '\\') {
      const escaped = json[++i];
      if (!escaped) break;
      if (escaped === 'u') {
        const hex = json.slice(i + 1, i + 5);
        if (!/^[0-9a-f]{4}$/i.test(hex)) break;
        decoded += String.fromCharCode(parseInt(hex, 16)); i += 4;
      } else {
        const escapes = { '"': '"', '\\': '\\', '/': '/', n: '\n', r: '\r', t: '\t', b: '\b', f: '\f' };
        if (!(escaped in escapes)) break;
        decoded += escapes[escaped];
      }
    } else decoded += c;
  }
  if (/[\uD800-\uDBFF]$/.test(decoded)) decoded = decoded.slice(0, -1);
  return plain(decoded).slice(0, 600);
}

export async function* sseData(body) {
  const decoder = new TextDecoder();
  let buffer = '';
  const reader = body.getReader();
  try {
    while (true) {
      const { value, done } = await reader.read();
      buffer += done ? decoder.decode() : decoder.decode(value, { stream: true });
      buffer = buffer.replace(/\r\n/g, '\n');
      let end;
      while ((end = buffer.indexOf('\n\n')) >= 0) {
        const event = buffer.slice(0, end); buffer = buffer.slice(end + 2);
        const data = event.split('\n').filter(line => line.startsWith('data:'))
          .map(line => line.slice(5).trimStart()).join('\n');
        if (data) yield data;
      }
      if (buffer.length > 65536) throw new GameError('invalid_provider_stream', 502);
      if (done) {
        if (buffer.trim()) throw new GameError('truncated_provider_stream', 502);
        break;
      }
    }
  } finally { await reader.cancel().catch(() => {}); reader.releaseLock(); }
}
